using System;
using System.Threading.Tasks;

using IcedAstroGrep;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUiShell
{
	/// <summary>
	/// Shows engine messages with a WinUI dialog.
	/// </summary>
	/// <remarks>
	/// This is the whole shell side of <see cref="IUserNotifier"/>: the engine names a language key and
	/// passes format arguments, the shell turns that into text (through the shared <see cref="Language"/>)
	/// and picks how to show it. The WinForms shell does the same with a message box.
	/// </remarks>
	public sealed class WinUiNotifier : IUserNotifier
	{
		private readonly XamlRoot xamlRoot;

		/// <summary>
		/// Creates a notifier that shows its dialogs in the given window content.
		/// </summary>
		/// <param name="xamlRoot">XamlRoot of the window the dialogs belong to</param>
		public WinUiNotifier(XamlRoot xamlRoot)
		{
			this.xamlRoot = xamlRoot;
		}

		/// <summary>
		/// Shows the named message. Failures here are swallowed: a shell that cannot show a message must
		/// not take the application down with it, and the engine logs the same failure independently.
		/// </summary>
		/// <param name="languageKey">Key of the text to show</param>
		/// <param name="formatArguments">Arguments for the text's placeholders, or null</param>
		/// <param name="severity">How much attention the message deserves</param>
		public void Notify(string languageKey, object[] formatArguments, NotificationSeverity severity)
		{
			string text = Language.GetGenericText(languageKey, languageKey);

			if (formatArguments != null && formatArguments.Length > 0)
			{
				try
				{
					text = string.Format(text, formatArguments);
				}
				catch (FormatException)
				{
					// a text without the expected placeholders must still be shown
				}
			}

			Show(text, severity);
		}

		/// <summary>
		/// Shows text the shell itself composed.
		/// </summary>
		/// <param name="text">Text to show</param>
		/// <param name="severity">How much attention the message deserves</param>
		public void Show(string text, NotificationSeverity severity)
		{
			if (xamlRoot == null)
			{
				return;
			}

			_ = ShowAsync(text, severity);
		}

		private async Task ShowAsync(string text, NotificationSeverity severity)
		{
			try
			{
				var dialog = new ContentDialog
				{
					XamlRoot = xamlRoot,
					Title = ProductInformation.ApplicationName,
					Content = text,
					CloseButtonText = "OK"
				};

				await dialog.ShowAsync();
			}
			catch (Exception)
			{
				// another dialog is already open, or the window is going away: not worth a crash
			}
		}
	}
}
