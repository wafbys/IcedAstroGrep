using System.Text;

namespace IcedAstroGrep.Core
{
	/// <summary>
	/// Registers the legacy code page encodings that .NET Core does not ship by default.
	/// </summary>
	/// <remarks>
	///   IcedAstroGrep File Searching Utility. Written by Theodore L. Ward
	///   Copyright (C) 2002 AstroComma Incorporated.
	///
	///   This program is free software; you can redistribute it and/or
	///   modify it under the terms of the GNU General Public License
	///   as published by the Free Software Foundation; either version 2
	///   of the License, or (at your option) any later version.
	///
	///   This program is distributed in the hope that it will be useful,
	///   but WITHOUT ANY WARRANTY; without even the implied warranty of
	///   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	///   GNU General Public License for more details.
	///
	///   You should have received a copy of the GNU General Public License
	///   along with this program; if not, write to the Free Software
	///   Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
	///
	///   The author may be contacted at:
	///   ted@astrocomma.com or curtismbeard@gmail.com
	/// </remarks>
	/// <history>
	/// </history>
	public static class LegacyEncodingSupport
	{
		private static readonly object SyncRoot = new object();
		private static bool registered;

		/// <summary>
		/// Makes the code page encodings available to <see cref="Encoding.GetEncoding(int)"/> and to
		/// components that resolve them, such as ExcelDataReader and the encoding detectors.
		/// Safe to call repeatedly from anywhere.
		/// </summary>
		/// <remarks>
		/// .NET Core only ships Unicode plus a handful of encodings, so without this every
		/// <c>Encoding.GetEncoding(1252)</c> throws NotSupportedException — which on this branch
		/// broke reading .xls/.xlsx files and re-opening a cached code page for a detected encoding.
		/// </remarks>
		public static void EnsureRegistered()
		{
			if (registered)
			{
				return;
			}

			lock (SyncRoot)
			{
				if (registered)
				{
					return;
				}

				Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
				registered = true;
			}
		}
	}
}
