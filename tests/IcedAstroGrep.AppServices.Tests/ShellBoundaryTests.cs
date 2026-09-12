using System.Linq;
using System.Reflection;

using IcedAstroGrep.Output;
using IcedAstroGrep.Plugins.PDF;

using Xunit;

namespace IcedAstroGrep.AppServices.Tests
{
	/// <summary>
	/// The seam a second shell is built on: everything a shell has to reuse lives in
	/// IcedAstroGrep.AppServices, and that assembly must stay free of every UI framework. These tests
	/// run without a UI framework themselves, which is exactly the property they are about.
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
		public void TheExporterTemplatesMovedWithTheExporter()
		{
			// HTMLHelper composes the resource name from the executing assembly's name, so this breaks
			// if the templates stay behind in a shell, or the root namespace stops matching it
			foreach (string template in new[] { "Output.html", "Output.css", "Output-fileNameOnly.html" })
			{
				Assert.False(string.IsNullOrWhiteSpace(HTMLHelper.GetContents(template)),
					template + " could not be read from the embedded resources of " + typeof(HTMLHelper).Assembly.GetName().Name);
			}
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
	}
}
