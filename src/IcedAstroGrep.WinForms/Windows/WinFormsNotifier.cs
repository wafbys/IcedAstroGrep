using System.Windows.Forms;

using IcedAstroGrep.Core;

namespace IcedAstroGrep.Windows
{
	/// <summary>
	/// Shows a message from the engine with the WinForms message box.
	/// </summary>
	/// <remarks>
	/// This is the entire shell side of <see cref="IUserNotifier"/>: the engine names a language key,
	/// the shell turns it into the localized text and picks the icon. The caption is still
	/// <see cref="ProductInformation.ApplicationName"/>, and both error texts still use
	/// <c>string.Format</c> with the arguments the engine passes, so the messages a user sees are
	/// unchanged by the seam.
	/// </remarks>
	public sealed class WinFormsNotifier : IUserNotifier
	{
		/// <summary>
		/// The instance a WinForms shell passes to the engine. Stateless, so one is enough.
		/// </summary>
		public static readonly WinFormsNotifier Instance = new WinFormsNotifier();

		/// <summary>
		/// Shows the generic text named by <paramref name="languageKey"/>.
		/// </summary>
		/// <param name="languageKey">Key of the generic text to show.</param>
		/// <param name="formatArguments">Arguments for the text's placeholders, or null when it takes none.</param>
		/// <param name="severity">How much attention the message deserves.</param>
		public void Notify(string languageKey, object[] formatArguments, NotificationSeverity severity)
		{
			string text = Language.GetGenericText(languageKey);

			if (formatArguments != null && formatArguments.Length > 0)
			{
				text = string.Format(text, formatArguments);
			}

			MessageBox.Show(text, ProductInformation.ApplicationName, MessageBoxButtons.OK,
				severity == NotificationSeverity.Warning ? MessageBoxIcon.Exclamation : MessageBoxIcon.Information);
		}
	}
}
