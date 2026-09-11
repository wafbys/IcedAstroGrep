using System;

using IcedAstroGrep.Core;

namespace IcedAstroGrep
{
	/// <summary>
	/// Contains common methods to convert to string/from string.
	/// </summary>
	/// <remarks>
	///   IcedAstroGrep File Searching Utility. Written by Theodore L. Ward
	///   Copyright (C) 2002 AstroComma Incorporated.
	///
	///   This program is free software; you can redistribute it and/or
	///   modify it under the terms of the GNU General public License
	///   as published by the Free Software Foundation; either version 2
	///   of the License, or (at your option) any later version.
	///
	///   This program is distributed in the hope that it will be useful,
	///   but WITHOUT ANY WARRANTY; without even the implied warranty of
	///   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	///   GNU General public License for more details.
	///
	///   You should have received a copy of the GNU General public License
	///   along with this program; if not, write to the Free Software
	///   Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
	///
	///   The author may be contacted at:
	///   ted@astrocomma.com or curtismbeard@gmail.com
	/// </remarks>
	/// <history>
	/// [Curtis_Beard]      11/07/2012  Initial
	/// </history>
	public static class Convertors
	{
		/// <summary>
		/// <summary>
		/// Converts a Color to a string.
		/// </summary>
		/// <param name="color">Color</param>
		/// <returns>color values as a string</returns>
		/// <history>
		/// [Curtis_Beard]		11/03/2006	Created
		/// </history>
		public static string ConvertColorToString(System.Drawing.Color color)
		{
			return string.Format("{0}{4}{1}{4}{2}{4}{3}", color.R.ToString(), color.G.ToString(), color.B.ToString(), color.A.ToString(), Constants.COLOR_SEPARATOR);
		}

		/// <summary>
		/// Converts a colour setting value into the text an HTML/CSS style attribute accepts.
		/// </summary>
		/// <param name="colorSettingValue">colour values as a string</param>
		/// <returns>colour as #RRGGBB</returns>
		/// <remarks>
		/// Replaces System.Drawing.ColorTranslator.ToHtml, which lives in System.Drawing.Common. Every
		/// colour ConvertStringToColor can produce comes from Color.FromArgb, so it is never a KnownColor
		/// and ToHtml would have returned exactly the #RRGGBB text this produces; keeping the conversion
		/// here is what lets this shell-agnostic assembly depend on System.Drawing.Primitives alone.
		/// </remarks>
		public static string ConvertColorSettingToHtml(string colorSettingValue)
		{
			System.Drawing.Color color = ConvertStringToColor(colorSettingValue);

			return string.Format(System.Globalization.CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
		}

		/// <summary>
		/// Converts given file size in bytes as string to the display type as a string.
		/// </summary>
		/// <param name="bytes">File size in bytes</param>
		/// <param name="displayType">byte,kb,mb,gb</param>
		/// <returns>file size in given display type</returns>
		/// <history>
		/// [Curtis_Beard]        11/17/2014  Initial
		/// [Curtis_Beard]        03/05/2020  CHG: .Net 4.5 improvements
		/// </history>
		public static string ConvertFileSizeForDisplay(string bytes, string displayType)
		{
			// convert bytes value to selected display size
			long value = long.Parse(bytes);
			switch (displayType.ToLower())
			{
				case "byte":
					break;

				case "kb":
					value /= 1024;
					break;

				case "mb":
					value /= (1024 * 1024);
					break;

				case "gb":
					value /= (1024 * 1024 * 1024);
					break;
			}

			return value.ToString();
		}

		/// <summary>
		/// Converts given file size to long for use in comparison of file sizes.
		/// </summary>
		/// <param name="textValue">TextBox value entered by user</param>
		/// <param name="sizeType">The selected size type (byte,kb,mb,gb)</param>
		/// <param name="defaultValue">The default value</param>
		/// <returns>long representing number of bytes user selected</returns>
		/// <history>
		/// [Curtis_Beard]        02/09/2012  ADD: 3424156, size drop down selection
		/// [Curtis_Beard]        03/05/2020  CHG: .Net 4.5 improvements
		/// </history>
		public static long ConvertFileSizeFromDisplay(string textValue, string sizeType, long defaultValue)
		{
			long retVal = defaultValue;

			if (double.TryParse(textValue, out double size))
			{
				switch (sizeType.ToLower())
				{
					case "byte":
						break;

					case "kb":
						size *= 1024;
						break;

					case "mb":
						size *= 1024 * 1024;
						break;

					case "gb":
						size *= 1024 * 1024 * 1024;
						break;
				}

				retVal = (long)size;
			}

			return retVal;
		}

		/// <summary>
		/// Converts a string to a Color.
		/// </summary>
		/// <param name="color">color values as a string</param>
		/// <returns>Color</returns>
		/// <history>
		/// [Curtis_Beard]		11/03/2006	Created
		/// </history>
		public static System.Drawing.Color ConvertStringToColor(string color)
		{
			string[] rgba = color.Split(char.Parse(Constants.COLOR_SEPARATOR));

			return System.Drawing.Color.FromArgb(byte.Parse(rgba[3]), byte.Parse(rgba[0]), byte.Parse(rgba[1]), byte.Parse(rgba[2]));
		}

		/// <summary>
		/// Retrieves the values as an array of strings.
		/// </summary>
		/// <param name="values">ComboBox values as a string</param>
		/// <returns>Array of strings</returns>
		/// <history>
		/// [Curtis_Beard]		11/03/2006	Created
		/// </history>
		public static string[] GetComboBoxEntriesFromString(string values)
		{
			string[] entries = Utils.SplitByString(values, Constants.SEARCH_ENTRIES_SEPARATOR);

			return entries;
		}

		/// <summary>
		/// Get the hit count from the hit/line count display text (assumes in format hit / line)
		/// </summary>
		/// <param name="displayText">The display text to parse (format hit / line)</param>
		/// <returns>hit count</returns>
		/// <history>
		/// [Curtis_Beard]		01/08/2019	CHG: 119, add line hit count
		/// </history>
		public static int GetHitCountFromCountDisplay(string displayText)
		{
			string text = displayText;

			int pos = text.IndexOf(" /");
			if (pos == -1)
			{
				pos = text.Length;
			}
			text = text.Substring(0, pos);

			return int.Parse(text);
		}

		/// <summary>
		/// Get the line count from the hit/line count display text (assumes in format hit / line)
		/// </summary>
		/// <param name="displayText">The display text to parse (format hit / line)</param>
		/// <returns>hit count</returns>
		/// <history>
		/// [Curtis_Beard]		01/08/2019	CHG: 119, add line hit count
		/// </history>
		public static int GetLineCountFromCountDisplay(string displayText)
		{
			string text = displayText;

			int pos = text.IndexOf("/ ");
			if (pos == -1)
			{
				pos = -2;
			}
			text = text.Substring(pos + 2);

			return int.Parse(text);
		}

	}
}