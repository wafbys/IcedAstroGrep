using System;

using IcedAstroGrep.Core;
using IcedAstroGrep;

namespace IcedAstroGrep.Windows
{
   /// <summary>
   /// Used to access legacy methods for conversion or removal.
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
   /// [Curtis_Beard]		07/20/2006	Created
   /// </history>
   public class Legacy
   {
      /// <summary>
      /// Delete the registry settings for the legacy DEFAULT_EDITOR and EDITOR_ARG
      /// </summary>
      /// <history>
      /// [Curtis_Beard]		07/20/2006	Created
      /// </history>
      public static void DeleteSingleTextEditor()
      {
         try
         {
            Registry.DeleteStartupSetting("DEFAULT_EDITOR");
            Registry.DeleteStartupSetting("EDITOR_ARG");
         }
         catch {}
      }

      /// <summary>
      /// Attempt to convert search options in registry to most recent style.
      /// </summary>
      /// <remarks>
      /// Removes any registry settings for search options if found and successfully converted.
      /// </remarks>
      /// <history>
      /// [Curtis_Beard]		10/10/2006	Created
      /// [Curtis_Beard]	   03/07/2012	ADD: 3131609, exclusions
      /// [Curtis_Beard]	   11/06/2014	CHG: convert exclusionitem to filteritem
      /// </history>
      public static void ConvertSearchSettings()
      {
         if (Registry.CheckStartupSetting("USE_REG_EXPRESSIONS"))
         {
            IcedAstroGrep.SearchSettings.UseRegularExpressions = Registry.GetStartupSetting("USE_REG_EXPRESSIONS", false);
            Registry.DeleteStartupSetting("USE_REG_EXPRESSIONS");
         }

         if (Registry.CheckStartupSetting("USE_CASE_SENSITIVE"))
         {
            IcedAstroGrep.SearchSettings.UseCaseSensitivity = Registry.GetStartupSetting("USE_CASE_SENSITIVE", false);
            Registry.DeleteStartupSetting("USE_CASE_SENSITIVE");
         }

         if (Registry.CheckStartupSetting("USE_WHOLE_WORD"))
         {
            IcedAstroGrep.SearchSettings.UseWholeWordMatching = Registry.GetStartupSetting("USE_WHOLE_WORD", false);
            Registry.DeleteStartupSetting("USE_WHOLE_WORD");
         }

         if (Registry.CheckStartupSetting("USE_LINE_NUMBERS"))
         {
            IcedAstroGrep.SearchSettings.IncludeLineNumbers = Registry.GetStartupSetting("USE_LINE_NUMBERS", true);
            Registry.DeleteStartupSetting("USE_LINE_NUMBERS");
         }

         if (Registry.CheckStartupSetting("USE_RECURSION"))
         {
            IcedAstroGrep.SearchSettings.UseRecursion = Registry.GetStartupSetting("USE_RECURSION", true);
            Registry.DeleteStartupSetting("USE_RECURSION");
         }

         if (Registry.CheckStartupSetting("SHOW_FILE_NAMES_ONLY"))
         {
            IcedAstroGrep.SearchSettings.ReturnOnlyFileNames = Registry.GetStartupSetting("SHOW_FILE_NAMES_ONLY", false);
            Registry.DeleteStartupSetting("SHOW_FILE_NAMES_ONLY");
         }

         if (Registry.CheckStartupSetting("USE_NEGATION"))
         {
            IcedAstroGrep.SearchSettings.UseNegation = Registry.GetStartupSetting("USE_NEGATION", false);
            Registry.DeleteStartupSetting("USE_NEGATION");
         }

         if (Registry.CheckStartupSetting("NUM_CONTEXT_LINES"))
         {
            int lines = Registry.GetStartupSetting("NUM_CONTEXT_LINES", 0);
            if (lines < 0 || lines > Constants.MAX_CONTEXT_LINES)
               lines = 0;
            IcedAstroGrep.SearchSettings.ContextLines = lines;
            Registry.DeleteStartupSetting("NUM_CONTEXT_LINES");
         }

         var filterItems = new System.Collections.Generic.List<IcedAstroGrep.Core.FilterItem>();

         // old list to new search option
         if (!string.IsNullOrEmpty(GeneralSettings.ExtensionExcludeList))
         {
            var extensions = GeneralSettings.ExtensionExcludeList.Split(';');

            foreach (var ext in extensions)
            {
               IcedAstroGrep.Core.FilterItem item = new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.Extension), ext, IcedAstroGrep.Core.FilterType.ValueOptions.None, false, true);
               filterItems.Add(item);
            }

            // set extension exclude to list to empty
            GeneralSettings.ExtensionExcludeList = string.Empty;
         }

         // ExclusionItems to FilterItems
         if (!string.IsNullOrEmpty(SearchSettings.Exclusions))
         {
            var exclusionItems = IcedAstroGrep.Core.ExclusionItem.ConvertStringToExclusions(SearchSettings.Exclusions);
            foreach (var oldItem in exclusionItems)
            {
               IcedAstroGrep.Core.FilterItem item = new IcedAstroGrep.Core.FilterItem();
               item.Enabled = oldItem.Enabled;
               item.Value = oldItem.Value;
               item.ValueIgnoreCase = oldItem.IgnoreCase;
               switch (oldItem.Option)
               {
                  case IcedAstroGrep.Core.ExclusionItem.OptionsTypes.Contains:
                     item.ValueOption = IcedAstroGrep.Core.FilterType.ValueOptions.Contains;
                     break;

                  case IcedAstroGrep.Core.ExclusionItem.OptionsTypes.EndsWith:
                     item.ValueOption = IcedAstroGrep.Core.FilterType.ValueOptions.EndsWith;
                     break;

                  case IcedAstroGrep.Core.ExclusionItem.OptionsTypes.Equals:
                     item.ValueOption = IcedAstroGrep.Core.FilterType.ValueOptions.Equals;
                     break;

                  case IcedAstroGrep.Core.ExclusionItem.OptionsTypes.None:
                     item.ValueOption = IcedAstroGrep.Core.FilterType.ValueOptions.None;
                     break;

                  case IcedAstroGrep.Core.ExclusionItem.OptionsTypes.StartsWith:
                     item.ValueOption = IcedAstroGrep.Core.FilterType.ValueOptions.StartsWith;
                     break;
               }
               switch (oldItem.Type)
               {
                  case IcedAstroGrep.Core.ExclusionItem.ExclusionTypes.DirectoryName:
                     item.FilterType = new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.Directory, IcedAstroGrep.Core.FilterType.SubCategories.Name);
                     break;

                  case IcedAstroGrep.Core.ExclusionItem.ExclusionTypes.DirectoryPath:
                     item.FilterType = new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.Directory, IcedAstroGrep.Core.FilterType.SubCategories.Path);
                     break;

                  case IcedAstroGrep.Core.ExclusionItem.ExclusionTypes.FileExtension:
                     item.FilterType = new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.Extension);
                     break;

                  case IcedAstroGrep.Core.ExclusionItem.ExclusionTypes.FileName:
                     item.FilterType = new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.Name);
                     break;

                  case IcedAstroGrep.Core.ExclusionItem.ExclusionTypes.FilePath:
                     item.FilterType = new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.Path);
                     break;
               }
               filterItems.Add(item);
            }

            // set exclusions list to empty
            SearchSettings.Exclusions = string.Empty;
         }

         if (SearchSettings.MinimumFileCount > 0)
         {
            filterItems.Add(new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.MinimumHitCount),
               SearchSettings.MinimumFileCount.ToString(), IcedAstroGrep.Core.FilterType.ValueOptions.None, false, true));

            SearchSettings.MinimumFileCount = 0;
         }

         if (SearchSettings.SkipHidden)
         {
            filterItems.Add(new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.Hidden),
               string.Empty, IcedAstroGrep.Core.FilterType.ValueOptions.None, false, true));

            filterItems.Add(new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.Directory, IcedAstroGrep.Core.FilterType.SubCategories.Hidden),
               string.Empty, IcedAstroGrep.Core.FilterType.ValueOptions.None, false, true));

            SearchSettings.SkipHidden = false;
         }

         if (SearchSettings.SkipSystem)
         {
            filterItems.Add(new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.System),
               string.Empty, IcedAstroGrep.Core.FilterType.ValueOptions.None, false, true));

            filterItems.Add(new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.Directory, IcedAstroGrep.Core.FilterType.SubCategories.System),
               string.Empty, IcedAstroGrep.Core.FilterType.ValueOptions.None, false, true));

            SearchSettings.SkipSystem = false;
         }

         if (!string.IsNullOrEmpty(SearchSettings.MinimumFileSize))
         {
            filterItems.Add(new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.Size),
               SearchSettings.MinimumFileSize, IcedAstroGrep.Core.FilterType.ValueOptions.LessThan, false, SearchSettings.MinimumFileSizeType, true));

            SearchSettings.MinimumFileSize = string.Empty;
            SearchSettings.MinimumFileSizeType = string.Empty;
         }

         if (!string.IsNullOrEmpty(SearchSettings.MaximumFileSize))
         {
            filterItems.Add(new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.Size),
               SearchSettings.MaximumFileSize, IcedAstroGrep.Core.FilterType.ValueOptions.GreaterThan, false, SearchSettings.MaximumFileSizeType, true));

            SearchSettings.MaximumFileSize = string.Empty;
            SearchSettings.MaximumFileSizeType = string.Empty;
         }

         if (!string.IsNullOrEmpty(SearchSettings.ModifiedDateStart))
         {
            filterItems.Add(new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.DateModified),
               SearchSettings.ModifiedDateStart, IcedAstroGrep.Core.FilterType.ValueOptions.LessThan, false, true));

            SearchSettings.ModifiedDateStart = string.Empty;
         }

         if (!string.IsNullOrEmpty(SearchSettings.ModifiedDateEnd))
         {
            filterItems.Add(new IcedAstroGrep.Core.FilterItem(new IcedAstroGrep.Core.FilterType(IcedAstroGrep.Core.FilterType.Categories.File, IcedAstroGrep.Core.FilterType.SubCategories.DateModified),
               SearchSettings.ModifiedDateEnd, IcedAstroGrep.Core.FilterType.ValueOptions.GreaterThan, false, true));

            SearchSettings.ModifiedDateEnd = string.Empty;
         }

         // set filteritems list to new value
         if (filterItems.Count > 0)
         {
            SearchSettings.FilterItems = IcedAstroGrep.Core.FilterItem.ConvertFilterItemsToString(filterItems);
         }

         IcedAstroGrep.SearchSettings.Save();
      }

      /// <summary>
      /// Attempt to convert general settings in registry to most recent style.
      /// </summary>
      /// <remarks>
      /// Removes any registry settings for general settings if found and successfully converted.
      /// </remarks>
      /// <history>
      /// [Curtis_Beard]		10/10/2006	Created
      /// </history>
      public static void ConvertGeneralSettings()
      {
         // max mru
         if (Registry.CheckStartupSetting("MAX_STORED_PATHS"))
         {
            IcedAstroGrep.GeneralSettings.MaximumMRUPaths = Registry.GetStartupSetting("MAX_STORED_PATHS", 10);

            if (IcedAstroGrep.GeneralSettings.MaximumMRUPaths < 0 || IcedAstroGrep.GeneralSettings.MaximumMRUPaths > Constants.MAX_STORED_PATHS)
               IcedAstroGrep.GeneralSettings.MaximumMRUPaths = Constants.MAX_STORED_PATHS;

            Registry.DeleteStartupSetting("MAX_STORED_PATHS");
         }

         // mru values
         ConvertMRUSettings();

         // window settings
         // column widths
         // splitter positions
         ConvertWindowSettings();

         // colors
         ConvertResultColors();

         // exclude list
         if (Registry.CheckStartupSetting("ExtensionExcludeList"))
         {
            IcedAstroGrep.GeneralSettings.ExtensionExcludeList = Registry.GetStartupSetting("ExtensionExcludeList", string.Empty);
            Registry.DeleteStartupSetting("ExtensionExcludeList");
         }

         // language
         if (Registry.CheckStartupSetting("Language"))
         {
            IcedAstroGrep.GeneralSettings.Language = Registry.GetStartupSetting("Language", Constants.DEFAULT_LANGUAGE);
            Registry.DeleteStartupSetting("Language");
         }

         IcedAstroGrep.GeneralSettings.Save();
      }

      /// <summary>
      /// Retrieve the registry values for the text editors and return a Text
      /// </summary>
      /// <returns>TextEditor array, null if values don't exist</returns>
      /// <history>
      /// [Curtis_Beard]		10/10/2006	Created
      /// </history>
      public static TextEditor[] ConvertTextEditors()
      {
         TextEditor[] editors = null;

         string mruValue = Registry.GetRegistrySetting("TextEditors", "MRUList", string.Empty);

         if (mruValue.Length > 0)
         {
            string display = string.Empty;
            string path = string.Empty;
            string args = string.Empty;
            int max = int.Parse(mruValue);

            editors = new TextEditor[max];

            for (int i = 1; i <= max; i++)
            {
               path = Registry.GetRegistrySetting("TextEditors", i.ToString(), "-1");
               args = Registry.GetRegistrySetting("TextEditors", i.ToString() + "_args", "-1");
               display = Registry.GetRegistrySetting("TextEditors", i.ToString() + "_fileType", string.Empty);

               if (!path.Equals("-1") && !args.Equals("-1"))
                  editors[i - 1] = new TextEditor(display, path, args);
            }

            Registry.DeleteRegistrySetting("TextEditors");
         }
         else
         {
            // try legacy, if exist, update and remove
            string editorPath = Registry.GetStartupSetting("DEFAULT_EDITOR");
            string editorArgs = Registry.GetStartupSetting("EDITOR_ARG");

            if (editorPath.Length > 0)
            {
               TextEditor editor = new TextEditor();
               editor.FileType = "*";
               editor.Editor = editorPath;
               editor.Arguments = editorArgs;

               editors = new TextEditor[1];
               editors[0] = editor;

               // remove legacy from registry
               DeleteSingleTextEditor();
            }
         }

         return editors;
      }

      /// <summary>
      /// Deletes all registry entries for this application.
      /// </summary>
      /// <history>
      /// [Curtis_Beard]		10/10/2006	Created
      /// </history>
      public static void DeleteRegistry()
      {
         Registry.DeleteAllRegistry();
      }

		/// <summary>
		/// Convert the old language value to the new culture based values.
		/// </summary>
		/// <history>
		/// [Curtis_Beard]		05/22/2007  Created
		/// [Curtis_Beard]		08/30/2007  ADD: support for installer language values
        /// [Curtis_Beard]		01/24/2012  CHG: remove default case
        /// [Curtis_Beard]		05/08/2014  CHG: 70, installer support
		/// </history>
		public static void ConvertLanguageValue()
		{
			switch (GeneralSettings.Language)
			{
				case "Espa�ol":
				case "1034":
					GeneralSettings.Language = "es-es";
					break;

				case "Deutsch":
				case "1031":
					GeneralSettings.Language = "de-de";
					break;

				case "Italiano":
				case "1040":
					GeneralSettings.Language = "it-it";
					break;

				case "Danish":
				case "Dansk":
				case "1030":
					GeneralSettings.Language = "da-dk";
					break;

				case "English":
				case "1033":
					GeneralSettings.Language = "en-us";
					break;

				case "French":
				case "Fran�ais":
				case "1036":
					GeneralSettings.Language = "fr-fr";
					break;

				case "Polski":
				case "1045":
					GeneralSettings.Language = "pl-pl";
					break;
			}

			GeneralSettings.Save();
		}

      #region Private Methods
      /// <summary>
      /// Convert and remove registry entries pertaining to MRU lists.
      /// </summary>
      /// <history>
      /// [Curtis_Beard]		10/10/2006	Created
      /// </history>
      private static void ConvertMRUSettings()
      {
         string _registryValue;
         string _indexNum;
         System.Text.StringBuilder sbPaths = new System.Text.StringBuilder(IcedAstroGrep.GeneralSettings.MaximumMRUPaths);
         System.Text.StringBuilder sbFilters = new System.Text.StringBuilder(IcedAstroGrep.GeneralSettings.MaximumMRUPaths);
         System.Text.StringBuilder sbSearches = new System.Text.StringBuilder(IcedAstroGrep.GeneralSettings.MaximumMRUPaths);

         //  Get the MRU Paths and add them to the path combobox.
         for (int i = 0; i < IcedAstroGrep.GeneralSettings.MaximumMRUPaths; i++)
         {
            _indexNum = i.ToString();

            //  Get the most recent start pathes
            _registryValue = Registry.GetStartupSetting("MRUPath" + _indexNum);

            //  Add the path to the path combobox.
            if (!_registryValue.Equals(string.Empty))
            {
               if (sbPaths.Length > 0)
                  sbPaths.Append(Constants.SEARCH_ENTRIES_SEPARATOR);

               sbPaths.Append(_registryValue);

               Registry.DeleteStartupSetting("MRUPath" + _indexNum);
            }

            //  Get the most recent File filters
            _registryValue = Registry.GetStartupSetting("MRUFileName" + _indexNum);

            //  Add the file name to the path combobox.
            if (!_registryValue.Equals(string.Empty))
            {
               if (sbFilters.Length > 0)
                  sbFilters.Append(Constants.SEARCH_ENTRIES_SEPARATOR);

               sbFilters.Append(_registryValue);

               Registry.DeleteStartupSetting("MRUFileName" + _indexNum);
            }

            //  Get the most recent search expressions
            _registryValue = Registry.GetStartupSetting("MRUExpression" + _indexNum);

            //  Add the search expression to the path combobox.
            if (!_registryValue.Equals(string.Empty))
            {
               if (sbSearches.Length > 0)
                  sbSearches.Append(Constants.SEARCH_ENTRIES_SEPARATOR);

               sbSearches.Append(_registryValue);

               Registry.DeleteStartupSetting("MRUExpression" + _indexNum);
            }            
         }

         if (sbPaths.Length > 0)
            IcedAstroGrep.GeneralSettings.SearchStarts = sbPaths.ToString();

         if (sbFilters.Length > 0)
            IcedAstroGrep.GeneralSettings.SearchFilters = sbFilters.ToString();

         if (sbSearches.Length > 0)
            IcedAstroGrep.GeneralSettings.SearchTexts = sbSearches.ToString();
      }

      /// <summary>
      /// Convert and remove registry entries pertaining to window settings.
      /// </summary>
      /// <history>
      /// [Curtis_Beard]		10/10/2006	Created
      /// </history>
      private static void ConvertWindowSettings()
      {
         if (Registry.CheckStartupSetting("POS_TOP"))
         {
            IcedAstroGrep.GeneralSettings.WindowTop = Registry.GetStartupSetting("POS_TOP", -1);
            Registry.DeleteStartupSetting("POS_TOP");
         }
         if (Registry.CheckStartupSetting("POS_LEFT"))
         {
            IcedAstroGrep.GeneralSettings.WindowLeft = Registry.GetStartupSetting("POS_LEFT", -1);
            Registry.DeleteStartupSetting("POS_LEFT");
         }
         if (Registry.CheckStartupSetting("POS_WIDTH"))
         {
            IcedAstroGrep.GeneralSettings.WindowWidth = Registry.GetStartupSetting("POS_WIDTH", -1);
            Registry.DeleteStartupSetting("POS_WIDTH");
         }
         if (Registry.CheckStartupSetting("POS_HEIGHT"))
         {
            IcedAstroGrep.GeneralSettings.WindowHeight = Registry.GetStartupSetting("POS_HEIGHT", -1);
            Registry.DeleteStartupSetting("POS_HEIGHT");
         }
         if (Registry.CheckStartupSetting("POS_STATE"))
         {
            IcedAstroGrep.GeneralSettings.WindowState = Registry.GetStartupSetting("POS_STATE", -1);
            Registry.DeleteStartupSetting("POS_STATE");
         }

         if (Registry.CheckStartupSetting("SEARCH_PANEL_WIDTH"))
         {
            IcedAstroGrep.GeneralSettings.WindowSearchPanelWidth = Registry.GetStartupSetting("SEARCH_PANEL_WIDTH", -1);
            Registry.DeleteStartupSetting("SEARCH_PANEL_WIDTH");
         }
         if (Registry.CheckStartupSetting("FILE_PANEL_HEIGHT"))
         {
            IcedAstroGrep.GeneralSettings.WindowFilePanelHeight = Registry.GetStartupSetting("FILE_PANEL_HEIGHT", -1);
            Registry.DeleteStartupSetting("FILE_PANEL_HEIGHT");
         }

         if (Registry.CheckStartupSetting("FILELIST_FILENAME_WIDTH"))
         {
            IcedAstroGrep.GeneralSettings.WindowFileColumnNameWidth = Registry.GetStartupSetting("FILELIST_FILENAME_WIDTH", 100);
            Registry.DeleteStartupSetting("FILELIST_FILENAME_WIDTH");
         }
         if (Registry.CheckStartupSetting("FILELIST_LOCATED_IN_WIDTH"))
         {
            IcedAstroGrep.GeneralSettings.WindowFileColumnLocationWidth = Registry.GetStartupSetting("FILELIST_LOCATED_IN_WIDTH", 200);
            Registry.DeleteStartupSetting("FILELIST_LOCATED_IN_WIDTH");
         }
         if (Registry.CheckStartupSetting("FILELIST_DATE_MODIFIED_WIDTH"))
         {
            IcedAstroGrep.GeneralSettings.WindowFileColumnDateWidth = Registry.GetStartupSetting("FILELIST_DATE_MODIFIED_WIDTH", 150);
            Registry.DeleteStartupSetting("FILELIST_DATE_MODIFIED_WIDTH");
         }
         if (Registry.CheckStartupSetting("FILELIST_COUNT_WIDTH"))
         {
            IcedAstroGrep.GeneralSettings.WindowFileColumnCountWidth = Registry.GetStartupSetting("FILELIST_COUNT_WIDTH", 60);
            Registry.DeleteStartupSetting("FILELIST_COUNT_WIDTH");
         }
      }

      /// <summary>
      /// Convert and remove registry entries pertaining to result colors.
      /// </summary>
      /// <history>
      /// [Curtis_Beard]		10/10/2006	Created
      /// </history>
      private static void ConvertResultColors()
      {
         if (Registry.CheckStartupSetting("HighlightForeColor"))
         {
            IcedAstroGrep.GeneralSettings.HighlightForeColor = Convertors.ConvertColorToString(Registry.GetStartupSetting("HighlightForeColor", ProductInformation.ApplicationColor));
            Registry.DeleteStartupSetting("HighlightForeColor");
         }
         if (Registry.CheckStartupSetting("HighlightBackColor"))
         {
            IcedAstroGrep.GeneralSettings.HighlightBackColor = Convertors.ConvertColorToString(Registry.GetStartupSetting("HighlightBackColor", System.Drawing.SystemColors.Window));
            Registry.DeleteStartupSetting("HighlightBackColor");
         }
         if (Registry.CheckStartupSetting("ResultsForeColor"))
         {
            IcedAstroGrep.GeneralSettings.ResultsForeColor = Convertors.ConvertColorToString(Registry.GetStartupSetting("ResultsForeColor", System.Drawing.SystemColors.WindowText));
            Registry.DeleteStartupSetting("ResultsForeColor");
         }
         if (Registry.CheckStartupSetting("ResultsBackColor"))
         {
            IcedAstroGrep.GeneralSettings.ResultsBackColor = Convertors.ConvertColorToString(Registry.GetStartupSetting("ResultsBackColor", System.Drawing.SystemColors.Window));
            Registry.DeleteStartupSetting("ResultsBackColor");
         }
      }
      #endregion
   }
}