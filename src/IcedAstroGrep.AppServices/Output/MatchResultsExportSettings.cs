using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using IcedAstroGrep.Core;

namespace IcedAstroGrep.Output
{
   /// <summary>
   /// All export settings.
   /// </summary>
   public class MatchResultsExportSettings
   {
      /// <summary>File path</summary>
      public string Path { get; set; }

      /// <summary>Grep object containing settings and all results</summary>
      public Grep Grep { get; set; }

      /// <summary>The indexes of the grep's MatchResults to export</summary>
      public List<int> GrepIndexes { get; set; }

      /// <summary>Determines whether to show line numbers</summary>
      public bool ShowLineNumbers { get; set; }

      /// <summary>Determines whether to trim leading white space</summary>
      public bool RemoveLeadingWhiteSpace { get; set; }

      /// <summary>The number of context lines before a matched line</summary>
      public int ContextLinesBefore { get; set; }

      /// <summary>The number of context lines after a matched line</summary>
      public int ContextLinesAfter { get; set; }

      /// <summary>
      /// Whether each displayed line carries the file, line and column it came from as <c>data-</c>
      /// attributes.
      /// </summary>
      /// <remarks>
      /// Off by default, so an export is byte for byte what it always was. A viewer that wants clicks to
      /// open the source turns it on and reads the attributes back, which is how the WinUI shell maps a
      /// click in its results pane to a position in a file.
      /// </remarks>
      public bool IncludeSourceLocations { get; set; }
   }
}
