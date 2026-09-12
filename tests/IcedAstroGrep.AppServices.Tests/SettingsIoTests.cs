using System;
using System.IO;

using Xunit;

namespace IcedAstroGrep.AppServices.Tests
{
	/// <summary>
	/// A settings record shaped like the real ones: a public class with writable properties.
	/// </summary>
	public class SampleSettings
	{
		public string Name { get; set; }
		public int Count { get; set; }
		public bool Flag { get; set; }
	}

	/// <summary>
	/// Saving settings used to overwrite the document in place, so a crash or power loss could leave
	/// a truncated file that the next start discarded entirely. These pin the replacement contract.
	/// </summary>
	public class SettingsIoTests : IDisposable
	{
		private const string Version = "1.0.0";

		private readonly string _folder;
		private readonly string _path;

		public SettingsIoTests()
		{
			_folder = Path.Combine(Path.GetTempPath(), "IcedAstroGrepSettingsTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
			_path = Path.Combine(_folder, "Settings.xml");
		}

		public void Dispose()
		{
			try
			{
				if (Directory.Exists(_folder))
				{
					Directory.Delete(_folder, true);
				}
			}
			catch
			{
			}
		}

		private SampleSettings Save(string name, int count, bool flag)
		{
			var record = new SampleSettings { Name = name, Count = count, Flag = flag };
			Assert.True(SettingsIO.Save(record, _path, Version), "the settings could not be saved");
			return record;
		}

		private SampleSettings LoadFrom(string path)
		{
			var loaded = new SampleSettings();
			Assert.True(SettingsIO.Load(loaded, path, Version), "the settings could not be loaded from " + path);
			return loaded;
		}

		[Fact]
		public void SaveThenLoad_RoundTripsTheValues()
		{
			Save("hello", 7, true);

			var loaded = LoadFrom(_path);

			Assert.Equal("hello", loaded.Name);
			Assert.Equal(7, loaded.Count);
			Assert.True(loaded.Flag);
		}

		[Fact]
		public void Save_LeavesNoTemporaryFileBehind()
		{
			Save("hello", 1, false);
			Save("again", 2, false);

			Assert.False(File.Exists(_path + ".tmp"), "a temporary settings file was left behind");
			Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
		}

		[Fact]
		public void Save_KeepsThePreviousDocumentAsABackup()
		{
			Save("first", 1, false);
			Save("second", 2, false);

			string backup = _path + ".bak";
			Assert.True(File.Exists(backup), "no backup of the replaced settings was kept");

			var previous = LoadFrom(backup);
			Assert.Equal("first", previous.Name);
			Assert.Equal(1, previous.Count);

			// and the live file holds the newest values
			Assert.Equal("second", LoadFrom(_path).Name);
		}

		[Fact]
		public void Load_RecoversFromTheBackupWhenTheDocumentIsCorrupt()
		{
			Save("good", 42, true);
			Save("newer", 43, true);

			// simulate a truncated document, which is what an interrupted write used to leave behind
			File.WriteAllText(_path, "<SampleSettings version=\"1.0.0\"><property name=\"Name\"><value>trunc");

			var loaded = LoadFrom(_path);

			Assert.Equal("good", loaded.Name);
			Assert.Equal(42, loaded.Count);
		}

		[Fact]
		public void Load_ReturnsFalseWhenThereIsNoFile()
		{
			Assert.False(SettingsIO.Load(new SampleSettings(), _path, Version));
		}

		[Fact]
		public void Load_IgnoresADocumentWrittenByAnotherVersion()
		{
			Save("hello", 1, false);

			Assert.False(SettingsIO.Load(new SampleSettings(), _path, "9.9.9"));
		}

		[Fact]
		public void Save_CreatesTheContainingFolder()
		{
			string nested = Path.Combine(_folder, "nested", "deeper", "Settings.xml");

			Assert.True(SettingsIO.Save(new SampleSettings { Name = "nested" }, nested, Version));
			Assert.Equal("nested", LoadFrom(nested).Name);
		}
	}
}
