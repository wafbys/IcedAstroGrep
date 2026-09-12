using System;
using System.IO;
using System.Linq;

using IcedAstroGrep.Core;
using IcedAstroGrep.Core.EncodingDetection;
using IcedAstroGrep.Display;

using Xunit;

namespace IcedAstroGrep.AppServices.Tests
{
	/// <summary>
	/// The results pane had no tests at all while it was a hundred lines of widget calls inside frmMain.
	/// These pin the composition down: what a document looks like, where every line came from, and that
	/// the text has no trailing newline.
	/// </summary>
	public class ResultDocumentTests : IDisposable
	{
		private const string Hit = "needle";

		private readonly string folder;

		public ResultDocumentTests()
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
				// a leftover temp folder is not worth failing a test over
			}
		}

		[Fact]
		public void TheDocumentIsThePathABlankLineAndTheHitLineWithItsSourcePosition()
		{
			string path = Write("one.txt", "first line before", "    indented line with needle here", "third line after", "fourth line");

			ResultDocument document = Build();

			Assert.Equal(3, document.Lines.Count);

			Assert.Equal(ResultLineKind.FileName, document.Lines[0].Kind);
			Assert.Equal(path, document.Lines[0].Text);
			Assert.Equal(path, document.Lines[0].SourceFile);
			Assert.Equal(-1, document.Lines[0].SourceLineNumber);

			Assert.Equal(ResultLineKind.Separator, document.Lines[1].Kind);

			ResultDocumentLine hit = document.Lines[2];
			Assert.Equal(ResultLineKind.Content, hit.Kind);
			Assert.Equal("    indented line with needle here", hit.Text);
			Assert.Equal(2, hit.SourceLineNumber);
			Assert.Equal(path, hit.SourceFile);
			Assert.True(hit.HasMatch);
			Assert.Equal(24, hit.ColumnNumber);   // 1 based column of the hit
		}

		[Fact]
		public void TheTextIsTheLinesJoinedWithoutATrailingNewline()
		{
			string path = Write("one.txt", "first line before", "    indented line with needle here", "third line after", "fourth line");

			ResultDocument document = Build();

			Assert.Equal(path + Environment.NewLine + Environment.NewLine + "    indented line with needle here", document.Text);
			Assert.False(document.Text.EndsWith(Environment.NewLine));
		}

		[Fact]
		public void ContextLinesAreIncludedAndMarkedAsContext()
		{
			Write("one.txt", "first line before", "    indented line with needle here", "third line after", "fourth line");

			// the search has to capture the context; the display options then select from what it captured
			ResultDocument document = Build(searchContextLines: 2, beforeContextLines: 1, afterContextLines: 1);

			// path, blank, line 1, line 2 (hit), line 3
			Assert.Equal(5, document.Lines.Count);
			Assert.Equal(1, document.Lines[2].SourceLineNumber);
			Assert.False(document.Lines[2].HasMatch);
			Assert.True(document.Lines[3].HasMatch);
			Assert.Equal(3, document.Lines[4].SourceLineNumber);
			Assert.False(document.Lines[4].HasMatch);
		}

		[Fact]
		public void TwoFilesAreSeparatedByTwoBlankLines()
		{
			Write("one.txt", "one has the needle");
			Write("two.txt", "two has the needle");

			ResultDocument document = Build();

			Assert.Equal(
				new[]
				{
					ResultLineKind.FileName, ResultLineKind.Separator, ResultLineKind.Content,
					ResultLineKind.Separator, ResultLineKind.Separator,
					ResultLineKind.FileName, ResultLineKind.Separator, ResultLineKind.Content
				},
				document.Lines.Select(line => line.Kind).ToArray());
		}

		[Fact]
		public void RemovingLeadingWhiteSpaceKeepsTheIndentUpToTheHitButTrimsContext()
		{
			Write("indent.txt", "\t\tindented context", "\t\t\tneedle here");

			ResultDocument document = Build(searchContextLines: 2, removeWhiteSpace: true, beforeContextLines: 1);

			// path, blank, context line, hit line: the context line is trimmed, the hit line keeps the
			// indentation that precedes the hit
			Assert.Equal("indented context", document.Lines[2].Text);
			Assert.False(document.Lines[2].HasMatch);
			Assert.Equal("needle here", document.Lines[3].Text);
			Assert.True(document.Lines[3].HasMatch);
		}

		[Fact]
		public void NothingToShowProducesAnEmptyDocument()
		{
			Write("one.txt", "nothing of interest here");

			ResultDocument document = Build();

			Assert.Empty(document.Lines);
			Assert.Equal(string.Empty, document.Text);
		}

		private ResultDocument Build(bool removeWhiteSpace = false, int beforeContextLines = 0, int afterContextLines = 0, int searchContextLines = 0)
		{
			var spec = new SearchInterfaces.SearchSpec
			{
				StartDirectories = new[] { folder },
				SearchText = Hit,
				FileFilter = "*.txt",
				ContextLines = searchContextLines,
				EncodingDetectionOptions = new EncodingOptions(false) { UseEncodingCache = false }
			};

			var grep = new Grep(spec);
			grep.Execute();

			return ResultDocument.Build(grep.MatchResults, new ResultDocumentOptions
			{
				RemoveLeadingWhiteSpace = removeWhiteSpace,
				BeforeContextLines = beforeContextLines,
				AfterContextLines = afterContextLines
			});
		}

		private string Write(string name, params string[] lines)
		{
			string path = Path.Combine(folder, name);
			File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");
			return path;
		}
	}
}
