using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Xunit;
using Xunit.Abstractions;

namespace IcedAstroGrep.Core.Tests
{
	/// <summary>
	/// Covers how a search walks the file system and what it does with a specification it cannot honour.
	/// Both used to fail as something worse than a wrong result: a recursive walk that never ended, and
	/// a context buffer sized from whatever the caller asked for.
	/// </summary>
	public class GrepTraversalTests
	{
		private readonly ITestOutputHelper _output;

		public GrepTraversalTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void AContextLineCountOutsideTheAllowedRangeIsRejectedBeforeAnyFileIsRead()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("a.txt", "needle");

				foreach (int contextLines in new[] { -1, Grep.MaxContextLines + 1 })
				{
					var grep = new Grep(new TestSearchSpec
					{
						StartDirectories = new[] { folder.Path },
						SearchText = "needle",
						ContextLines = contextLines
					});

					var error = Assert.Throws<ArgumentOutOfRangeException>(() => grep.Execute());

					Assert.Equal("ISearchSpec.ContextLines", error.ParamName);
				}
			}
		}

		[Fact]
		public void AContextLineCountAtTheLimitIsStillAccepted()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("a.txt", "needle");

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path },
					SearchText = "needle",
					ContextLines = Grep.MaxContextLines
				});

				grep.Execute();

				Assert.Single(grep.MatchResults);
			}
		}

		[Fact]
		public void ADirectoryLoopDoesNotRecurseForever()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("root.txt", "needle");
				folder.Write("sub/child.txt", "needle");

				string loop = Path.Combine(folder.Path, "sub", "loop");

				// a junction pointing back at its own ancestor is the classic way to make a recursive walk
				// never end; junctions do not need elevation, but policy can still refuse one
				if (!TryCreateJunction(loop, folder.Path))
				{
					_output.WriteLine("Skipped: this machine would not create a junction without elevation.");
					return;
				}

				try
				{
					var grep = new Grep(new TestSearchSpec
					{
						StartDirectories = new[] { folder.Path },
						SearchText = "needle",
						SearchInSubfolders = true
					});

					grep.Execute();

					Assert.Equal(2, grep.MatchResults.Count);
				}
				finally
				{
					// remove the link first, so the temporary folder can be deleted afterwards
					Directory.Delete(loop);
				}
			}
		}

		[Fact]
		public void AStartDirectoryInsideAnotherIsOnlyWalkedOnce()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("root.txt", "needle");
				folder.Write("sub/child.txt", "needle");

				var grep = new Grep(new TestSearchSpec
				{
					StartDirectories = new[] { folder.Path, Path.Combine(folder.Path, "sub") },
					SearchText = "needle",
					SearchInSubfolders = true
				});

				grep.Execute();

				// child.txt is reachable from both roots; the second walk must not report it again
				Assert.Equal(2, grep.MatchResults.Count);
				Assert.Single(grep.MatchResults, result => result.File.Name == "child.txt");
			}
		}

		/// <summary>
		/// Creates a directory junction, which (unlike a symbolic link) does not require elevation.
		/// </summary>
		/// <param name="linkPath">Path of the junction to create</param>
		/// <param name="targetPath">Directory the junction points at</param>
		/// <returns>true when the junction exists afterwards</returns>
		private static bool TryCreateJunction(string linkPath, string targetPath)
		{
			try
			{
				var startInfo = new ProcessStartInfo("cmd.exe", string.Format("/c mklink /J \"{0}\" \"{1}\"", linkPath, targetPath))
				{
					CreateNoWindow = true,
					UseShellExecute = false
				};

				using (Process process = Process.Start(startInfo))
				{
					process.WaitForExit();

					return process.ExitCode == 0 && Directory.Exists(linkPath);
				}
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
