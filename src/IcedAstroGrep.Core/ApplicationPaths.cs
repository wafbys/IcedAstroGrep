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
	}
}
