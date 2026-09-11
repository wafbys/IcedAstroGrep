using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Xunit;

namespace IcedAstroGrep.Core.Tests
{
	/// <summary>
	/// The regex timeout is the only protection against a pattern that never finishes matching, so
	/// both ways of observing it are pinned here: the synchronous exception and the async event.
	/// </summary>
	public class GrepRegexTimeoutTests
	{
		/// <summary>Input that makes a catastrophic backtracking pattern explode.</summary>
		private const string PathologicalInput = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!";

		[Fact]
		public void BuildSearchRegEx_CarriesAMatchTimeout()
		{
			var regex = Grep.BuildSearchRegEx(new TestSearchSpec { SearchText = "f.o", UseRegularExpressions = true });

			Assert.NotNull(regex);
			Assert.Equal(Grep.SearchRegExTimeout, regex.MatchTimeout);
		}

		[Fact]
		public void CatastrophicPattern_AbortsTheSynchronousSearch()
		{
			using (var folder = new TempFolder())
			{
				string path = folder.Write("pathological.txt", PathologicalInput + Environment.NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartFilePaths = new[] { path },
					SearchText = "^(a+)+$",
					UseRegularExpressions = true
				});

				var stopwatch = Stopwatch.StartNew();
				Assert.Throws<SearchRegexTimeoutException>(() => grep.Execute());
				stopwatch.Stop();

				// without the timeout this match never returns
				Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), "the search took " + stopwatch.Elapsed.TotalSeconds.ToString("0.0") + " s");
			}
		}

		[Fact]
		public void CatastrophicPattern_IsReportedThroughTheAsyncSearchEvents()
		{
			using (var folder = new TempFolder())
			{
				string path = folder.Write("pathological.txt", PathologicalInput + Environment.NewLine);

				var grep = new Grep(new TestSearchSpec
				{
					StartFilePaths = new[] { path },
					SearchText = "(a+)+$",
					UseRegularExpressions = true
				});

				Exception reported = null;
				var finished = new ManualResetEventSlim(false);

				grep.SearchError += (file, ex) => reported = ex;
				grep.SearchCancel += () => finished.Set();
				grep.SearchComplete += () => finished.Set();

				grep.BeginExecute();

				Assert.True(finished.Wait(TimeSpan.FromSeconds(60)), "the async search did not settle");
				Assert.IsType<SearchRegexTimeoutException>(reported);
			}
		}
	}
}
