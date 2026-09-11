using System;
using System.IO;

using IcedAstroGrep.Core;

using Xunit;

namespace IcedAstroGrep.Core.Tests
{
	/// <summary>
	/// The portable layout writes beside the executable, which is not always allowed; the probe that
	/// reports this has to be right, because a false "writable" brings back the silent failures.
	/// </summary>
	public class ApplicationPathsTests
	{
		[Fact]
		public void IsDirectoryWritable_IsTrueForAWritableFolder()
		{
			string folder = Path.Combine(Path.GetTempPath(), "IcedAstroGrepPathsTests", Guid.NewGuid().ToString("N"));

			string error;
			try
			{
				Assert.True(ApplicationPaths.IsDirectoryWritable(folder, out error), "unexpected failure: " + error);
				Assert.Equal(string.Empty, error);

				// the folder is created on demand and the probe does not linger
				Assert.True(Directory.Exists(folder));
				Assert.Empty(Directory.GetFiles(folder));
			}
			finally
			{
				try
				{
					if (Directory.Exists(folder))
					{
						Directory.Delete(folder, true);
					}
				}
				catch
				{
				}
			}
		}

		[Fact]
		public void IsDirectoryWritable_IsFalseWhenThePathIsAFile()
		{
			using (var folder = new TempFolder())
			{
				string file = folder.Write("not-a-folder.txt", "x");

				string error;
				Assert.False(ApplicationPaths.IsDirectoryWritable(file, out error), "a file path must not be reported as a writable folder");
				Assert.False(string.IsNullOrWhiteSpace(error));
			}
		}

		[Fact]
		public void IsDirectoryWritable_ExplainsAnEmptyPath()
		{
			string error;
			Assert.False(ApplicationPaths.IsDirectoryWritable("  ", out error));
			Assert.False(string.IsNullOrWhiteSpace(error));
		}
	}
}
