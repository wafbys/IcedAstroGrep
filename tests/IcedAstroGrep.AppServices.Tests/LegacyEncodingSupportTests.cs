using System.Text;

using IcedAstroGrep.Core;

using Xunit;

namespace IcedAstroGrep.AppServices.Tests
{
	/// <summary>
	/// The legacy code pages are needed by the Excel reader and by the encoding detectors, and they
	/// are one registration away: WindowsDesktop carries the provider, so nothing has to be packaged
	/// for them. These tests fail loudly if that ever stops being true.
	/// </summary>
	public class LegacyEncodingSupportTests
	{
		[Fact]
		public void EnsureRegistered_MakesTheLegacyCodePagesUsable()
		{
			LegacyEncodingSupport.EnsureRegistered();

			// 0x93/0x94 are the curly quotes in 1252, so this fails if a fallback encoding is used
			var windows1252 = Encoding.GetEncoding(1252);

			Assert.Equal("\u201Cquoted\u201D", windows1252.GetString(new byte[] { 0x93, 0x71, 0x75, 0x6F, 0x74, 0x65, 0x64, 0x94 }));
		}

		[Fact]
		public void EnsureRegistered_CanBeCalledRepeatedly()
		{
			LegacyEncodingSupport.EnsureRegistered();
			LegacyEncodingSupport.EnsureRegistered();

			Assert.Equal(1252, Encoding.GetEncoding(1252).CodePage);
			Assert.Equal(20127, Encoding.GetEncoding(20127).CodePage);
		}
	}
}
