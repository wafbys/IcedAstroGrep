using System;
using System.IO;
using System.Text;
using System.Threading;

using Xunit;

namespace IcedAstroGrep.Core.Tests
{
	/// <summary>
	/// Starting a new search must stop the previous one, otherwise the two race over the shared
	/// plug-in instances and the on-disk encoding cache.
	/// </summary>
	public class GrepAbortTests
	{
		[Fact]
		public void AbortAndWait_ReturnsTrueWhenNoSearchWasStarted()
		{
			var grep = new Grep(new TestSearchSpec { StartDirectories = new string[0], SearchText = "x" });

			Assert.True(grep.AbortAndWait(TimeSpan.FromSeconds(5)));
		}

		[Fact]
		public void AbortAndWait_StopsARunningSearchAndJoinsItsThread()
		{
			using (var folder = new TempFolder())
			{
				var content = new StringBuilder();
				for (int i = 0; i < 3000; i++)
				{
					content.AppendLine("line " + i + " with the needle in it");
				}

				for (int i = 0; i < 200; i++)
				{
					folder.Write("file" + i + ".txt", content.ToString());
				}

				var grep = new Grep(new TestSearchSpec { StartDirectories = new[] { folder.Path }, SearchText = "needle" });

				bool completed = false;
				bool cancelled = false;
				var finished = new ManualResetEventSlim(false);

				grep.SearchComplete += () => { completed = true; finished.Set(); };
				grep.SearchCancel += () => { cancelled = true; finished.Set(); };

				grep.BeginExecute();

				// let the search actually get going so a running search is aborted, not a pending thread
				Thread.Sleep(300);

				Assert.True(grep.AbortAndWait(TimeSpan.FromSeconds(30)), "the search thread was not joined");
				Assert.True(finished.Wait(TimeSpan.FromSeconds(5)), "the search did not report completion or cancellation");
				Assert.True(cancelled, "the in-flight search was not cancelled");
				Assert.False(completed, "the search completed instead of being cancelled");
			}
		}
	}
}
