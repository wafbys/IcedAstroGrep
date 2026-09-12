using System.Drawing;
using System.Windows.Forms;

namespace IcedAstroGrep.Theme
{
	/// <summary>
	/// The application light/default color theme.
	/// </summary>
	public class LightTheme : ITheme
	{
		/// <summary>
		/// The IcedAstroGrep accent colour, used when the user opts in to it.
		/// </summary>
		/// <remarks>
		/// This lived in <c>ProductInformation</c> in the engine until the second shell was planned.
		/// A UI value has no business in the engine, and this theme is the only thing that ever read
		/// it.
		/// </remarks>
		private static readonly Color IcedAstroGrepAccentColor = Color.FromArgb(251, 127, 6);

		/// <summary>
		/// Initialize this theme and its colors.
		/// </summary>
		public LightTheme()
		{
			Colors.BackColor = SystemColors.Window;
			Colors.ForeColor = SystemColors.ControlText;
			Colors.LinkColor = SystemColors.HotTrack;
			Colors.ApplicationAccentColor = GeneralSettings.UseIcedAstroGrepAccentColor ? IcedAstroGrepAccentColor : SystemColors.ControlText;

			Colors.Control = SystemColors.Control;
			Colors.ControlDark = SystemColors.ControlDark;
			Colors.ControlDarkDark = SystemColors.ControlDarkDark;
			Colors.ControlLight = SystemColors.ControlLight;
			Colors.Window = SystemColors.Window;
		}

		/// <inheritdoc/>
		public Colors Colors { get; } = new Colors();

		/// <inheritdoc/>
		public ToolStripRenderer MenuRenderer { get; } = new ToolStripProfessionalRenderer();

		/// <inheritdoc/>
		public ToolStripRenderer ToolStripRenderer { get; } = new ToolStripProfessionalRenderer();
	}
}