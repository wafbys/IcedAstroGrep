using System.Linq;
using System.Reflection;

using IcedAstroGrep.Output;
using IcedAstroGrep.Plugins.PDF;

using Xunit;

namespace IcedAstroGrep.Tests
{
	/// <summary>
	/// The seam the WinUI 3 shell will be built on: everything a second shell has to reuse lives in
	/// IcedAstroGrep.AppServices, and that assembly must stay free of every UI framework. These tests
	/// fail if the seam is crossed again.
	/// </summary>
	public class ShellBoundaryTests
	{
		[Fact]
		public void TheShellAgnosticAssemblyDoesNotReferenceAUIFramework()
		{
			var services = typeof(GeneralSettings).Assembly;

			Assert.Equal("IcedAstroGrep.AppServices", services.GetName().Name);

			string[] forbidden =
			{
				"System.Windows.Forms",
				"PresentationFramework",
				"PresentationCore",
				"WindowsBase",
				"System.Drawing.Common"
			};

			var referenced = services.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();

			foreach (string name in forbidden)
			{
				Assert.DoesNotContain(name, referenced);
			}
		}

		[Fact]
		public void TheWinFormsShellStillReferencesWinForms()
		{
			// the guard above only means something if the shell it was extracted out of really does
			// reference the framework the services assembly is meant to avoid
			var shell = typeof(IcedAstroGrep.Windows.Forms.frmMain).Assembly;

			Assert.Contains("System.Windows.Forms", shell.GetReferencedAssemblies().Select(assembly => assembly.Name));
		}

		[Fact]
		public void TheExporterTemplatesMovedWithTheExporter()
		{
			// HTMLHelper composes the resource name from the executing assembly's name, so this breaks
			// if the templates stay behind in the shell, or the root namespace stops matching it
			foreach (string template in new[] { "Output.html", "Output.css", "Output-fileNameOnly.html" })
			{
				Assert.False(string.IsNullOrWhiteSpace(HTMLHelper.GetContents(template)),
					template + " could not be read from the embedded resources of " + typeof(HTMLHelper).Assembly.GetName().Name);
			}
		}

		[Fact]
		public void TheLanguageResourcesStayedWithTheShell()
		{
			// Language.cs resolves "<executing assembly>.Language.<culture>.xml" against its own
			// assembly, so the text has to stay embedded in the shell that owns the wording
			var shell = typeof(IcedAstroGrep.Windows.Language).Assembly;

			Assert.Contains("IcedAstroGrep.Language.en-us.xml", shell.GetManifestResourceNames());
		}

		[Fact]
		public void ThePdfToolBinaryIsEmbeddedUnderTheNameThePluginLooksFor()
		{
			// the plug-in names the resource with a string, so the csproj and the code can drift apart
			var plugin = typeof(PDFPlugin);
			var field = plugin.GetField("PdfToTextResourceName", BindingFlags.NonPublic | BindingFlags.Static);

			Assert.NotNull(field);

			string name = (string)field.GetRawConstantValue();
			string[] embedded = plugin.Assembly.GetManifestResourceNames();

			Assert.Contains(name, embedded);
		}

		[Fact]
		public void TheEmbeddedPdfToolCanBeReadBackByThePlugin()
		{
			// the plug-in extracts these bytes to a temporary file at run time, so a name that resolves
			// is not enough: this calls the loader and checks that a real executable comes back
			var method = typeof(PDFPlugin).GetMethod("ReadEmbeddedPdfToText", BindingFlags.NonPublic | BindingFlags.Static);

			Assert.NotNull(method);

			byte[] contents = (byte[])method.Invoke(null, null);

			Assert.True(contents.Length > 100000, "the embedded pdftotext.exe looks truncated: " + contents.Length + " bytes");
			Assert.Equal(0x4D, contents[0]);
			Assert.Equal(0x5A, contents[1]);
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
			// itself and drop its System.Drawing.Common dependency without changing any output.
			var color = System.Drawing.Color.FromArgb(alpha, red, green, blue);
			string setting = Convertors.ConvertColorToString(color);

			Assert.Equal(System.Drawing.ColorTranslator.ToHtml(color), Convertors.ConvertColorSettingToHtml(setting));
		}
	}
}
