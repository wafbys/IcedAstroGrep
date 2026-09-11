using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using IcedAstroGrep.Core;
using IcedAstroGrep.Core.Plugin;
using System.Diagnostics;

namespace IcedAstroGrep.Plugins.PDF
{
	/// <summary>
	/// Used to search a PDF file for a specified string.
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
	///   along with this program; if (not, write to the Free Software
	///   Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
	///
	///   The author may be contacted at:
	///   ted@astrocomma.com or curtismbeard@gmail.com
	/// </remarks>
	/// <history>
	/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
	/// </history>
	public class PDFPlugin : IDisposable, IIcedAstroGrepPlugin
	{
		private string pdfToTxtAppPath = string.Empty;

		/// <summary>
		/// Maximum time to wait for pdftotext to convert a single document before its process tree
		/// is killed. Search cancellation only takes effect between files, so an unresponsive
		/// converter would otherwise hold the whole search open.
		/// </summary>
		private const int PDF_TO_TEXT_TIMEOUT_MILLISECONDS = 60 * 1000;

		/// <summary>
		/// Age after which a .txt left in the plugin temp folder is treated as abandoned by a
		/// crashed or killed run and removed on unload. Live conversions are bounded by
		/// <see cref="PDF_TO_TEXT_TIMEOUT_MILLISECONDS"/>, so nothing legitimate gets this old.
		/// </summary>
		private static readonly TimeSpan STALE_OUTPUT_AGE = TimeSpan.FromMinutes(5);

		/// <summary>
		/// Initializes a new instance of the <see cref="PDFPlugin"/> class.
		/// </summary>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		public PDFPlugin()
		{
			// extract pdf to text application
			if (string.IsNullOrEmpty(pdfToTxtAppPath))
			{
				ExtractPDFToTxtApp();
			}

			IsAvailable = !string.IsNullOrEmpty(pdfToTxtAppPath);
		}

		/// <summary>
		/// Handles destruction of the object.
		/// </summary>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		~PDFPlugin()
		{
			Dispose();
		}

		/// <summary>
		/// Gets the author of the plugin.
		/// </summary>
		public string Author
		{
			get { return "The IcedAstroGrep Team"; }
		}

		/// <summary>
		/// Gets the description of the plugin.
		/// </summary>
		public string Description
		{
			get { return "Searches PDF documents using the pdf to text utility from Xpdf.  Currently doesn't support Context lines or Line Numbers."; }
		}

		/// <summary>
		/// Gets the valid extensions for this grep type.
		/// </summary>
		/// <remarks>Comma separated list of strings.</remarks>
		public string Extensions
		{
			get { return ".pdf"; }
		}

		/// <summary>
		/// Checks to see if the plugin is available on this system.
		/// </summary>
		public bool IsAvailable { get; private set; }

		/// <summary>
		/// Determines whether the plug-in skipped the file and it should be read by another plug-in or the default search.
		/// </summary>
		public bool IsFileSkipped => false;

		/// <summary>
		/// Gets the name of the plugin.
		/// </summary>
		public string Name
		{
			get { return "PDF"; }
		}

		/// <summary>
		/// Gets the version of the plugin.
		/// </summary>
		public string Version
		{
			get { return "1.0.0"; }
		}

		/// <summary>
		/// Handles disposing of the object.
		/// </summary>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		public void Dispose()
		{
			IsAvailable = false;
			pdfToTxtAppPath = string.Empty;

			// deliberately does NOT delete the temp folder: the finalizer calls this, and the folder
			// holds shared state (the extracted utility, plus any output another instance is still
			// reading), so removing it here could break an in-flight search or delete the utility
			// out from under a live instance. Abandoned outputs are swept by Unload(), and keeping
			// the utility is what makes the up-to-date check pay off across runs.
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Searches the given file for the given search text.
		/// </summary>
		/// <param name="file">FileInfo object</param>
		/// <param name="searchSpec">ISearchSpec interface value</param>
		/// <param name="ex">Exception holder if error occurs</param>
		/// <returns>Hitobject containing grep results, null if on error</returns>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		public MatchResult Grep(FileInfo file, ISearchSpec searchSpec, ref Exception ex)
		{
			// initialize Exception object to null
			ex = null;
			MatchResult match = null;

			if (IsFileSupported(file))
			{
				try
				{
					Regex reg = IcedAstroGrep.Core.Grep.BuildSearchRegEx(searchSpec);

					// pull text from pdf file and parse it; the converted text is read line by line
					// so a large document is never held in memory as a whole
					foreach (string line in ExtractText(file))
					{
						int posInStr = -1;
						MatchCollection regCol = null;

						if (searchSpec.UseRegularExpressions)
						{
							regCol = reg.Matches(line);

							if (regCol.Count > 0)
							{
								posInStr = 1;
							}
						}
						else
						{
							// If we are looking for whole worlds only, perform the check.
							if (searchSpec.UseWholeWordMatching)
							{
								// if match is found, also check against our internal line hit count method to be sure they are in sync
								Match mtc = reg.Match(line);
								if (mtc != null && mtc.Success && IcedAstroGrep.Core.Grep.RetrieveLineMatches(line, searchSpec).Count > 0)
								{
									posInStr = mtc.Index;
								}
							}
							else
							{
								posInStr = line.IndexOf(searchSpec.SearchText, searchSpec.UseCaseSensitivity ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
							}
						}

						if (posInStr > -1)
						{
							if (match == null)
							{
								match = new MatchResult(file);

								// found hit in file so just return
								if (searchSpec.ReturnOnlyFileNames)
								{
									break;
								}
							}

							var matchLineFound = new MatchResultLine() { Line = line, LineNumber = -1, HasMatch = true, LongLineCharCount = searchSpec.LongLineCharCount, BeforeAfterCharCount = searchSpec.BeforeAfterCharCount };

							if (searchSpec.UseRegularExpressions)
							{
								posInStr = regCol[0].Index;
								match.SetHitCount(regCol.Count);

								foreach (Match regExMatch in regCol)
								{
									matchLineFound.Matches.Add(new MatchResultLineMatch(regExMatch.Index, regExMatch.Length));
								}
							}
							else
							{
								var lineMatches = IcedAstroGrep.Core.Grep.RetrieveLineMatches(line, searchSpec);
								match.SetHitCount(lineMatches.Count);
								matchLineFound.Matches = lineMatches;
							}
							matchLineFound.ColumnNumber = 1;
							match.Matches.Add(matchLineFound);
						}
					}
				}
				catch (Exception funcEx)
				{
					ex = funcEx;
				}
			}

			return match;
		}

		/// <summary>
		/// Determines if given file is supported by current plugin.
		/// </summary>
		/// <param name="file">Current FileInfo object</param>
		/// <returns>True if supported, False if not supported</returns>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		public bool IsFileSupported(FileInfo file)
		{
			string ext = file.Extension;

			string[] supportedExtensions = Extensions.Split(new char[1] { ',' });
			foreach (string supportedExtension in supportedExtensions)
			{
				if (ext.Equals(supportedExtension, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Loads the plugin and prepares it for a grep.
		/// </summary>
		/// <returns>returns true if (successfully loaded or false otherwise</returns>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		public bool Load()
		{
			return Load(false);
		}

		/// <summary>
		/// Loads the plugin and prepares it for a grep.
		/// </summary>
		/// <param name="visible">true makes underlying application visible, false is make it hidden</param>
		/// <returns>returns true if (successfully loaded or false otherwise</returns>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		public bool Load(bool visible)
		{
			// extract pdf to text application
			if (string.IsNullOrEmpty(pdfToTxtAppPath))
			{
				ExtractPDFToTxtApp();
			}

			return !string.IsNullOrEmpty(pdfToTxtAppPath);
		}

		/// <summary>
		/// Unloads the plugin.
		/// </summary>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		public void Unload()
		{
			// sweep outputs abandoned by a crashed or killed run
			try
			{
				foreach (string leftover in Directory.GetFiles(GetPDFFolder(), "*.txt"))
				{
					try
					{
						if (DateTime.UtcNow - File.GetLastWriteTimeUtc(leftover) > STALE_OUTPUT_AGE)
						{
							File.Delete(leftover);
						}
					}
					catch (Exception ex)
					{
						IcedAstroGrep.Core.Logging.LogClient.Instance.Logger.Warn("Unable to delete abandoned pdftotext output {0}: {1}", leftover, IcedAstroGrep.Core.Logging.LogClient.GetAllExceptions(ex));
					}
				}
			}
			catch (Exception ex)
			{
				IcedAstroGrep.Core.Logging.LogClient.Instance.Logger.Warn("Unable to clean up the PDF plugin temp folder: {0}", IcedAstroGrep.Core.Logging.LogClient.GetAllExceptions(ex));
			}
		}

		/// <summary>
		/// Extracts the pdf to text application to the pdf temp folder.
		/// The write is skipped when the extracted file already matches the embedded copy, and any
		/// failure is logged rather than thrown: this runs from the main form's constructor, so a
		/// locked or unwritable temp folder must not prevent the application from starting.
		/// </summary>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		/// <summary>
		/// Logical name of the pdftotext.exe embedded in this assembly: RootNamespace + folder + file,
		/// so it has to stay in step with the csproj, where the binary is a plain EmbeddedResource
		/// rather than an entry in the shell's Resources.resx.
		/// </summary>
		private const string PdfToTextResourceName = "IcedAstroGrep.AppServices.Resources.pdftotext.exe";

		private void ExtractPDFToTxtApp()
		{
			pdfToTxtAppPath = string.Empty;

			try
			{
				byte[] contents = ReadEmbeddedPdfToText();
				string targetPath = Path.Combine(GetPDFFolder(), "pdftotext.exe");

				if (!IsExtractedAppUpToDate(targetPath, contents))
				{
					File.WriteAllBytes(targetPath, contents);
				}

				pdfToTxtAppPath = targetPath;
			}
			catch (Exception ex)
			{
				IcedAstroGrep.Core.Logging.LogClient.Instance.Logger.Error("Unable to extract the pdftotext utility, the PDF plugin is unavailable: {0}", IcedAstroGrep.Core.Logging.LogClient.GetAllExceptions(ex));
			}
		}

		/// <summary>
		/// Reads the embedded pdftotext utility.
		/// </summary>
		/// <returns>Contents of the embedded utility</returns>
		/// <exception cref="InvalidOperationException">The embedded resource is missing or unreadable.</exception>
		private static byte[] ReadEmbeddedPdfToText()
		{
			using (Stream stream = typeof(PDFPlugin).Assembly.GetManifestResourceStream(PdfToTextResourceName))
			{
				if (stream == null)
				{
					throw new InvalidOperationException(string.Format("The embedded {0} resource is missing from {1}.", PdfToTextResourceName, typeof(PDFPlugin).Assembly.GetName().Name));
				}

				using (var contents = new MemoryStream())
				{
					stream.CopyTo(contents);

					return contents.ToArray();
				}
			}
		}

		/// <summary>
		/// Determines whether the already extracted utility matches the embedded copy, so an
		/// unchanged 1 MB binary is not rewritten on every start.
		/// </summary>
		/// <param name="path">Path of the extracted utility</param>
		/// <param name="contents">Embedded utility contents</param>
		/// <returns>true when the extracted file can be used as is</returns>
		private static bool IsExtractedAppUpToDate(string path, byte[] contents)
		{
			try
			{
				var extracted = new FileInfo(path);

				if (!extracted.Exists || extracted.Length != contents.Length)
				{
					return false;
				}

				byte[] existing = File.ReadAllBytes(path);
				for (int i = 0; i < contents.Length; i++)
				{
					if (existing[i] != contents[i])
					{
						return false;
					}
				}

				return true;
			}
			catch
			{
				// unreadable or locked, fall through to rewriting it
				return false;
			}
		}

		/// <summary>
		/// Extracts the text from the given pdf file.
		/// </summary>
		/// <param name="file">The <see cref="FileInfo"/> of the pdf file to extract the text from</param>
		/// <returns>The lines of text from the pdf if able to extract</returns>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		private IEnumerable<string> ExtractText(FileInfo file)
		{
			string tempFolder = GetPDFFolder();

			// include a hash of the full path so two same named PDFs from different directories can
			// never share an output file, and a leftover output can never be read as this file's
			string tempFileName = Path.Combine(tempFolder, string.Format("{0}-{1}.txt", Path.GetFileNameWithoutExtension(file.Name), GetPathHash(file.FullName)));

			try
			{
				using (Process process = new Process())
				{
					// use command prompt
					process.StartInfo.FileName = pdfToTxtAppPath;
					process.StartInfo.Arguments = string.Format("-layout \"{0}\" \"{1}\"", file.FullName, tempFileName);
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.WorkingDirectory = tempFolder;
					process.StartInfo.CreateNoWindow = true;
					// start cmd prompt, execute command
					process.Start();

					if (!process.WaitForExit(PDF_TO_TEXT_TIMEOUT_MILLISECONDS))
					{
						// cancel only takes effect between files, so an unresponsive converter has to
						// be reaped here instead of holding the search open
						TryKillProcessTree(process);

						throw new Exception(string.Format("pdftotext did not finish within {0} seconds converting '{1}'", PDF_TO_TEXT_TIMEOUT_MILLISECONDS / 1000, file.FullName));
					}

					if (process.ExitCode == 0)
					{
						if (!File.Exists(tempFileName))
							throw new Exception(string.Format("pdftotext did not generate an output file when converting '{0}'", file.FullName));
					}
					else
					{
						string errorMessage = string.Empty;
						switch (process.ExitCode)
						{
							case 1:
								errorMessage = "Error opening PDF file";
								break;

							case 2:
								errorMessage = "Error opening an output file";
								break;

							case 3:
								errorMessage = "Error related to PDF permissions";
								break;

							default:
								errorMessage = "Unknown error";
								break;
						}

						throw new Exception(string.Format("pdftotext returned '{0}' converting '{1}'", errorMessage, file.FullName));
					}
				}
			}
			catch (Exception)
			{
				// nothing is going to read the output, so do not leave it behind
				TryDeleteFile(tempFileName);
				throw;
			}

			// The lines are handed out lazily and the temporary file is removed once the caller stops
			// enumerating (including an early break), so a large conversion is never read into memory
			// as a whole.
			return ReadLinesThenDelete(tempFileName);
		}

		private static IEnumerable<string> ReadLinesThenDelete(string path)
		{
			try
			{
				using (var reader = new StreamReader(path))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						yield return line;
					}
				}
			}
			finally
			{
				TryDeleteFile(path);
			}
		}

		/// <summary>
		/// Builds a short file system safe hash of the given path.
		/// </summary>
		/// <param name="path">Path to hash</param>
		/// <returns>16 hexadecimal characters</returns>
		private static string GetPathHash(string path)
		{
			using (var algorithm = System.Security.Cryptography.SHA256.Create())
			{
				byte[] hash = algorithm.ComputeHash(Encoding.Unicode.GetBytes(path.ToUpperInvariant()));

				var builder = new StringBuilder(16);
				for (int i = 0; i < 8; i++)
				{
					builder.Append(hash[i].ToString("x2"));
				}

				return builder.ToString();
			}
		}

		/// <summary>
		/// Kills the given process and every process it started, ignoring any failure.
		/// </summary>
		/// <param name="process">Process to terminate</param>
		private static void TryKillProcessTree(Process process)
		{
			try
			{
				process.Kill(entireProcessTree: true);
				process.WaitForExit(5000);
			}
			catch (Exception ex)
			{
				IcedAstroGrep.Core.Logging.LogClient.Instance.Logger.Warn("Unable to terminate pdftotext: {0}", IcedAstroGrep.Core.Logging.LogClient.GetAllExceptions(ex));
			}
		}

		/// <summary>
		/// Deletes the given file, ignoring any failure.
		/// </summary>
		/// <param name="path">File to delete</param>
		private static void TryDeleteFile(string path)
		{
			try
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch (Exception ex)
			{
				IcedAstroGrep.Core.Logging.LogClient.Instance.Logger.Warn("Unable to delete temporary pdftotext output {0}: {1}", path, IcedAstroGrep.Core.Logging.LogClient.GetAllExceptions(ex));
			}
		}

		/// <summary>
		/// Retrieves the pdf temp folder path (and creates it if not found).
		/// </summary>
		/// <returns>The full directory path to the pdf temp folder</returns>
		/// <history>
		/// [Curtis_Beard]      09/09/2019	ADD: PDF plugin
		/// </history>
		private string GetPDFFolder()
		{
			string tempFolder = Path.Combine(Path.GetTempPath(), "IcedAstroGrep-PDF");
			if (!Directory.Exists(tempFolder))
				Directory.CreateDirectory(tempFolder);

			return tempFolder;
		}
	}
}
