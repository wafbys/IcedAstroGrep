using System;
using System.IO;

using IFilterTextReader;
using IFilterTextReader.Exceptions;

using Xunit;
using Xunit.Abstractions;

namespace IcedAstroGrep.AppServices.Tests
{
	/// <summary>
	/// Exercises the iFilter path against a real COM filter installed on the machine. That path is
	/// where the IPersistStream loading, the stream lifetime and the single character Read live, and
	/// none of it is covered by the Core tests.
	/// </summary>
	/// <remarks>
	/// A machine without an iFilter registered for the extension has nothing to integrate with, so
	/// the test reports that and succeeds rather than failing a runner that simply lacks Office or
	/// the indexing filter.
	/// </remarks>
	public class IFilterIntegrationTests
	{
		private readonly ITestOutputHelper _output;

		public IFilterIntegrationTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void ARealFileIsReadThroughTheInstalledIFilter()
		{
			string folder = Path.Combine(Path.GetTempPath(), "IcedAstroGrepIFilterTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);

			try
			{
				string path = Path.Combine(folder, "sample.txt");
				File.WriteAllText(path, "alpha beta" + Environment.NewLine + "gamma delta" + Environment.NewLine);

				string text;
				try
				{
					using (var reader = new FilterReader(path))
					{
						text = reader.ReadToEnd();
					}
				}
				catch (IFFilterNotFound ex)
				{
					_output.WriteLine("Skipped: no iFilter is registered for .txt on this machine ({0})", ex.Message);
					return;
				}

				_output.WriteLine("iFilter returned: {0}", text.Replace("\r", "\\r").Replace("\n", "\\n"));
				Assert.Contains("alpha beta", text);
				Assert.Contains("gamma delta", text);

				// Read() used to throw on every call because it passed a zero length buffer; it must
				// now hand back the first character the filter produces (the chunk type separator).
				using (var reader = new FilterReader(path))
				{
					int first = reader.Read();
					Assert.NotEqual(-1, first);
				}
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
	}
}
