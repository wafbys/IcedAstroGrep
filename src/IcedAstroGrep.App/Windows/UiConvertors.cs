using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

using IcedAstroGrep.Core;

namespace IcedAstroGrep
{
	/// <summary>
	/// Converts UI values (fonts, combo boxes, colours as brushes) to and from the string forms the
	/// settings use.
	/// </summary>
	/// <remarks>
	/// Everything here needs WinForms, WPF or System.Drawing.Common, so it stayed in the shell when the
	/// shell-agnostic half of Convertors moved to IcedAstroGrep.AppServices. The value conversions that
	/// only need System.Drawing.Primitives went with it (see Convertors there).
	/// </remarks>
	public static class UiConvertors
	{
		/// Calculates the width of the drop down list of the given combo box
		/// </summary>
		/// <param name="combo">Combo box to base calculate from</param>
		/// <param name="maxWidth">The maximum width of the drop down (defaults to 600)</param>
		/// <returns>Width of longest string in combo box items</returns>
		/// <history>
		/// [Curtis_Beard]    11/21/2005	Created
		/// [Curtis_Beard]    09/16/2019	CHG: rework to have a max and support extended combobox
		/// [Curtis_Beard]    03/05/2020	CHG: add maxWidth parameter to be able to override
		/// </history>
		public static int CalculateDropDownWidth(ComboBox combo, int maxWidth = 600)
		{
			maxWidth += SystemInformation.VerticalScrollBarWidth;
			int defaultWidth = combo.Width + SystemInformation.VerticalScrollBarWidth;
			int calculatedWidth = defaultWidth;
			bool isComboBoxEx = combo is IcedAstroGrep.Windows.Controls.ComboBoxEx;

			using (Graphics g = combo.CreateGraphics())
			{
				string _itemValue = string.Empty;
				SizeF _size;

				foreach (object _item in combo.Items)
				{
					_itemValue = _item.ToString();
					if (isComboBoxEx)
					{
						_itemValue = (_item as IcedAstroGrep.Windows.ComboBoxExEntry).Display;
					}

					_size = g.MeasureString(_itemValue, combo.Font);

					if (_size.Width > calculatedWidth)
						calculatedWidth = Convert.ToInt32(_size.Width);
				}

				// keep original width if no item longer
				if (calculatedWidth != defaultWidth)
					calculatedWidth += SystemInformation.VerticalScrollBarWidth;
			}

			return Math.Min(calculatedWidth, maxWidth);
		}

		/// <summary>
		/// Converts a font to a string.
		/// </summary>
		/// <param name="font">Font</param>
		/// <returns>font values as a string</returns>
		/// <history>
		/// [Curtis_Beard]	   02/24/2012	CHG: 3488321, ability to change results font
		/// [Curtis_Beard]		10/22/2012	FIX: 36, use invariant culture to always have same float decimal separator
		/// </history>
		public static string ConvertFontToString(System.Drawing.Font font)
		{
			return string.Format("{0}{3}{1}{3}{2}", font.Name, font.Size.ToString(System.Globalization.CultureInfo.InvariantCulture), font.Style.ToString(), Constants.FONT_SEPARATOR);
		}

		/// <summary>
		/// Converts a string to a Font.
		/// </summary>
		/// <param name="font">font values as a string</param>
		/// <returns>Font</returns>
		/// <history>
		/// [Curtis_Beard]	   02/24/2012	CHG: 3488321, ability to change results font
		/// [Curtis_Beard]		10/22/2012	FIX: 36, use invariant culture to always have same float decimal separator
		/// </history>
		public static System.Drawing.Font ConvertStringToFont(string font)
		{
			string[] fontValues = Utils.SplitByString(font, Constants.FONT_SEPARATOR);

			return new System.Drawing.Font(fontValues[0], float.Parse(fontValues[1], System.Globalization.CultureInfo.InvariantCulture), (System.Drawing.FontStyle)Enum.Parse(typeof(System.Drawing.FontStyle), fontValues[2], true), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
		}

		/// <summary>
		/// Converts a string to a SolidColorBrush.
		/// </summary>
		/// <param name="color">color values as a string</param>
		/// <returns>System.Windows.Media.SolidColorBrush</returns>
		/// <history>
		/// [Curtis_Beard]		04/15/2015	Created
		/// </history>
		public static System.Windows.Media.SolidColorBrush ConvertStringToSolidColorBrush(string color)
		{
			System.Drawing.Color dColor = Convertors.ConvertStringToColor(color);

			return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(dColor.R, dColor.G, dColor.B));
		}

		/// <summary>
		/// Retrieves all the ComboBox entries as a string.
		/// </summary>
		/// <param name="combo">ComboBox</param>
		/// <returns>string of entries</returns>
		/// <history>
		/// [Curtis_Beard]		11/03/2006	Created
		/// </history>
		public static string GetComboBoxEntriesAsString(System.Windows.Forms.ComboBox combo)
		{
			string[] entries = new string[combo.Items.Count];

			for (int i = 0; i < combo.Items.Count; i++)
			{
				entries[i] = combo.Items[i].ToString();
			}

			return string.Join(Constants.SEARCH_ENTRIES_SEPARATOR, entries);
		}
	}
}
