using System.Windows.Forms;

namespace IcedAstroGrep.Theme
{
	/// <summary>
	/// IcedAstroGrep ships a light theme only.
	/// </summary>
	public class ThemeProvider
	{
		private static ITheme theme;

		public enum ThemeType
		{
			System = 0,
			Light = 1,
			Dark = 2
		}

		public static ITheme Theme
		{
			get
			{
				if (theme == null)
				{
					theme = new LightTheme();
				}

				return theme;
			}
			set
			{
				theme = value ?? new LightTheme();
			}
		}

		public static void ChangeTheme(ThemeType themeType)
		{
			theme = new LightTheme();
		}

		public static void ChangeThemeBySystem()
		{
			theme = new LightTheme();
		}

		public static void Reload()
		{
			theme = new LightTheme();
		}
	}
}
