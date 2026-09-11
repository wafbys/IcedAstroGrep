using System;
using System.IO;

namespace IcedAstroGrep.Core.Tests
{
	/// <summary>
	/// A disposable temporary folder for tests that need real files on disk.
	/// </summary>
	internal sealed class TempFolder : IDisposable
	{
		public TempFolder()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "IcedAstroGrepTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path);
		}

		public string Path { get; }

		/// <summary>
		/// Writes a file below the folder, creating any intermediate directories, and returns its
		/// full path.
		/// </summary>
		public string Write(string relativePath, string content)
		{
			string full = System.IO.Path.Combine(Path, relativePath);
			string directory = System.IO.Path.GetDirectoryName(full);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllText(full, content);
			return full;
		}

		public void Dispose()
		{
			try
			{
				if (Directory.Exists(Path))
				{
					Directory.Delete(Path, true);
				}
			}
			catch
			{
			}
		}
	}
}
