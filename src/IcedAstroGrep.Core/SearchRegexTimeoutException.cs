using System;

namespace IcedAstroGrep.Core
{
   /// <summary>
   /// Thrown when the search regular expression exceeds the allowed match timeout.
   /// This is a fatal error for the running search: a pattern that trips the timeout once
   /// (catastrophic backtracking) will trip it on every following line, so the search is
   /// stopped and reported instead of silently hanging the search thread.
   /// </summary>
   /// <remarks>
   /// IcedAstroGrep File Searching Utility. Written by Theodore L. Ward
   /// Copyright (C) 2002 AstroComma Incorporated.
   /// 
   /// This program is free software; you can redistribute it and/or
   /// modify it under the terms of the GNU General Public License
   /// as published by the Free Software Foundation; either version 2
   /// of the License, or (at your option) any later version.
   /// 
   /// This program is distributed in the hope that it will be useful,
   /// but WITHOUT ANY WARRANTY; without even the implied warranty of
   /// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   /// GNU General Public License for more details.
   /// 
   /// You should have received a copy of the GNU General Public License
   /// along with this program; if not, write to the Free Software
   /// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
   /// 
   /// The author may be contacted at:
   /// ted@astrocomma.com or curtismbeard@gmail.com
   /// </remarks>
   public class SearchRegexTimeoutException : Exception
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="SearchRegexTimeoutException"/> class.
      /// </summary>
      /// <param name="message">Description of the failure</param>
      /// <param name="innerException">The underlying <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/></param>
      public SearchRegexTimeoutException(string message, Exception innerException)
         : base(message, innerException)
      {
      }
   }
}
