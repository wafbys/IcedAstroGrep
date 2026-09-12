using System.Linq;

using Xunit;

namespace IcedAstroGrep.WinForms.Tests
{
	/// <summary>
	/// The WinForms side of the shell boundary: what has to stay true of this particular shell, and the
	/// facts that make the AppServices guards meaningful.
	/// </summary>
	public class ShellBoundaryTests
	{
		[Fact]
		public void TheWinFormsShellStillReferencesWinForms()
		{
			// the AppServices guard only means something if the shell it was extracted out of really does
			// reference the framework the services assembly is meant to avoid
			var shell = typeof(IcedAstroGrep.Windows.Forms.frmMain).Assembly;

			Assert.Contains("System.Windows.Forms", shell.GetReferencedAssemblies().Select(assembly => assembly.Name));
		}

		[Fact]
		public void TheLanguageResourcesStayedWithTheShell()
		{
			// Language.cs resolves "<executing assembly>.Language.<culture>.xml" against its own
			// assembly, so the text has to stay embedded in the shell that owns the wording
			var shell = typeof(IcedAstroGrep.Windows.Language).Assembly;

			Assert.Contains("IcedAstroGrep.Language.en-us.xml", shell.GetManifestResourceNames());
		}

		[Theory]
		[InlineData(0, 0, 0, 255)]
		[InlineData(255, 255, 255, 255)]
		[InlineData(255, 0, 0, 255)]
		[InlineData(0, 128, 0, 255)]
		[InlineData(240, 240, 240, 255)]
		[InlineData(255, 255, 225, 255)]
		[InlineData(1, 2, 3, 0)]
		public void TheHtmlColourTextMatchesWhatColorTranslatorReturned(int red, int green, int blue, int alpha)
		{
			// ConvertStringToColor always builds its colour with Color.FromArgb, which never sets the
			// known-colour bit, so ColorTranslator.ToHtml always took its "#RRGGBB" branch rather than
			// returning a colour name. That is what lets the shell-agnostic assembly format the text
			// itself and drop its System.Drawing.Common dependency without changing any output. The
			// comparison needs System.Drawing.Common, which is why it runs here and not in AppServices.
			var color = System.Drawing.Color.FromArgb(alpha, red, green, blue);
			string setting = Convertors.ConvertColorToString(color);

			Assert.Equal(System.Drawing.ColorTranslator.ToHtml(color), Convertors.ConvertColorSettingToHtml(setting));
		}
	}
}
