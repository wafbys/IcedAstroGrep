using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using IcedAstroGrep.Core;
using IcedAstroGrep.Core.Logging;


namespace IcedAstroGrep
{
	/// <summary>
	/// Used to retrieve language specific text for controls and generic messages.
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
	/// <history>
	/// [Curtis_Beard]      07/31/2006	Created
	/// [Curtis_Beard]		02/28/2020	CHG: .Net 4.5 cleanup
	/// </history>
	public class Language
	{
		private static readonly Dictionary<string, string> genericTextDictionary = new Dictionary<string, string>();

		private static XmlNode __RootNode = null;

		private static XmlDocument __XmlDoc = null;

		private static List<LanguageItem> internalLanguages = null;

		/// <summary>
		/// Initializes an instance of the Language class.
		/// </summary>
		private Language()
		{ }

		/// <summary>
		/// Gets the location of all the language files.
		/// </summary>
		/// <history>
		/// [Curtis_Beard]		05/22/2007	Created
		/// </history>
		/// <summary>
		/// Root of the loaded language document, so a shell can look text up by node path.
		/// </summary>
		/// <remarks>
		/// The document keys are "&lt;FormName&gt;/&lt;ControlName&gt;/value", which is how the WinForms shell
		/// localizes controls by name. A shell with its own naming can ignore this and call
		/// <see cref="GetGenericText(string)"/> instead.
		/// </remarks>
		public static XmlNode TextRoot
		{
			get { return __RootNode; }
		}
		/// <summary>
		/// The languages a shell can offer: the ones shipped inside this assembly plus any external
		/// language file found beside the executable.
		/// </summary>
		/// <remarks>Reading this also loads the shipped list, so a shell can offer the choices before it
		/// has loaded a language.</remarks>
		public static List<LanguageItem> AvailableLanguages
		{
			get
			{
				LoadInternalLanguages();

				var items = new List<LanguageItem>();
				items.AddRange(internalLanguages);
				items.AddRange(GetExternalLanguages());

				return items;
			}
		}
		public static string LanguageLocation
		{
			get
			{
				return Path.Combine(ApplicationPaths.DataFolder, "Language");
			}
		}

		/// <summary>
		/// Gets a string value from the generic text section of a language file.
		/// </summary>
		/// <param name="name">Key name to retrieve</param>
		/// <returns>string containing text or string.empty if not found</returns>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		public static string GetGenericText(string name)
		{
			return GetGenericText(name, string.Empty);
		}

		/// <summary>
		/// Gets a string value from the generic text section of a language file.
		/// </summary>
		/// <param name="name">Key name to retrieve</param>
		/// <param name="defaultValue">Default value to return if not found</param>
		/// <returns>string containing text or given default value if not found</returns>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// [Curtis_Beard]		08/15/2017	PAT: 5, use dictionary instead of xpath for generic text
		/// </history>
		public static string GetGenericText(string name, string defaultValue)
		{
			if (genericTextDictionary != null && genericTextDictionary.ContainsKey(name))
			{
				return genericTextDictionary[name];
			}

			return defaultValue;
		}

		/// <summary>
		/// Loads the given language's file.
		/// </summary>
		/// <param name="culture">String containing current cultrue to load</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// [Curtis_Beard]		10/11/2006	CHG: Close stream
		/// [Curtis_Beard]		06/15/205	CHG: 57, support external language files
		/// [LinkNet]				04/24/2017	CHG: Remove unused "language" parameter name
		/// </history>
		public static void Load(string culture)
		{
			LoadInternalLanguages();

			if (!LoadInternal(culture))
			{
				if (!LoadExternal(culture))
				{
					// failed to load internal and external, so try default
					if (LoadInternal(Constants.DEFAULT_LANGUAGE))
					{
						// success in loading default, make sure to update settings value
						GeneralSettings.Language = Constants.DEFAULT_LANGUAGE;
					}
				}
			}
		}

		/// <summary>
		/// Check the given LanguageItem list for a given culture.
		/// </summary>
		/// <param name="items">List to check against</param>
		/// <param name="culture">Culture to check</param>
		/// <returns>true if list contains culture, false otherwise</returns>
		/// <history>
		/// [Curtis_Beard]		06/15/2015	CHG: 57, support external language files
		/// </history>
		private static bool DoesExistInList(List<LanguageItem> items, string culture)
		{
			if (items != null && !string.IsNullOrEmpty(culture))
			{
				foreach (var item in items)
				{
					if (item.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Retrieve all external language files from LanguageLocation property.
		/// </summary>
		/// <returns>List of all external language items</returns>
		/// <history>
		/// [Curtis_Beard]		06/15/2015	CHG: 57, support external language files
		/// </history>
		private static List<LanguageItem> GetExternalLanguages()
		{
			var items = new List<LanguageItem>();

			try
			{
				if (Directory.Exists(LanguageLocation))
				{
					string[] files = Directory.GetFiles(LanguageLocation, "*.xml");

					if (files.Length > 0)
					{
						foreach (string file in files)
						{
							var item = GetLanguageItemFromFile(file);
							if (item != null && !DoesExistInList(internalLanguages, item.Culture) && !DoesExistInList(items, item.Culture))
							{
								items.Add(item);
							}
							else
							{
								LogClient.Instance.Logger.Info("External language already exists {0}", item != null ? item.Culture : string.Empty);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogClient.Instance.Logger.Error("Unable to retrieve external languages {0}", LogClient.GetAllExceptions(ex));
			}

			return items;
		}

		/// <summary>
		/// Retrieve the language information from the file specified.
		/// </summary>
		/// <param name="path">Full path to language file</param>
		/// <returns>LanguageItem object containing file information, otherwise null</returns>
		/// <history>
		/// [Curtis_Beard]		06/15/2015	CHG: 57, support external language files
		/// </history>
		private static LanguageItem GetLanguageItemFromFile(string path)
		{
			LanguageItem item = null;

			try
			{
				if (File.Exists(path))
				{
					XmlDocument doc = new XmlDocument();

					doc.Load(path);

					XmlNode root = doc.SelectSingleNode("language");

					if (root != null && root.Attributes.Count > 0)
					{
						string displayName = string.Empty;
						string culture = string.Empty;

						if (root.Attributes["displayName"] != null)
							displayName = root.Attributes["displayName"].Value;

						if (root.Attributes["culture"] != null)
							culture = root.Attributes["culture"].Value;

						if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(culture))
						{
							item = new LanguageItem(displayName, culture, true, path);
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogClient.Instance.Logger.Error("Unable to retrieve external language information from file {0}, {1}", path, LogClient.GetAllExceptions(ex));
			}

			return item;
		}

		/// <summary>
		/// Attempts to load an external language file for the given culture.
		/// </summary>
		/// <param name="culture">Culture to load</param>
		/// <returns>true on success, false otherwise</returns>
		/// <history>
		/// [Curtis_Beard]		06/15/2015	CHG: 57, support external language files
		/// [Curtis_Beard]		08/15/2017	PAT: 5, use dictionary instead of xpath for generic text
		/// </history>
		private static bool LoadExternal(string culture)
		{
			try
			{
				if (Directory.Exists(LanguageLocation))
				{
					var items = GetExternalLanguages();
					if (items != null && items.Count > 0)
					{
						var item = (from i in items where i.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase) select i).FirstOrDefault();
						if (item != null)
						{
							// found external language match, load content from file
							__XmlDoc = new XmlDocument();
							__XmlDoc.Load(item.ExternalFilePath);

							XmlNode root = __XmlDoc.SelectSingleNode("language");

							if (root != null && root.Attributes.Count > 0)
							{
								__RootNode = root;

								if (__RootNode != null)
								{
									var genericNode = __RootNode.SelectSingleNode("generic");

									if (genericNode != null)
									{
										genericTextDictionary.Clear();
										var textNodes = genericNode.SelectNodes("text");
										foreach (XmlNode node in textNodes)
										{
											genericTextDictionary.Add(node.Attributes["name"].Value, node.Attributes["value"].Value);
										}

										return true;
									}
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogClient.Instance.Logger.Error("Error loading external language {0}, {1}", culture, LogClient.GetAllExceptions(ex));
			}

			return false;
		}

		/// <summary>
		/// Attempts to load an internal language file for the given culture.
		/// </summary>
		/// <param name="culture">Culture to load</param>
		/// <returns>true on success, false otherwise</returns>
		/// <history>
		/// [Curtis_Beard]		06/15/2015	CHG: 57, support external language files
		/// [Curtis_Beard]		08/15/2017	PAT: 5, use dictionary instead of xpath for generic text
		/// </history>
		private static bool LoadInternal(string culture)
		{
			try
			{
				System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
				string _name = assembly.GetName().Name;

				using (Stream stream = assembly.GetManifestResourceStream(string.Format("{0}.Language.{1}.xml", _name, culture)))
				{
					if (stream != null)
					{
						string contents = string.Empty;

						using (StreamReader _reader = new StreamReader(stream))
						{
							contents = _reader.ReadToEnd();
						}

						stream.Close();

						if (!contents.Equals(string.Empty))
						{
							__XmlDoc = new XmlDocument();
							__XmlDoc.LoadXml(contents);

							__RootNode = __XmlDoc.SelectSingleNode("language");

							if (__RootNode != null)
							{
								var genericNode = __RootNode.SelectSingleNode("generic");

								if (genericNode != null)
								{
									genericTextDictionary.Clear();
									var textNodes = genericNode.SelectNodes("text");
									foreach (XmlNode node in textNodes)
									{
										genericTextDictionary.Add(node.Attributes["name"].Value, node.Attributes["value"].Value);
									}

									return true;
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogClient.Instance.Logger.Error("Error loading internal language {0}, {1}", culture, LogClient.GetAllExceptions(ex));
			}

			return false;
		}

		private static void LoadInternalLanguages()
		{
			if (internalLanguages == null)
			{
				internalLanguages = new List<LanguageItem>
				{
					// load our internally defined languages
					new LanguageItem("English", "en-us"),
					new LanguageItem("Fran�ais", "fr-fr"),
					new LanguageItem("Espa�ol", "es-es"),
					new LanguageItem("Deutsch", "de-de"),
					new LanguageItem("Italiano", "it-it"),
					new LanguageItem("Dansk", "da-dk"),
					new LanguageItem("Polski", "pl-pl")
				};
			}
		}

	}

	/// <summary>
	/// Used to contain a language file.
	/// </summary>
	/// <history>
	/// [Curtis_Beard]		05/22/2007	Created
	/// [Curtis_Beard]		06/15/2015	CHG: 57, support external language files
	/// </history>
	public class LanguageItem
	{
		/// <summary>
		/// Creates a new instance of the LanguageItem class.
		/// </summary>
		/// <param name="displayName">Display name</param>
		/// <param name="culture">Culture string</param>
		/// <history>
		/// [Curtis_Beard]		05/22/2007	Created
		/// [Curtis_Beard]		06/15/2015	CHG: 57, support external language files
		/// </history>
		public LanguageItem(string displayName, string culture)
		{
			DisplayName = displayName;
			Culture = culture;
			IsExternal = false;
			ExternalFilePath = null;
		}

		/// <summary>
		/// Creates a new instance of the LanguageItem class.
		/// </summary>
		/// <param name="displayName">Display name</param>
		/// <param name="culture">Culture string</param>
		/// <param name="isExternal">Language is from external file</param>
		/// <param name="filePath">File path to language file</param>
		/// <history>
		/// [Curtis_Beard]		06/15/2015	CHG: 57, support external language files
		/// </history>
		public LanguageItem(string displayName, string culture, bool isExternal, string filePath)
		   : this(displayName, culture)
		{
			IsExternal = isExternal;

			if (IsExternal)
			{
				ExternalFilePath = filePath;
			}
		}

		/// <summary>
		/// Gets/Sets the language's culture string.
		/// </summary>
		public string Culture
		{
			get;
			set;
		}

		/// <summary>
		/// Gets/Sets the language's display name.
		/// </summary>
		public string DisplayName
		{
			get;
			set;
		}

		/// <summary>
		/// Gets/Sets the external language's file path.
		/// </summary>
		/// <remarks>
		/// Will be null if internal
		/// </remarks>
		public string ExternalFilePath
		{
			get;
			set;
		}

		/// <summary>
		/// Gets/Sets whether language is from external file.
		/// </summary>
		public bool IsExternal
		{
			get;
			set;
		}
	}

}
