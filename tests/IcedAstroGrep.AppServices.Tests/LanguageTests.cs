using System.Linq;
using System.Xml;

using Xunit;

namespace IcedAstroGrep.AppServices.Tests
{
	/// <summary>
	/// The language text is part of the shell-agnostic services now, so any shell can show the same
	/// wording. These tests prove the files are embedded where the lookup looks for them, that a loaded
	/// language resolves, and that the document can be walked by form and control name.
	/// </summary>
	public class LanguageTests
	{
		[Fact]
		public void TheLanguageFilesAreEmbeddedWhereTheLookupFindsThem()
		{
			var assembly = typeof(Language).Assembly;

			Assert.Equal("IcedAstroGrep.AppServices", assembly.GetName().Name);

			// Language.cs composes the resource name as "<assembly name>.Language.<culture>.xml"
			Assert.Contains("IcedAstroGrep.AppServices.Language.en-us.xml", assembly.GetManifestResourceNames());
		}

		[Fact]
		public void LoadingALanguageResolvesKnownKeys()
		{
			Language.Load("en-us");

			Assert.Contains("cannot be written to", Language.GetGenericText("ApplicationFolderNotWritable"));
		}

		[Fact]
		public void AnUnknownKeyFallsBackToTheSuppliedDefault()
		{
			Language.Load("en-us");

			Assert.Equal("fallback", Language.GetGenericText("NoSuchKeyAnywhere", "fallback"));
		}

		[Fact]
		public void AScreenAndControlCanBeLookedUpInTheLoadedDocument()
		{
			Language.Load("en-us");

			// this is the path shape the WinForms localizer walks, which is why Language exposes TextRoot
			XmlNode screen = Language.TextRoot.SelectSingleNode("screen[@name='frmMain']");

			Assert.NotNull(screen);

			XmlNode control = screen.SelectSingleNode("control[@name='lblSearchText']");

			Assert.NotNull(control);
			Assert.Equal("Search Text", control.Attributes["value"].Value);
		}

		[Fact]
		public void TheShippedLanguagesAreOfferedForSelection()
		{
			var cultures = Language.AvailableLanguages.Select(item => item.Culture).ToList();

			Assert.Contains("en-us", cultures);
			Assert.Contains("de-de", cultures);
			Assert.Contains("pl-pl", cultures);
		}
	}
}
