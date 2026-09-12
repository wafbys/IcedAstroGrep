namespace IcedAstroGrep
{
	/// <summary>
	/// How the engine asks the hosting shell to show a message to the user.
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
	///   The engine knows the language *key*, not the text: the language resources belong to the shell
	///   (IcedAstroGrep.WinForms/Windows/Language.cs plus Language/*.xml), so a second shell brings its own
	///   wording and its own way of showing it -- a WinForms MessageBox today, a WinUI 3 ContentDialog
	///   or nothing at all tomorrow. The engine always logs the same failure as well, so a caller that
	///   passes null loses the dialog but not the diagnosis.
	/// </remarks>
	public interface IUserNotifier
	{
		/// <summary>
		/// Shows a localized message to the user.
		/// </summary>
		/// <param name="languageKey">Key of the generic text to show, for example TextEditorsErrorGeneric.</param>
		/// <param name="formatArguments">Arguments for the text's placeholders, or null when it takes none.</param>
		/// <param name="severity">How much attention the message deserves.</param>
		void Notify(string languageKey, object[] formatArguments, NotificationSeverity severity);
	}

	/// <summary>
	/// How much attention a message from the engine deserves. Mirrors the message box icons the
	/// WinForms shell uses, so a shell can map it to whatever it has.
	/// </summary>
	public enum NotificationSeverity
	{
		/// <summary>Something the user should know; nothing is broken.</summary>
		Information,

		/// <summary>Something the user asked for did not work.</summary>
		Warning
	}
}
