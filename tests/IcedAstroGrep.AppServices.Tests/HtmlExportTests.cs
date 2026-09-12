using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using IcedAstroGrep.Core;
using IcedAstroGrep.Core.EncodingDetection;
using IcedAstroGrep.Output;

using Xunit;

namespace IcedAstroGrep.AppServices.Tests
{
	/// <summary>
	/// The results pane in the WinUI shell shows the *export* markup, so the two must not drift apart.
	/// These tests pin the string the exporters build, and that the source location attributes a viewer
	/// needs stay out of an export.
	/// </summary>
	public class HtmlExportTests : IDisposable
	{
		private readonly string folder;

		public HtmlExportTests()
		{
			folder = Path.Combine(Path.GetTempPath(), "IcedAstroGrepTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(folder, true);
			}
			catch
			{
			}
		}

		[Fact]
		public void TheDocumentCanBeBuiltWithoutWritingAFile()
		{
			string path = Write("one.txt", "first line before", "    indented line with needle here", "third line after");

			string html = MatchResultsExport.BuildResultsAsHTML(CreateSettings());

			Assert.Contains("<title>IcedAstroGrep Results</title>", html);
			Assert.Contains(path, html);
			Assert.Contains("<span class=\"searchtext\">needle</span>", html);
		}

		[Fact]
		public void TheBuiltDocumentIsExactlyWhatTheFileExportWrites()
		{
			Write("one.txt", "first line before", "    indented line with needle here", "third line after");

			MatchResultsExportSettings settings = CreateSettings();
			settings.Path = Path.Combine(folder, "export.html");

			MatchResultsExport.SaveResultsAsHTML(settings);

			// the file and the string are the same document, byte for byte (the writer's own newline is
			// part of both), which is what keeps the shell's pane and an export from drifting apart
			Assert.Equal(MatchResultsExport.BuildResultsAsHTML(settings), File.ReadAllText(settings.Path));
		}

		[Fact]
		public void SourceLocationsStayOutOfAnExport()
		{
			Write("one.txt", "a line with needle");

			string html = MatchResultsExport.BuildResultsAsHTML(CreateSettings());

			Assert.DoesNotContain("data-file=", html);
			Assert.DoesNotContain("srcline", html);
		}

		[Fact]
		public void AViewerCanAskForTheSourceOfEveryLine()
		{
			string path = Write("one.txt", "first line before", "    indented line with needle here", "third line after");

			MatchResultsExportSettings settings = CreateSettings();
			settings.IncludeSourceLocations = true;

			string html = MatchResultsExport.BuildResultsAsHTML(settings);

			Assert.Contains("class=\"srcline\"", html);
			Assert.Contains("data-file=\"" + System.Net.WebUtility.HtmlEncode(path) + "\"", html);
			Assert.Contains("data-line=\"2\"", html);
			Assert.Contains("data-column=\"24\"", html);

			// the highlighting is unchanged by the wrapper
			Assert.Contains("<span class=\"searchtext\">needle</span>", html);
		}

		private MatchResultsExportSettings CreateSettings()
		{
			var spec = new SearchInterfaces.SearchSpec
			{
				StartDirectories = new[] { folder },
				SearchText = "needle",
				FileFilter = "*.txt",
				EncodingDetectionOptions = new EncodingOptions(false) { UseEncodingCache = false }
			};

			var grep = new Grep(spec);
			grep.Execute();

			return new MatchResultsExportSettings
			{
				Grep = grep,
				GrepIndexes = Enumerable.Range(0, grep.MatchResults.Count).ToList(),
				ShowLineNumbers = true,
				ContextLinesBefore = 1,
				ContextLinesAfter = 1
			};
		}

		private string Write(string name, params string[] lines)
		{
			string path = Path.Combine(folder, name);
			File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");
			return path;
		}
	}
}
