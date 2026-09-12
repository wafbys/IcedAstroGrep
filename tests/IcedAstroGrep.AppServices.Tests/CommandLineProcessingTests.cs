using System.IO;
using System.Linq;

using Xunit;

namespace IcedAstroGrep.AppServices.Tests
{
	/// <summary>
	/// The command line is the one interface both shells have to behave identically on, and it had no
	/// coverage at all. These tests pin the implemented forms down, including three sharp edges that are
	/// easy to get wrong: a bare directory is only taken as the start path when it is the single
	/// argument (otherwise use <c>/spath=</c>), <c>/otype</c> is lowercased, and a value with spaces has
	/// to arrive as one quoted argument.
	/// </summary>
	public class CommandLineProcessingTests
	{
		private const string Exe = "IcedAstroGrep.exe";

		[Fact]
		public void NoArguments_ReportsNoArguments()
		{
			var args = CommandLineProcessing.Process(new[] { Exe });

			Assert.False(args.AnyArguments);
			Assert.False(args.DisplayHelp);
		}

		[Fact]
		public void ASingleDirectoryIsTakenAsTheStartPath()
		{
			var args = CommandLineProcessing.Process(new[] { Exe, Path.GetTempPath() });

			Assert.True(args.AnyArguments);
			Assert.True(args.IsValidStartPath);
			Assert.Equal(Path.GetTempPath(), args.StartPath);
		}

		[Fact]
		public void WithMoreArgumentsTheStartPathNeedsTheSwitch()
		{
			// a bare directory is silently ignored once other arguments are present, which is why the
			// command line reference documents /spath
			var bare = CommandLineProcessing.Process(new[] { Exe, "/stext=needle", Path.GetTempPath() });

			Assert.True(bare.AnyArguments);
			Assert.False(bare.IsValidStartPath);

			var withSwitch = CommandLineProcessing.Process(new[] { Exe, "/stext=needle", "/spath=" + Path.GetTempPath() });

			Assert.True(withSwitch.IsValidStartPath);
			Assert.Equal(Path.GetTempPath(), withSwitch.StartPath);
		}

		[Fact]
		public void TheSearchTextIsAcceptedInEveryImplementedForm()
		{
			string[][] forms =
			{
				new[] { "/stext=needle" },
				new[] { "-stext=needle" },
				new[] { "--stext=needle" },
				new[] { "-stext", "needle" }
			};

			foreach (string[] form in forms)
			{
				string shown = string.Join(" ", form);
				var args = CommandLineProcessing.Process(new[] { Exe }.Concat(form).ToArray());

				Assert.True(args.AnyArguments, shown);
				Assert.True(args.IsValidSearchText, shown);
				Assert.Equal("needle", args.SearchText);
			}
		}

		[Fact]
		public void AQuotedValueWithSpacesIsUnquoted()
		{
			var args = CommandLineProcessing.Process(new[] { Exe, "/stext=\"two words\"" });

			Assert.Equal("two words", args.SearchText);
		}

		[Fact]
		public void FlagsAndSwitchesAreParsed()
		{
			var args = CommandLineProcessing.Process(new[] { Exe, "/s", "/r", "/c", "/w", "/f", "/n", "/l", "/e", "/exit", "/sh", "/ss" });

			Assert.True(args.StartSearch);
			Assert.True(args.UseRecursion);
			Assert.True(args.IsCaseSensitive);
			Assert.True(args.IsWholeWord);
			Assert.True(args.IsFileNamesOnly);
			Assert.True(args.IsNegation);
			Assert.True(args.UseLineNumbers);
			Assert.True(args.UseRegularExpressions);
			Assert.True(args.ExitAfterSearch);
			Assert.True(args.SkipHiddenFile);
			Assert.True(args.SkipHiddenDirectory);
			Assert.True(args.SkipSystemFile);
			Assert.True(args.SkipSystemDirectory);
		}

		[Fact]
		public void AnExportRequestImpliesASearchAndLowercasesTheType()
		{
			string outputPath = Path.Combine(Path.GetTempPath(), "results");

			var args = CommandLineProcessing.Process(new[] { Exe, "/stext=needle", "/opath=" + outputPath, "/otype=JSON" });

			Assert.Equal(outputPath, args.OutputPath);
			Assert.Equal("json", args.OutputType);
			Assert.True(args.StartSearch);
		}

		[Fact]
		public void AContextLineCountIsAcceptedWithinTheShellLimitOnly()
		{
			var accepted = CommandLineProcessing.Process(new[] { Exe, "/stext=needle", "/cl=2" });

			Assert.Equal(2, accepted.ContextLines);

			// out of range values are dropped rather than passed on to the engine, which would reject them
			var rejected = CommandLineProcessing.Process(new[] { Exe, "/stext=needle", "/cl=9999" });

			Assert.NotEqual(9999, rejected.ContextLines);
		}

		[Theory]
		[InlineData("/?")]
		[InlineData("/h")]
		[InlineData("/help")]
		public void TheHelpSwitchesAreRecognised(string form)
		{
			var args = CommandLineProcessing.Process(new[] { Exe, form });

			Assert.True(args.AnyArguments);
			Assert.True(args.DisplayHelp);
		}
	}
}
