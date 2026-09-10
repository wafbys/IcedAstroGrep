namespace IcedAstroGrep.Theme
{
	/// <summary>
	/// IcedAstroGrep ships a light theme only.
	/// </summary>
	public class ThemeProvider
	{
		private static ITheme theme;

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
		}
	}
}
