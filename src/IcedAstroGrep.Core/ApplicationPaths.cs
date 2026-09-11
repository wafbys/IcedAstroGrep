using System;
using System.IO;
using System.Reflection;

namespace IcedAstroGrep.Core
{
	/// <summary>
	/// Portable-only paths: all data lives beside the executable.
	/// </summary>
	public sealed class ApplicationPaths
	{
		private static string EntryDirectory
		{
			get
			{
				Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
				return Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
			}
		}

		public static string DataFolder => EntryDirectory;

		public static string CacheDirectory => Path.Combine(DataFolder, "Cache");

		public static string LogDirectory => Path.Combine(DataFolder, "Log");

		public static string LogFile => Path.Combine(LogDirectory, ProductInformation.ApplicationName + ".log");

		public static string LogArchiveFile => Path.Combine(LogDirectory, ProductInformation.ApplicationName + ".{#}.log");

		public static string ExecutionFolder => EntryDirectory;

		public static string ExecutingAssembly
		{
			get
			{
				Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
				return Path.GetFullPath(assembly.Location);
			}
		}

		/// <summary>
		/// Determines whether the given directory can be written to, creating it when necessary.
		/// </summary>
		/// <param name="directory">Directory to test</param>
		/// <param name="error">Description of the failure, empty when the directory is writable</param>
		/// <returns>true when a file could be created and removed again</returns>
		/// <remarks>
		/// The portable layout keeps settings, logs and the encoding cache beside the executable,
		/// which fails outright under Program Files or on read-only media. Callers use this to tell
		/// the user up front instead of letting every later write fail silently.
		/// </remarks>
		public static bool IsDirectoryWritable(string directory, out string error)
		{
			error = string.Empty;

			if (string.IsNullOrWhiteSpace(directory))
			{
				error = "no directory was given";
				return false;
			}

			string probe = Path.Combine(directory, "write-probe-" + Guid.NewGuid().ToString("N") + ".tmp");

			try
			{
				Directory.CreateDirectory(directory);
				File.WriteAllText(probe, string.Empty);
				return true;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				return false;
			}
			finally
			{
				try
				{
					if (File.Exists(probe))
					{
						File.Delete(probe);
					}
				}
				catch (Exception)
				{
					// the probe file is worthless; failing to remove it must not mask the result
				}
			}
		}
	}
}
