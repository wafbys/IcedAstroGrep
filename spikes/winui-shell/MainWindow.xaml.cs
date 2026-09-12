using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

using IcedAstroGrep;
using IcedAstroGrep.Core;
using IcedAstroGrep.Core.EncodingDetection;
using IcedAstroGrep.Core.Logging;
using IcedAstroGrep.Display;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
		private const int DisplayContextLines = 2;

		private static readonly TimeSpan PreviousSearchWaitTimeout = TimeSpan.FromSeconds(5);

		private readonly DispatcherQueue dispatcher;
		private readonly Stopwatch stopwatch = new Stopwatch();

		private Grep grep;
		private bool searching;
		private int filesSearched;
		private int hitFiles;
		private int lineHits;
		private int errorCount;

		public MainWindow()
		{
			InitializeComponent();

			dispatcher = DispatcherQueue;

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

			LogClient.Instance.Logger.Info("### WinUI shell spike started, engine {0} ###", ProductInformation.ApplicationVersionText);
		}

		private void StartSearchClick(object sender, RoutedEventArgs e)
		{
			if (searching)
			{
				return;
			}

			ErrorText.Text = string.Empty;

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
				ErrorText.Text = "Could not start the search: " + ex.Message;
				return;
			}

			FileList.Items.Clear();
			ResultsPane.Text = string.Empty;

			filesSearched = 0;
			hitFiles = 0;
			lineHits = 0;
			errorCount = 0;
			searching = true;
			stopwatch.Restart();

			StartButton.IsEnabled = false;
			CancelButton.IsEnabled = true;
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

			OnUiThread(() => SetStatus(string.Format("{0} file(s) searched\u2026", filesSearched)));
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
			// pattern that exceeded the match timeout is called out rather than folded into a count.
			string line = ex is SearchRegexTimeoutException
				? "SEARCH STOPPED: " + ex.Message
				: string.Format("error{0}: {1}", file == null ? string.Empty : " in " + file.FullName, ex.Message);

			LogClient.Instance.Logger.Warn("WinUI shell search error: {0}", line);

			OnUiThread(() =>
			{
				ErrorText.Text = string.IsNullOrEmpty(ErrorText.Text) ? line : ErrorText.Text + Environment.NewLine + line;
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
			OnUiThread(() => SetStatus(string.Format("searching {0} with the {1} plug-in\u2026", file.Name, pluginName)));
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

			SetStatus(string.Format(
				"search {0} in {1:0.00}s: {2} file(s) searched, {3} with hits, {4} hit line(s), {5} error(s)",
				outcome, stopwatch.Elapsed.TotalSeconds, filesSearched, hitFiles, lineHits, errorCount));

			ShowResults();
		}

		private void ShowResults()
		{
			if (grep == null)
			{
				return;
			}

			try
			{
				// The same composition the WinForms pane uses. This is the payoff of moving it into
				// AppServices: the layout, the source line numbers and the blank lines between files are
				// not re-implemented here.
				ResultDocument document = ResultDocument.Build(grep.MatchResults, new ResultDocumentOptions
				{
					BeforeContextLines = DisplayContextLines,
					AfterContextLines = DisplayContextLines
				});

				ResultsPane.Text = document.Text;
			}
			catch (Exception ex)
			{
				ErrorText.Text = "Could not compose the results: " + ex.Message;
			}
		}

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
