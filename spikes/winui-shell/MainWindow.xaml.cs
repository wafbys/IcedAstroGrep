using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

using IcedAstroGrep;
using IcedAstroGrep.Core;
using IcedAstroGrep.Core.EncodingDetection;
using IcedAstroGrep.Core.Logging;
using IcedAstroGrep.Output;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;

using Windows.Graphics;

namespace WinUiShell
{
	/// <summary>
	/// M1 window: the search loop — the same engine and the same plug-ins the WinForms shell drives, the
	/// same cancellation order, and the results pane composed by the shared <see cref="ResultDocument"/>.
	/// </summary>
	/// <remarks>
	/// What is deliberately missing: the results viewer's highlighting and click-to-open (M2), settings,
	/// plug-in and editor screens (M3). What is here is the part the contract is strict about: every one
	/// of the ten events is handled, a second search never starts over a running one, and a failed search
	/// is shown rather than counted.
	/// </remarks>
	public sealed partial class MainWindow : Window
	{
		private static readonly TimeSpan PreviousSearchWaitTimeout = TimeSpan.FromSeconds(5);

		private readonly DispatcherQueue dispatcher;
		private readonly Stopwatch stopwatch = new Stopwatch();

		private Grep grep;
		private bool searching;
		private int filesSearched;
		private int hitFiles;
		private int lineHits;
		private int errorCount;
		private int displayContextLines = 2;
		private bool loadingSettings;
		private bool bridgeWired;
		private WinUiNotifier notifier;

		/// <summary>
		/// The dialog host for engine messages, created on first use because a window's XamlRoot is only
		/// available once its content is in the visual tree.
		/// </summary>
		private WinUiNotifier Notifier
		{
			get { return notifier ?? (notifier = new WinUiNotifier(Content == null ? null : Content.XamlRoot)); }
		}

		public MainWindow()
		{
			InitializeComponent();

			dispatcher = DispatcherQueue;

			// the caption carries the build identity, exactly like the WinForms shell's
			Title = string.Format("{0} {1}", ProductInformation.ApplicationName, ProductInformation.ApplicationVersionText);

			// open at a usable size rather than the default, and put the caret where the user starts
			AppWindow.Resize(new SizeInt32(1100, 760));
			SearchBox.Loaded += (sender, args) => SearchBox.Focus(FocusState.Programmatic);

			ResetError();

			try
			{
				// the same plug-in manager the WinForms shell uses, which also exercises the embedded
				// pdftotext resource and the settings files from a WinUI process
				PluginManager.Load();
				PluginText.Text = string.Format("plug-ins: {0}", PluginManager.Items.Count);
			}
			catch (Exception ex)
			{
				PluginText.Text = "plug-ins failed: " + ex.Message;
			}

			try
			{
				// the configured editors come from the same settings file the WinForms shell writes; with
				// none configured, opening a hit falls back to the file's associated application
				TextEditors.Load();
			}
			catch (Exception ex)
			{
				LogClient.Instance.Logger.Warn("The WinUI shell could not load the text editor settings: {0}", ex.Message);
			}

			ApplySettings();

			LogClient.Instance.Logger.Info("### WinUI shell spike started, engine {0} ###", ProductInformation.ApplicationVersionText);
		}

		/// <summary>
		/// Applies the settings the WinForms shell also owns, so a user can switch between the two shells
		/// without losing anything: the language and the search options come from the same files.
		/// </summary>
		private void ApplySettings()
		{
			try
			{
				Language.Load(GeneralSettings.Language);

				RecurseCheck.IsChecked = SearchSettings.UseRecursion;
				CaseCheck.IsChecked = SearchSettings.UseCaseSensitivity;
				WholeWordCheck.IsChecked = SearchSettings.UseWholeWordMatching;
				RegexCheck.IsChecked = SearchSettings.UseRegularExpressions;
				NegationCheck.IsChecked = SearchSettings.UseNegation;
				FileNamesOnlyCheck.IsChecked = SearchSettings.ReturnOnlyFileNames;

				// the search always captures the widest context; the pane shows this many lines around a hit
				displayContextLines = SearchSettings.ContextLinesBefore;

				LanguageBox.ItemsSource = Language.AvailableLanguages;

				// selecting the current language raises SelectionChanged, which would save the settings
				// again on every start; the flag keeps that inside ApplySettings
				loadingSettings = true;

				try
				{
					foreach (LanguageItem item in Language.AvailableLanguages)
					{
						if (string.Equals(item.Culture, GeneralSettings.Language, StringComparison.OrdinalIgnoreCase))
						{
							LanguageBox.SelectedItem = item;
							break;
						}
					}
				}
				finally
				{
					loadingSettings = false;
				}

				SetStatus(Text("SearchStarted", "Search Started"));
			}
			catch (Exception ex)
			{
				ShowError("Settings could not be loaded: " + ex.Message);
			}
		}

		private void LanguageChanged(object sender, SelectionChangedEventArgs e)
		{
			if (loadingSettings)
			{
				return;
			}

			var item = LanguageBox.SelectedItem as LanguageItem;

			if (item == null)
			{
				return;
			}

			try
			{
				Language.Load(item.Culture);
				GeneralSettings.Language = item.Culture;
				GeneralSettings.Save();

				// everything the engine says is localized from here on; the shell's own labels stay its own
				SetStatus(Text("SearchStarted", "Search Started"));
			}
			catch (Exception ex)
			{
				ShowError("The language could not be changed: " + ex.Message);
			}
		}

		/// <summary>
		/// Writes the search options back to the shared settings so the other shell sees them too.
		/// </summary>
		/// <param name="spec">Spec the search was built from</param>
		private void SaveSettings(SearchInterfaces.SearchSpec spec)
		{
			try
			{
				SearchSettings.UseRecursion = spec.SearchInSubfolders;
				SearchSettings.UseCaseSensitivity = spec.UseCaseSensitivity;
				SearchSettings.UseWholeWordMatching = spec.UseWholeWordMatching;
				SearchSettings.UseRegularExpressions = spec.UseRegularExpressions;
				SearchSettings.UseNegation = spec.UseNegation;
				SearchSettings.ReturnOnlyFileNames = spec.ReturnOnlyFileNames;

				SearchSettings.Save();
			}
			catch (Exception ex)
			{
				LogClient.Instance.Logger.Warn("The WinUI shell could not save the search options: {0}", ex.Message);
			}
		}

		/// <summary>
		/// Text for a key, with an English fallback so a missing key degrades to readable text.
		/// </summary>
		private static string Text(string key, string fallback)
		{
			return Language.GetGenericText(key, fallback);
		}

		/// <summary>
		/// Reports a problem through the InfoBar, which is the Fluent way to say something went wrong
		/// without blocking the user.
		/// </summary>
		private void ShowError(string message)
		{
			ErrorBar.Message = message;
			ErrorBar.IsOpen = true;
		}

		private void ResetError()
		{
			ErrorBar.IsOpen = false;
			ErrorBar.Message = string.Empty;
		}

		private void SearchAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
		{
			args.Handled = true;
			StartSearchClick(this, null);
		}

		private void CancelAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
		{
			args.Handled = true;
			CancelSearchClick(this, null);
		}

		private void FindAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
		{
			args.Handled = true;
			SearchBox.Focus(FocusState.Programmatic);
			SearchBox.SelectAll();
		}

		private void StartSearchClick(object sender, RoutedEventArgs e)
		{
			if (searching)
			{
				return;
			}

			ResetError();

			// This ordering is the contract, and it is why it is spelled out here rather than in a helper:
			// detach the handlers first, so the previous search can no longer marshal callbacks onto this
			// thread while we block waiting for it, and only then abort and wait. Starting a second search
			// over a running one would race it over the plug-in instances and the on-disk encoding cache.
			if (grep != null)
			{
				DetachEvents(grep);

				if (!grep.AbortAndWait(PreviousSearchWaitTimeout))
				{
					SetStatus("the previous search did not stop in time; starting anyway");
				}

				grep = null;
			}

			SearchInterfaces.SearchSpec spec;

			try
			{
				spec = BuildSearchSpec();
			}
			catch (Exception ex)
			{
				ShowError("Could not start the search: " + ex.Message);
				return;
			}

			SaveSettings(spec);

			FileList.Items.Clear();
			ClearResults();

			filesSearched = 0;
			hitFiles = 0;
			lineHits = 0;
			errorCount = 0;
			searching = true;
			stopwatch.Restart();

			StartButton.IsEnabled = false;
			CancelButton.IsEnabled = true;
			SearchPanel.IsEnabled = false;
			Progress.IsActive = true;
			SetStatus("searching\u2026");

			grep = new Grep(spec)
			{
				// the built-in plug-ins, exactly as the WinForms shell passes them
				Plugins = PluginManager.Items
			};

			grep.SearchingFile += OnSearchingFile;
			grep.FileHit += OnFileHit;
			grep.LineHit += OnLineHit;
			grep.SearchComplete += OnSearchComplete;
			grep.SearchCancel += OnSearchCancel;
			grep.SearchError += OnSearchError;
			grep.FileFiltered += OnFileFiltered;
			grep.DirectoryFiltered += OnDirectoryFiltered;
			grep.SearchingFileByPlugin += OnSearchingFileByPlugin;
			grep.FileEncodingDetected += OnFileEncodingDetected;

			grep.BeginExecute();
		}

		private void CancelSearchClick(object sender, RoutedEventArgs e)
		{
			// Abort() is cooperative: the engine checks the token between files and every 1024 lines,
			// so this returns immediately and the search stops shortly after.
			if (grep != null && searching)
			{
				SetStatus("cancelling\u2026");
				grep.Abort();
			}
		}

		private void OnSearchingFile(FileInfo file)
		{
			Interlocked.Increment(ref filesSearched);

			OnUiThread(() => SetStatus(string.Format(Text("SearchSearching", "Searching {0}"), filesSearched)));
		}

		private void OnFileHit(FileInfo file, int index)
		{
			Interlocked.Increment(ref hitFiles);

			// Read the result on the search thread: MatchResults is appended by that thread, and the
			// element for this index is already there. Only the finished string crosses to the UI thread.
			string line = DescribeHit(file, index);

			OnUiThread(() =>
			{
				FileList.Items.Add(line);
				SetStatus(string.Format("{0} file(s) with hits, {1} searched", hitFiles, filesSearched));
			});
		}

		private void OnLineHit(MatchResult match, int index)
		{
			// Deliberately not marshalled: this fires once per hit line and would flood the dispatcher.
			// The status line is refreshed by the file level events instead.
			Interlocked.Increment(ref lineHits);
		}

		private void OnSearchComplete()
		{
			OnUiThread(() => FinishSearch("finished"));
		}

		private void OnSearchCancel()
		{
			OnUiThread(() => FinishSearch("cancelled"));
		}

		private void OnSearchError(FileInfo file, Exception ex)
		{
			Interlocked.Increment(ref errorCount);

			// A search that quietly returns nothing is the worst failure mode this engine has, so a
			// pattern that exceeded the match timeout is called out in a dialog rather than folded into
			// a counter: it means the search stopped early and the results are incomplete.
			string line;

			if (ex is SearchRegexTimeoutException)
			{
				line = "SEARCH STOPPED: " + ex.Message;
			}
			else if (file == null)
			{
				line = Text("SearchGenericError", "An error occurred searching.") + " " + ex.Message;
			}
			else
			{
				line = string.Format(Text("SearchFileError", "An error occurred searching file: {0}."), file.FullName) + " " + ex.Message;
			}

			LogClient.Instance.Logger.Warn("WinUI shell search error: {0}", line);

			OnUiThread(() =>
			{
				ShowError(line);

				if (ex is SearchRegexTimeoutException)
				{
					Notifier.Show(line, NotificationSeverity.Warning);
				}
			});
		}

		private void OnFileFiltered(FileInfo file, FilterItem filterItem, string value)
		{
			// the shell would add these to a log view; M1 only needs the event handled
		}

		private void OnDirectoryFiltered(DirectoryInfo dir, FilterItem filterItem, string value)
		{
		}

		private void OnSearchingFileByPlugin(FileInfo file, string pluginName)
		{
			OnUiThread(() => SetStatus(string.Format(Text("SearchSearchingByPlugin", "Plugin {0} was used to search the file."), pluginName)));
		}

		private void OnFileEncodingDetected(FileInfo file, System.Text.Encoding encoding, string encoderName)
		{
		}

		private void FinishSearch(string outcome)
		{
			searching = false;
			stopwatch.Stop();

			Progress.IsActive = false;
			StartButton.IsEnabled = true;
			CancelButton.IsEnabled = false;
			SearchPanel.IsEnabled = true;

			SetStatus(string.Format(
				"{0} in {1:0.00}s: {2} file(s) searched, {3} with hits, {4} hit line(s), {5} error(s)",
				Text(outcome == "cancelled" ? "SearchCancelled" : "SearchFinished", outcome),
				stopwatch.Elapsed.TotalSeconds, filesSearched, hitFiles, lineHits, errorCount));

			_ = ShowResultsAsync();
		}

		/// <summary>
		/// Shows the results in the WebView, using the very markup an HTML export produces.
		/// </summary>
		/// <remarks>
		/// Route A of the evaluation: the pane, the exporter and the printed page are one document, so
		/// there is nothing to keep in step. The lines carry their source position because the shell asks
		/// for it, and a small script posts that position back when a line is clicked.
		/// </remarks>
		private async System.Threading.Tasks.Task ShowResultsAsync()
		{
			if (grep == null)
			{
				return;
			}

			try
			{
				var settings = new MatchResultsExportSettings
				{
					Grep = grep,
					GrepIndexes = Enumerable.Range(0, grep.MatchResults.Count).ToList(),
					ShowLineNumbers = true,
					ContextLinesBefore = displayContextLines,
					ContextLinesAfter = displayContextLines,
					IncludeSourceLocations = true
				};

				string html = MatchResultsExport.BuildResultsAsHTML(settings);

				await ResultsView.EnsureCoreWebView2Async();

				if (!bridgeWired)
				{
					ResultsView.CoreWebView2.WebMessageReceived += OnResultsMessage;
					bridgeWired = true;
				}

				ResultsView.CoreWebView2.NavigateToString(html.Replace("</body>", ClickBridge + "</body>"));
			}
			catch (Exception ex)
			{
				ShowError("Could not show the results: " + ex.Message);
			}
		}

		/// <summary>
		/// Opens a hit in the configured text editor. The position comes from the clicked line's data
		/// attributes, so this is the same "open at line and column" the WinForms shell offers.
		/// </summary>
		private void OnResultsMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
		{
			try
			{
				using (JsonDocument message = JsonDocument.Parse(args.WebMessageAsJson))
				{
					JsonElement root = message.RootElement;

					string file = root.GetProperty("file").GetString();
					int line = ParseNumber(root, "line");
					int column = ParseNumber(root, "column");
					string text = root.TryGetProperty("text", out JsonElement textElement) ? textElement.GetString() : string.Empty;

					TextEditors.Open(new TextEditorOpener(file, line, column, text, SearchBox.Text), Notifier);
				}
			}
			catch (Exception ex)
			{
				LogClient.Instance.Logger.Warn("The WinUI shell could not open the clicked result: {0}", ex.Message);
			}
		}

		private static int ParseNumber(JsonElement root, string name)
		{
			if (!root.TryGetProperty(name, out JsonElement element))
			{
				return -1;
			}

			return int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;
		}

		private void PrintResultsClick(object sender, RoutedEventArgs e)
		{
			try
			{
				// the WinUI shell has no printing of its own; the WebView brings it, which is one of the
				// reasons route A was chosen
				ResultsView.CoreWebView2?.ShowPrintUI();
			}
			catch (Exception ex)
			{
				ShowError("Could not print the results: " + ex.Message);
			}
		}

		private void ClearResults()
		{
			try
			{
				ResultsView.CoreWebView2?.NavigateToString("<html><body></body></html>");
			}
			catch (Exception)
			{
				// the control is not ready yet, and an empty pane is what we wanted anyway
			}
		}

		/// <summary>
		/// Turns a click on a result line into a message to the shell. Written with single quotes so it
		/// can live in a verbatim string.
		/// </summary>
		private const string ClickBridge = @"
<script>
document.querySelectorAll('.srcline').forEach(function (el) {
    el.style.cursor = 'pointer';
    el.title = 'Open this line in the configured text editor';
    el.addEventListener('click', function () {
        window.chrome.webview.postMessage({
            file: el.getAttribute('data-file'),
            line: el.getAttribute('data-line'),
            column: el.getAttribute('data-column'),
            text: el.textContent
        });
    });
});
</script>";

		private SearchInterfaces.SearchSpec BuildSearchSpec()
		{
			string folder = PathBox.Text.Trim();

			if (folder.Length == 0)
			{
				throw new InvalidOperationException("Enter a folder to search.");
			}

			if (!Directory.Exists(folder))
			{
				throw new DirectoryNotFoundException(folder);
			}

			return new SearchInterfaces.SearchSpec
			{
				StartDirectories = new[] { folder },
				SearchText = SearchBox.Text,
				SearchInSubfolders = RecurseCheck.IsChecked == true,
				UseCaseSensitivity = CaseCheck.IsChecked == true,
				UseWholeWordMatching = WholeWordCheck.IsChecked == true,
				UseRegularExpressions = RegexCheck.IsChecked == true,
				UseNegation = NegationCheck.IsChecked == true,
				ReturnOnlyFileNames = FileNamesOnlyCheck.IsChecked == true,
				FileFilter = string.IsNullOrWhiteSpace(FileTypesBox.Text) ? "*.*" : FileTypesBox.Text.Trim(),

				// The search captures the widest context the shell can offer and the display picks from
				// it: a shell that only widened the display side would find no context lines to show.
				ContextLines = Constants.MAX_CONTEXT_LINES,

				EncodingDetectionOptions = new EncodingOptions
				{
					DetectFileEncoding = true,
					UseEncodingCache = true
				}
			};
		}

		private string DescribeHit(FileInfo file, int index)
		{
			try
			{
				MatchResult result = grep.MatchResults[index];

				return string.Format("{0,6} hit(s)   {1}", result.HitCount, file.FullName);
			}
			catch (Exception)
			{
				return file.FullName;
			}
		}

		private void DetachEvents(Grep previous)
		{
			previous.SearchingFile -= OnSearchingFile;
			previous.FileHit -= OnFileHit;
			previous.LineHit -= OnLineHit;
			previous.SearchComplete -= OnSearchComplete;
			previous.SearchCancel -= OnSearchCancel;
			previous.SearchError -= OnSearchError;
			previous.FileFiltered -= OnFileFiltered;
			previous.DirectoryFiltered -= OnDirectoryFiltered;
			previous.SearchingFileByPlugin -= OnSearchingFileByPlugin;
			previous.FileEncodingDetected -= OnFileEncodingDetected;
		}

		/// <summary>
		/// Runs the action on the UI thread: every engine event arrives on the search thread.
		/// </summary>
		private void OnUiThread(Action action)
		{
			if (dispatcher.HasThreadAccess)
			{
				action();
				return;
			}

			dispatcher.TryEnqueue(() => action());
		}

		private void SetStatus(string text)
		{
			StatusText.Text = text;
		}
	}
}
