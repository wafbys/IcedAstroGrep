using System.Reflection;

using Xunit;

namespace IcedAstroGrep.Core.Tests
{
	/// <summary>
	/// The running application reports its version and the commit it was built from; these pin the
	/// build-time stamping and the fallback for a build that had no repository to read.
	/// </summary>
	public class ProductInformationTests
	{
		[Fact]
		public void TheBuildStampsTheCommitIntoTheAssembly()
		{
			string commit = ProductInformation.ReadCommit(typeof(ProductInformation).Assembly);

			Assert.False(string.IsNullOrWhiteSpace(commit), "no commit metadata was stamped into the assembly");

			// "unknown" is the deliberate fallback for a source export without a repository; anything
			// else has to look like the seven character abbreviation the build writes, so it matches
			// what GitHub and GitHub Desktop show for the same commit
			if (commit != ProductInformation.UnknownCommit)
			{
				Assert.Matches("^[0-9a-f]{7}$", commit);
			}
		}

		[Fact]
		public void ReadCommitFallsBackWhenTheAssemblyCarriesNoCommit()
		{
			Assert.Equal(ProductInformation.UnknownCommit, ProductInformation.ReadCommit(null));
			Assert.Equal(ProductInformation.UnknownCommit, ProductInformation.ReadCommit(typeof(string).Assembly));
		}

		[Fact]
		public void ApplicationVersionTextCombinesTheVersionAndTheCommit()
		{
			string text = ProductInformation.ApplicationVersionText;

			Assert.Contains(ProductInformation.ApplicationVersion.ToString(3), text);
			Assert.Contains(ProductInformation.ApplicationCommit, text);
		}
	}
}
