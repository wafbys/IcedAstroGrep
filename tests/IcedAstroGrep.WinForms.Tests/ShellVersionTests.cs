using IcedAstroGrep.Core;

using Xunit;

namespace IcedAstroGrep.WinForms.Tests
{
	/// <summary>
	/// The window caption is built from the shell assembly's own version and the commit stamped into
	/// it, so both have to actually be present in that assembly — the engine asks the running
	/// (entry) assembly, which is this one once the application starts.
	/// </summary>
	public class ShellVersionTests
	{
		[Fact]
		public void TheShellAssemblyIsStampedWithAVersionAndACommit()
		{
			var shell = typeof(IcedAstroGrep.Windows.Forms.frmMain).Assembly;

			var version = shell.GetName().Version;
			Assert.NotNull(version);
			Assert.True(version.Major > 0, "the shell assembly has no real version: " + version);

			string commit = ProductInformation.ReadCommit(shell);
			Assert.False(string.IsNullOrWhiteSpace(commit), "no commit metadata was stamped into the shell assembly");

			// "unknown" is the deliberate fallback for a source export without a repository
			if (commit != ProductInformation.UnknownCommit)
			{
				Assert.Matches("^[0-9a-f]{7}$", commit);
			}
		}

		[Fact]
		public void TheCaptionFormatCarriesBothTheVersionAndTheCommit()
		{
			var shell = typeof(IcedAstroGrep.Windows.Forms.frmMain).Assembly;

			// this mirrors what frmMain.SetWindowText composes from these two values
			string caption = string.Format("{0} {1} ({2})", ProductInformation.ApplicationName, shell.GetName().Version.ToString(3), ProductInformation.ReadCommit(shell));

			Assert.Matches(@"^IcedAstroGrep \d+\.\d+\.\d+ \(.+\)$", caption);
		}
	}
}
