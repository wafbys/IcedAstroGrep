using System;
using System.Collections.Generic;
using System.IO;

using IcedAstroGrep.Core.Plugin;

using Xunit;

namespace IcedAstroGrep.Core.Tests
{
	/// <summary>
	/// A plug-in that always fails but asks to be skipped.
	/// </summary>
	internal sealed class FailingPlugin : IIcedAstroGrepPlugin
	{
		public string Author => "test";
		public string Description => "always fails";
		public string Extensions => string.Empty;
		public bool IsAvailable => true;
		public bool IsFileSkipped { get; private set; }
		public string Name => "Failing";
		public string Version => "1.0";

		public MatchResult Grep(FileInfo file, ISearchSpec searchSpec, ref Exception ex)
		{
			ex = new Exception("the plug-in failed");
			IsFileSkipped = true;
			return null;
		}

		public bool IsFileSupported(FileInfo file) => true;
		public bool Load() => true;
		public bool Load(bool visible) => true;
		public void Unload() { }
	}

	/// <summary>
	/// A plug-in that claims to have handled every file and never asks to be skipped.
	/// </summary>
	internal sealed class ClaimingPlugin : IIcedAstroGrepPlugin
	{
		public string Author => "test";
		public string Description => "handles everything";
		public string Extensions => string.Empty;
		public bool IsAvailable => true;
		public bool IsFileSkipped => false;
		public string Name => "Claiming";
		public string Version => "1.0";

		public MatchResult Grep(FileInfo file, ISearchSpec searchSpec, ref Exception ex) => null;

		public bool IsFileSupported(FileInfo file) => true;
		public bool Load() => true;
		public bool Load(bool visible) => true;
		public void Unload() { }
	}

	/// <summary>
	/// Pins the contract that decides whether a plug-in failure is a visible error with a fallback
	/// or a silent false negative — the worst possible outcome for a search tool.
	/// </summary>
	public class PluginContractTests
	{
		private static List<PluginWrapper> Wrap(IIcedAstroGrepPlugin plugin)
		{
			return new List<PluginWrapper> { new PluginWrapper(plugin, string.Empty, plugin.Name, false, true, 0) };
		}

		[Fact]
		public void APLuginThatFailsAndSkips_ReportsTheErrorAndStillUsesTheDefaultSearch()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("document.txt", "before" + Environment.NewLine + "the needle is here" + Environment.NewLine);

				var grep = new Grep(new TestSearchSpec { StartDirectories = new[] { folder.Path }, SearchText = "needle" })
				{
					Plugins = Wrap(new FailingPlugin())
				};

				var errors = new List<Exception>();
				grep.SearchError += (file, ex) => errors.Add(ex);

				grep.Execute();

				Assert.Single(errors);
				Assert.Contains("the plug-in failed", errors[0].Message);
				Assert.Single(grep.MatchResults);
				Assert.False(grep.MatchResults[0].FromPlugin);
			}
		}

		[Fact]
		public void APLuginThatClaimsTheFile_SuppressesTheDefaultSearch()
		{
			using (var folder = new TempFolder())
			{
				folder.Write("document.txt", "the needle is here" + Environment.NewLine);

				var grep = new Grep(new TestSearchSpec { StartDirectories = new[] { folder.Path }, SearchText = "needle" })
				{
					Plugins = Wrap(new ClaimingPlugin())
				};

				grep.Execute();

				// this is exactly why a plug-in must set IsFileSkipped when it cannot read a file:
				// claiming it silently loses every hit in it
				Assert.Empty(grep.MatchResults);
			}
		}
	}
}
