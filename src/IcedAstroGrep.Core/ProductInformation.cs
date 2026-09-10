using System;
using System.Drawing;
using System.Reflection;

namespace IcedAstroGrep.Core
{
	/// <summary>
	/// Product identity for IcedAstroGrep, a portable fork of AstroGrep.
	/// </summary>
	public sealed class ProductInformation
	{
		public static Color ApplicationColor = Color.FromArgb(251, 127, 6);

		public static string ApplicationName = "IcedAstroGrep";

		public static string LicenseUrl = "https://www.gnu.org/licenses/old-licenses/gpl-2.0.html";

		public static string RegExHelpUrl = "https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-language-quick-reference";

		public static string WebsiteUrl = LicenseUrl;

		public static string HelpUrl = RegExHelpUrl;

		public static string DonationUrl = string.Empty;

		public static string DownloadUrl = string.Empty;

		public static string VersionUrl = string.Empty;

		/// <summary>Upstream project this fork is based on.</summary>
		public static string UpstreamName = "AstroGrep";

		public static Version ApplicationVersion
		{
			get
			{
				Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
				return assembly.GetName().Version ?? new Version(1, 0, 0, 0);
			}
		}

		/// <summary>IcedAstroGrep is portable-only; settings live next to the executable.</summary>
		public static bool IsPortable => true;
	}
}
