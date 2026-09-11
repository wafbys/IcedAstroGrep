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
		/// <summary>
		/// Reported when the running build was not produced from a git checkout, or on a machine
		/// without git: a source export still has to be able to say where it came from.
		/// </summary>
		public const string UnknownCommit = "unknown";

		/// <summary>Metadata key carrying the commit in the assembly built from this repository.</summary>
		private const string CommitMetadataKey = "GitHash";

		public static Color ApplicationColor = Color.FromArgb(251, 127, 6);

		public static string ApplicationName = "IcedAstroGrep";

		public static string LicenseUrl = "https://www.gnu.org/licenses/old-licenses/gpl-2.0.html";

		public static string RegExHelpUrl = "https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-language-quick-reference";

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

		/// <summary>
		/// The commit the running build came from, or <see cref="UnknownCommit"/> when the build had
		/// no repository to read it from.
		/// </summary>
		public static string ApplicationCommit
		{
			get { return ReadCommit(Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()); }
		}

		/// <summary>
		/// Version and commit as one display string, for example <c>1.1.0 (a1b2c3d4e5f6)</c>.
		/// </summary>
		public static string ApplicationVersionText
		{
			get { return string.Format("{0} ({1})", ApplicationVersion.ToString(3), ApplicationCommit); }
		}

		/// <summary>
		/// Reads the commit stamped into the given assembly by the build.
		/// </summary>
		/// <param name="assembly">Assembly to read, may be null</param>
		/// <returns>The commit, or <see cref="UnknownCommit"/> when the assembly carries none</returns>
		public static string ReadCommit(Assembly assembly)
		{
			if (assembly == null)
			{
				return UnknownCommit;
			}

			try
			{
				foreach (object attribute in assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
				{
					var metadata = attribute as AssemblyMetadataAttribute;
					if (metadata != null && metadata.Key == CommitMetadataKey && !string.IsNullOrWhiteSpace(metadata.Value))
					{
						return metadata.Value;
					}
				}
			}
			catch (Exception)
			{
				// an unreadable attribute must not stop the application from starting
			}

			return UnknownCommit;
		}

		/// <summary>IcedAstroGrep is portable-only; settings live next to the executable.</summary>
		public static bool IsPortable => true;
	}
}
