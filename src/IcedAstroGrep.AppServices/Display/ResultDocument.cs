using System;
using System.Collections.Generic;
using System.Linq;

using IcedAstroGrep.Core;

namespace IcedAstroGrep.Display
{
	/// <summary>
	/// What a line in the results pane is.
	/// </summary>
	public enum ResultLineKind
	{
		/// <summary>The full path of a file whose matches follow.</summary>
		FileName,

		/// <summary>A line of the file: either a hit or a context line around one.</summary>
		Content,

		/// <summary>A blank line separating two files' results.</summary>
		Separator
	}

	/// <summary>
	/// One line of the results pane, with the source position it came from.
	/// </summary>
	/// <remarks>
	/// Immutable: a shell renders these, and the line number margin reads the source position to show
	/// the file's own line numbers next to the text.
	/// </remarks>
	public sealed class ResultDocumentLine
	{
		/// <summary>
		/// Creates a line.
		/// </summary>
		/// <param name="text">Text as it appears in the pane</param>
		/// <param name="kind">What this line is</param>
		/// <param name="sourceLineNumber">Line number within the source file, or -1 when the line has none</param>
		/// <param name="sourceFile">Source file this line belongs to, empty when it belongs to none</param>
		/// <param name="columnNumber">Column of the first hit within the line, 1 based</param>
		/// <param name="hasMatch">Whether the line contains a hit rather than context</param>
		public ResultDocumentLine(string text, ResultLineKind kind, int sourceLineNumber, string sourceFile, int columnNumber, bool hasMatch)
		{
			Text = text;
			Kind = kind;
			SourceLineNumber = sourceLineNumber;
			SourceFile = sourceFile ?? string.Empty;
			ColumnNumber = columnNumber;
			HasMatch = hasMatch;
		}

		/// <summary>Text as it appears in the pane.</summary>
		public string Text { get; }

		/// <summary>What this line is.</summary>
		public ResultLineKind Kind { get; }

		/// <summary>Line number within the source file, or -1 when the line has none.</summary>
		public int SourceLineNumber { get; }

		/// <summary>Source file this line belongs to, empty when it belongs to none.</summary>
		public string SourceFile { get; }

		/// <summary>Column of the first hit within the line, 1 based.</summary>
		public int ColumnNumber { get; }

		/// <summary>Whether the line contains a hit rather than context.</summary>
		public bool HasMatch { get; }
	}

	/// <summary>
	/// How a shell wants the results composed.
	/// </summary>
	public sealed class ResultDocumentOptions
	{
		/// <summary>Remove the leading white space of every displayed line.</summary>
		/// <remarks>
		/// On a hit line the indentation up to the hit is kept, so the match stays where the reader
		/// expects it; context lines are trimmed outright.
		/// </remarks>
		public bool RemoveLeadingWhiteSpace { get; set; }

		/// <summary>Context lines to show before each hit.</summary>
		public int BeforeContextLines { get; set; }

		/// <summary>Context lines to show after each hit.</summary>
		public int AfterContextLines { get; set; }
	}

	/// <summary>
	/// The results of a search, composed for display: the text of the pane plus, for every line, the
	/// source position it came from.
	/// </summary>
	/// <remarks>
	/// This is the shell-agnostic half of the results pane. It used to live in the WinForms shell's
	/// frmMain, written against the AvalonEdit widget; a second shell should not have to re-invent how
	/// results are laid out, and the layout deserves tests, which it could not have while it was a
	/// hundred lines of widget calls.
	/// </remarks>
	public sealed class ResultDocument
	{
		private ResultDocument(IList<ResultDocumentLine> lines)
		{
			Lines = lines;

			// The pane text is the lines joined by the platform newline and, deliberately, without a
			// trailing one: the shell used to append every line and then remove the final newline.
			Text = string.Join(Environment.NewLine, lines.Select(line => line.Text));
		}

		/// <summary>The lines of the pane, in order.</summary>
		public IList<ResultDocumentLine> Lines { get; }

		/// <summary>The text of the pane: <see cref="Lines"/> joined by the platform newline, no trailing newline.</summary>
		public string Text { get; }

		/// <summary>
		/// Composes the given results for display: for each file its full path, a blank line, the hit
		/// and context lines, and two blank lines before the next file.
		/// </summary>
		/// <param name="matches">Results of a search, in the order they should be displayed</param>
		/// <param name="options">How to compose, or null for the defaults (no trimming, no context)</param>
		/// <returns>The composed document; empty when there is nothing to show</returns>
		public static ResultDocument Build(IList<MatchResult> matches, ResultDocumentOptions options)
		{
			var lines = new List<ResultDocumentLine>();

			if (matches == null || matches.Count == 0)
			{
				return new ResultDocument(lines);
			}

			options = options ?? new ResultDocumentOptions();

			var separator = new ResultDocumentLine(string.Empty, ResultLineKind.Separator, -1, string.Empty, 1, false);

			for (int i = 0; i < matches.Count; i++)
			{
				MatchResult match = matches[i];
				string path = match.File.FullName;

				lines.Add(new ResultDocumentLine(path, ResultLineKind.FileName, -1, path, 1, false));
				lines.Add(separator);

				var displayLines = match.GetDisplayMatches(options.BeforeContextLines, options.AfterContextLines);

				for (int j = 0; j < displayLines.Count; j++)
				{
					var displayLine = displayLines[j];
					string text = displayLine.Line;

					if (options.RemoveLeadingWhiteSpace)
					{
						text = displayLine.HasMatch
							? text.Substring(Utils.GetValidLeadingSpaces(text, displayLine.Matches[0].StartPosition))
							: text.TrimStart();
					}

					lines.Add(new ResultDocumentLine(
						text,
						ResultLineKind.Content,
						displayLine.LineNumber,
						displayLine.LineNumber > -1 ? path : string.Empty,
						displayLine.ColumnNumber,
						displayLine.HasMatch));
				}

				if (i + 1 < matches.Count)
				{
					lines.Add(separator);
					lines.Add(separator);
				}
			}

			return new ResultDocument(lines);
		}
	}
}
