using System;
using System.IO;
using Xunit;

namespace IcedAstroGrep.Core.Tests
{
	public class GrepTests : IDisposable
	{
		private readonly string _dir;

		public GrepTests()
		{
			_dir = Path.Combine(Path.GetTempPath(), "IcedAstroGrepTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_dir);
			File.WriteAllText(Path.Combine(_dir, "sample.txt"), "hello world" + Environment.NewLine + "foo bar hello" + Environment.NewLine + "nothing here");
		}

		public void Dispose()
		{
			try
			{
				if (Directory.Exists(_dir))
				{
					Directory.Delete(_dir, true);
				}
			}
			catch
			{
			}
		}

		[Fact]
		public void LiteralSearch_FindsMatchingLines()
		{
			var grep = new Grep(new TestSearchSpec
			{
				StartDirectories = new[] { _dir },
				SearchText = "hello"
			});

			grep.Execute();

			Assert.Single(grep.MatchResults);
			Assert.Equal(2, grep.MatchResults[0].HitCount);
		}

		[Fact]
		public void CaseSensitiveSearch_IgnoresDifferentCase()
		{
			var grep = new Grep(new TestSearchSpec
			{
				StartDirectories = new[] { _dir },
				SearchText = "HELLO",
				UseCaseSensitivity = true
			});

			grep.Execute();

			Assert.Empty(grep.MatchResults);
		}

		[Fact]
		public void WholeWordSearch_SkipsEmbeddedMatch()
		{
			File.WriteAllText(Path.Combine(_dir, "words.txt"), "cat" + Environment.NewLine + "concatenate");
			var grep = new Grep(new TestSearchSpec
			{
				StartFilePaths = new[] { Path.Combine(_dir, "words.txt") },
				SearchText = "cat",
				UseWholeWordMatching = true
			});

			grep.Execute();

			Assert.Single(grep.MatchResults);
			Assert.Equal(1, grep.MatchResults[0].HitCount);
		}

		[Fact]
		public void RegexSearch_FindsPattern()
		{
			var grep = new Grep(new TestSearchSpec
			{
				StartDirectories = new[] { _dir },
				SearchText = "f.o",
				UseRegularExpressions = true
			});

			grep.Execute();

			Assert.Single(grep.MatchResults);
		}
	}
}
