using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace IcedAstroGrep.Core.Tests
{
	/// <summary>
	/// Covers the Grep paths the engine review called out as untested: negation, context lines,
	/// file-names-only, the minimum hit count filter, exclusions and subfolder recursion.
	/// </summary>
	public class GrepSearchTests
	{
		private const string NewLine = "\r\n";

		[Fact]
		public void Negation_ReportsTheFileWithoutAMatch()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("with.txt", "the needle is here" + NewLine);
				folder.Write("without.txt", "nothing of interest" + NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					UseNegation = true
				});

				grep.Execute();

				Assert.Single(grep.MatchResults);
				Assert.Equal("without.txt", grep.MatchResults[0].File.Name);
			}
		}

		[Fact]
		public void ContextLines_AreIncludedAroundAHit()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("context.txt", "before the hit" + NewLine + "the needle is here" + NewLine + "after the hit" + NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					ContextLines = 1
				});

				grep.Execute();

				Assert.Single(grep.MatchResults);
				var lines = grep.MatchResults[0].Matches;

				Assert.Contains(lines, line => line.HasMatch && line.Line.Contains("needle"));
				Assert.Contains(lines, line => !line.HasMatch && line.Line.Contains("before the hit"));
				Assert.Contains(lines, line => !line.HasMatch && line.Line.Contains("after the hit"));
			}
		}

		[Fact]
		public void ContextLines_AreOmittedWhenNotRequested()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("context.txt", "before the hit" + NewLine + "the needle is here" + NewLine + "after the hit" + NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle"
				});

				grep.Execute();

				Assert.Single(grep.MatchResults);
				Assert.All(grep.MatchResults[0].Matches, line => Assert.True(line.HasMatch));
			}
		}

		[Fact]
		public void ReturnOnlyFileNames_KeepsTheFileHitWithoutLineDetails()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("names.txt", "the needle is here" + NewLine + "another needle" + NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					ReturnOnlyFileNames = true
				});

				grep.Execute();

				Assert.Single(grep.MatchResults);
				Assert.Empty(grep.MatchResults[0].Matches);
				Assert.Equal(1, grep.MatchResults[0].HitCount);
			}
		}

		[Fact]
		public void MinimumHitCount_FiltersOutFilesBelowTheThreshold()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("one.txt", "the needle is here" + NewLine);
				folder.Write("two.txt", "the needle is here" + NewLine + "another needle" + NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					FilterItems = new List<FilterItem>
					{
						new FilterItem(new FilterType(FilterType.Categories.File, FilterType.SubCategories.MinimumHitCount), "2", FilterType.ValueOptions.None, false, true)
					}
				});

				var filtered = new List<string>();
				grep.FileFiltered += (file, item, value) => filtered.Add(file.Name);

				grep.Execute();

				Assert.Single(grep.MatchResults);
				Assert.Equal("two.txt", grep.MatchResults[0].File.Name);
				Assert.Contains("one.txt", filtered);
			}
		}

		[Fact]
		public void ExtensionExclusion_SkipsTheFile()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("keep.txt", "the needle is here" + NewLine);
				folder.Write("skip.log", "the needle is here too" + NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					FileFilter = "*",
					FilterItems = new List<FilterItem>
					{
						new FilterItem(new FilterType(FilterType.Categories.File, FilterType.SubCategories.Extension), ".log", FilterType.ValueOptions.None, false, true)
					}
				});

				var filtered = new List<string>();
				grep.FileFiltered += (file, item, value) => filtered.Add(file.Name);

				grep.Execute();

				Assert.Single(grep.MatchResults);
				Assert.Equal("keep.txt", grep.MatchResults[0].File.Name);
				Assert.Contains("skip.log", filtered);
			}
		}

		[Fact]
		public void NameExclusion_SkipsTheFile()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("keep.txt", "the needle is here" + NewLine);
				folder.Write("generated-skip.txt", "the needle is here too" + NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					FilterItems = new List<FilterItem>
					{
						new FilterItem(new FilterType(FilterType.Categories.File, FilterType.SubCategories.Name), "generated-", FilterType.ValueOptions.StartsWith, false, true)
					}
				});

				grep.Execute();

				Assert.Single(grep.MatchResults);
				Assert.Equal("keep.txt", grep.MatchResults[0].File.Name);
			}
		}

		[Fact]
		public void SearchInSubfolders_ControlsRecursion()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("top.txt", "the needle is here" + NewLine);
				folder.Write(@"nested\deep.txt", "the needle is here too" + NewLine);

				var shallow = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					SearchInSubfolders = false
				});
				shallow.Execute();

				var deep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					SearchInSubfolders = true
				});
				deep.Execute();

				Assert.Equal(new[] { "top.txt" }, shallow.MatchResults.Select(m => m.File.Name).ToArray());
				Assert.Equal(new[] { "deep.txt", "top.txt" }, deep.MatchResults.Select(m => m.File.Name).OrderBy(n => n).ToArray());
			}
		}

		[Fact]
		public void DirectoryExclusion_SkipsTheDirectory()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("top.txt", "the needle is here" + NewLine);
				folder.Write(@"skipped\deep.txt", "the needle is here too" + NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					SearchInSubfolders = true,
					FilterItems = new List<FilterItem>
					{
						new FilterItem(new FilterType(FilterType.Categories.Directory, FilterType.SubCategories.Name), "skipped", FilterType.ValueOptions.Equals, false, true)
					}
				});

				var filtered = new List<string>();
				grep.DirectoryFiltered += (dir, item, value) => filtered.Add(dir.Name);

				grep.Execute();

				Assert.Single(grep.MatchResults);
				Assert.Equal("top.txt", grep.MatchResults[0].File.Name);
				Assert.Contains("skipped", filtered);
			}
		}
	}
}
