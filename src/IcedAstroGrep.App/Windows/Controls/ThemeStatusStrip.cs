using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IcedAstroGrep.Windows.Controls
{
	/// <summary>
	/// A <see cref="StatusStrip"/> with custom dark theme support.
	/// </summary>
	public class ThemeStatusStrip : StatusStrip
	{
		#region Constructor Region

		/// <summary>
		/// Constructor, sets back and fore colors
		/// </summary>
		public ThemeStatusStrip()
		{
			BackColor = Theme.ThemeProvider.Theme.Colors.Control;
			ForeColor = Theme.ThemeProvider.Theme.Colors.ForeColor;
		}

		#endregion

		/// <summary>
		/// Resets the back and fore colors and redraws itself
		/// </summary>
		public void Reset()
		{
			BackColor = Theme.ThemeProvider.Theme.Colors.Control;
			ForeColor = Theme.ThemeProvider.Theme.Colors.ForeColor;

			Invalidate();
		}

		#region Paint Region

		/// <inheritdoc/>
		protected override void OnPaintBackground(PaintEventArgs e)
		{
			var g = e.Graphics;

			using (var b = new SolidBrush(Theme.ThemeProvider.Theme.Colors.Control))
			{
				g.FillRectangle(b, ClientRectangle);
			}

			using (var p = new Pen(Theme.ThemeProvider.Theme.Colors.ControlDark))
			{
				g.DrawLine(p, ClientRectangle.Left, 0, ClientRectangle.Right, 0);
			}

			using (var p = new Pen(Theme.ThemeProvider.Theme.Colors.ControlLight))
			{
				g.DrawLine(p, ClientRectangle.Left, 1, ClientRectangle.Right, 1);
			}
		}

		#endregion
	}
}
