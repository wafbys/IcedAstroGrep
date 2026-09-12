using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using IcedAstroGrep.Core;

// The menu walking below deliberately handles both kinds of menu: the shell still builds some menus
// from the legacy MainMenu / MenuItem API and others from MainMenuStrip, so the localizer has to cope
// with both. WFDEV006 only asks for the modern API, and migrating the shell's menus is a UI change
// that belongs with modernizing the shell, not with localization.
#pragma warning disable WFDEV006

namespace IcedAstroGrep.Windows
{
	/// <summary>
	/// Applies the loaded language text to WinForms controls, menus and tool strips.
	/// </summary>
	/// <remarks>
	/// The text itself (key lookup, the language files and loading them) lives in <see cref="Language"/>, which
	/// moved to IcedAstroGrep.AppServices so every shell can show the same wording. Everything here takes
	/// WinForms types and therefore stays with this shell.
	/// </remarks>
	public static class WinFormsLocalization
	{
		/// <summary>
		/// Generates an xml document for the given form with all controls.
		/// </summary>
		/// <param name="frm">Form to process</param>
		/// <param name="path">Fully qualified file path</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		public static void GenerateXml(Form frm, string path)
		{
			XmlDocument xmlDoc = new XmlDocument();
			XmlNode rootNode;
			XmlAttribute attrib;

			try
			{
				xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "utf-8", "yes"));

				rootNode = xmlDoc.CreateElement("screen");
				attrib = xmlDoc.CreateAttribute("name");
				attrib.Value = frm.Name;
				rootNode.Attributes.Append(attrib);
				attrib = xmlDoc.CreateAttribute("value");
				attrib.Value = frm.Text;
				rootNode.Attributes.Append(attrib);

				//process all controls on form
				foreach (Control control in frm.Controls)
					GenerateXmlControls(control, rootNode, xmlDoc);

				xmlDoc.AppendChild(rootNode);

				//process menu items on form
				if (frm.Menu != null)
				{
					foreach (MenuItem item in frm.Menu.MenuItems)
					{
						XmlNode menuNode = xmlDoc.CreateElement("menu");

						attrib = xmlDoc.CreateAttribute("index");
						attrib.Value = item.Index.ToString();
						menuNode.Attributes.Append(attrib);

						attrib = xmlDoc.CreateAttribute("value");
						attrib.Value = item.Text;
						menuNode.Attributes.Append(attrib);

						GenerateXmlMenuItems(item, menuNode, xmlDoc);

						rootNode.AppendChild(menuNode);
					}
				}
				xmlDoc.Save(path);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		/// <summary>
		/// Retrieve the control's text value in the loaded language file.
		/// </summary>
		/// <param name="control">Control to set</param>
		/// <returns>value of control's text in language file</returns>
		/// <history>
		/// [Curtis_Beard]		11/02/2006	Created
		/// </history>
		public static string GetControlText(Control control)
		{
			if (Language.TextRoot != null)
			{
				string formName = control.FindForm().Name;
				XmlNode node = Language.TextRoot.SelectSingleNode("screen[@name='" + formName + "']");
				XmlNode controlNode;

				if (node != null)
				{
					//node found, find control
					controlNode = node.SelectSingleNode("control[@name='" + control.Name + "']");

					if (controlNode != null)
					{
						//text
						if (controlNode.Attributes["value"] != null)
							return controlNode.Attributes["value"].Value;
					}
				}
			}

			return string.Empty;
		}

		/// <summary>
		/// Retrieve the control's tooltip text value in the loaded language file.
		/// </summary>
		/// <param name="control">Control to set</param>
		/// <returns>value of control's tooltip text in language file</returns>
		/// <history>
		/// [Curtis_Beard]		08/16/2016	Created
		/// </history>
		public static string GetControlToolTipText(Control control)
		{
			if (Language.TextRoot != null)
			{
				string formName = control.FindForm().Name;
				XmlNode node = Language.TextRoot.SelectSingleNode("screen[@name='" + formName + "']");
				XmlNode controlNode;

				if (node != null)
				{
					//node found, find control
					controlNode = node.SelectSingleNode("control[@name='" + control.Name + "']");

					if (controlNode != null)
					{
						//text
						if (controlNode.Attributes["tooltip"] != null)
							return controlNode.Attributes["tooltip"].Value;
					}
				}
			}

			return string.Empty;
		}

		/// <summary>
		/// Retrieve the control's tooltip text value in the loaded language file.
		/// </summary>
		/// <param name="control">Control to set</param>
		/// <returns>value of control's tooltip text in language file</returns>
		/// <history>
		/// [Curtis_Beard]		08/16/2016	Created
		/// </history>
		public static string GetControlToolTipText(ToolStripItem control)
		{
			if (Language.TextRoot != null)
			{
				string formName = control.Owner.FindForm().Name;
				XmlNode node = Language.TextRoot.SelectSingleNode("screen[@name='" + formName + "']");
				XmlNode controlNode;

				if (node != null)
				{
					//node found, find control
					controlNode = node.SelectSingleNode("control[@name='" + control.Name + "']");

					if (controlNode != null)
					{
						//text
						if (controlNode.Attributes["tooltip"] != null)
							return controlNode.Attributes["tooltip"].Value;
					}
				}
			}

			return string.Empty;
		}

		/// <summary>
		/// Loads the given ComboBox with the available languages.
		/// </summary>
		/// <param name="combo">ComboBox to load</param>
		/// <history>
		/// [Curtis_Beard]		05/18/2007	Created
		/// [Curtis_Beard]		06/15/2015	CHG: add polish language entry
		/// [Curtis_Beard]		06/15/2015	CHG: 57, support external language files
		/// </history>
		public static void LoadComboBox(ComboBox combo)
		{
			combo.Items.Clear();
			combo.DisplayMember = "DisplayName";
			combo.ValueMember = "Culture";

			// the shipped languages plus any external file beside the executable
			combo.Items.AddRange(Language.AvailableLanguages.ToArray());
		}

		/// <summary>
		/// Processes the given form for all controls.
		/// </summary>
		/// <param name="frm">Form to process</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		public static void ProcessForm(Form frm)
		{
			ProcessForm(frm, null);
		}

		/// <summary>
		/// Processes the given form for all controls.
		/// </summary>
		/// <param name="frm">Form to process</param>
		/// <param name="tip">ToolTip control</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		public static void ProcessForm(Form frm, ToolTip tip)
		{
			if (Language.TextRoot != null)
			{
				SetFormText(frm);

				//process controls on form
				foreach (Control control in frm.Controls)
				{
					if (control.GetType() == typeof(ToolStrip) || control.GetType() == typeof(StatusStrip))
						ProcessToolStrip((ToolStrip)control, tip);
					else
						ProcessControl(control, tip);
				}

				//process menu items on form
				if (frm.Menu != null)
				{
					foreach (MenuItem item in frm.Menu.MenuItems)
					{
						XmlNode menuNode = Language.TextRoot.SelectSingleNode("screen[@name='" + frm.Name + "']/menu[@index='" + item.Index + "']");

						if (menuNode != null && menuNode.Attributes["value"] != null)
						{
							item.Text = menuNode.Attributes["value"].Value;

							ProcessMainMenuItem(item);
						}
					}
				}

				//process menu strip items on form
				if (frm.MainMenuStrip != null)
				{
					for (int i = 0; i < frm.MainMenuStrip.Items.Count; i++)
					{
						ToolStripMenuItem item = frm.MainMenuStrip.Items[i] as ToolStripMenuItem;
						XmlNode menuNode = Language.TextRoot.SelectSingleNode("screen[@name='" + frm.Name + "']/menu[@index='" + i + "']");

						if (menuNode != null && menuNode.Attributes["value"] != null)
						{
							item.Text = menuNode.Attributes["value"].Value;
							List<int> indexes = new List<int>
							{
								i
							};
							ProcessMainToolStripMenuItem(item, indexes);
						}
					}
				}
			}
		}

		/// <summary>
		/// Sets the given context menuitem's text property.
		/// </summary>
		/// <param name="holder">Control containing ContextMenu</param>
		/// <param name="item">MenuItem to set</param>
		/// <history>
		/// [Curtis_Beard]		02/01/2012	Created
		/// </history>
		public static void SetContextMenuItemText(Control holder, MenuItem item)
		{
			SetContextMenuItemText(holder, item, null);
		}

		/// <summary>
		/// Sets the given context menu strip item's text property.
		/// </summary>
		/// <param name="holder">Control containing ContextMenu</param>
		/// <param name="item"><see cref="ToolStripMenuItem"/> to set</param>
		/// <param name="itemIndex"></param>
		/// <history>
		/// [Curtis_Beard]		08/25/2022	Created
		/// </history>
		public static void SetContextMenuStripItemText(Control holder, ToolStripMenuItem item, int itemIndex)
		{
			SetContextMenuStripItemText(holder, item, null, itemIndex, -1);
		}

		/// <summary>
		/// Sets the given context menuitem's text property or sub menuitem if specified.
		/// </summary>
		/// <param name="holder">Control containing ContextMenu</param>
		/// <param name="item">MenuItem</param>
		/// <param name="subItem">MenuItem's child MenuItem to set</param>
		/// <history>
		/// [Curtis_Beard]		09/18/2013	ADD: 65, support for contextmenu sub items
		/// </history>
		public static void SetContextMenuItemText(Control holder, MenuItem item, MenuItem subItem)
		{
			if (Language.TextRoot != null)
			{
				string formName = GetParentControl(holder).Name;
				XmlNode node;
				if (subItem == null)
				{
					node = Language.TextRoot.SelectSingleNode("screen[@name='" + formName + "']/control[@name='" + holder.Name + "']/menuitem[@index='" + item.Index + "']");
				}
				else
				{
					node = Language.TextRoot.SelectSingleNode("screen[@name='" + formName + "']/control[@name='" + holder.Name + "']/menuitem[@index='" + item.Index + "']/menuitem[@index='" + subItem.Index + "']");
				}

				if (node != null)
				{
					if (node.Attributes["value"] != null)
					{
						if (subItem == null)
						{
							item.Text = node.Attributes["value"].Value;
						}
						else
						{
							subItem.Text = node.Attributes["value"].Value;
						}
					}
				}
			}
		}

		/// <summary>
		/// Sets the given context menu strip item's text property or sub <see cref="ToolStripMenuItem"/> if specified.
		/// </summary>
		/// <param name="holder">Control containing ContextMenu</param>
		/// <param name="item"><see cref="ToolStripMenuItem"/></param>
		/// <param name="subItem"><see cref="ToolStripMenuItem"/>'s child <see cref="ToolStripMenuItem"/> to set</param>
		/// <param name="itemIndex"></param>
		/// <param name="subItemIndex"></param>
		/// <history>
		/// [Curtis_Beard]		08/25/2022	Created
		/// </history>
		public static void SetContextMenuStripItemText(Control holder, ToolStripMenuItem item, ToolStripMenuItem subItem, int itemIndex, int subItemIndex)
		{
			if (Language.TextRoot != null)
			{
				string formName = GetParentControl(holder).Name;
				XmlNode node;
				if (subItem == null)
				{
					node = Language.TextRoot.SelectSingleNode("screen[@name='" + formName + "']/control[@name='" + holder.Name + "']/menuitem[@index='" + itemIndex + "']");
				}
				else
				{
					node = Language.TextRoot.SelectSingleNode("screen[@name='" + formName + "']/control[@name='" + holder.Name + "']/menuitem[@index='" + itemIndex + "']/menuitem[@index='" + subItemIndex + "']");
				}

				if (node != null)
				{
					if (node.Attributes["value"] != null)
					{
						if (subItem == null)
						{
							item.Text = node.Attributes["value"].Value;
						}
						else
						{
							subItem.Text = node.Attributes["value"].Value;
						}
					}
				}
			}
		}

		/// <summary>
		/// Sets the given control's text property.
		/// </summary>
		/// <param name="control">Control to set</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		public static void SetControlText(Control control)
		{
			SetControlText(control, null);
		}

		/// <summary>
		/// Sets the given control's text property.
		/// </summary>
		/// <param name="control">Control to set</param>
		/// <param name="tip">ToolTip control</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		public static void SetControlText(Control control, ToolTip tip)
		{
			if (Language.TextRoot != null)
			{
				string formName = control.FindForm().Name;
				XmlNode node = Language.TextRoot.SelectSingleNode("screen[@name='" + formName + "']");
				XmlNode controlNode;

				if (node != null)
				{
					//node found, find control
					controlNode = node.SelectSingleNode("control[@name='" + control.Name + "']");

					if (controlNode != null)
					{
						//found control node

						//text
						if (controlNode.Attributes["value"] != null)
							control.Text = controlNode.Attributes["value"].Value;

						//tooltip
						if (tip != null && controlNode.Attributes["tooltip"] != null)
							tip.SetToolTip(control, controlNode.Attributes["tooltip"].Value);
					}
				}
			}
		}

		/// <summary>
		/// Set a given form's text property.
		/// </summary>
		/// <param name="frm">Form to set text</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		public static void SetFormText(Form frm)
		{
			if (Language.TextRoot != null)
			{
				XmlNode node = Language.TextRoot.SelectSingleNode("screen[@name='" + frm.Name + "']");

				if (node != null && node.Attributes["value"] != null)
				{
					//node found
					frm.Text = node.Attributes["value"].Value;
				}
			}
		}

		/// <summary>
		/// Sets the given control's text and tooltip.
		/// </summary>
		/// <param name="control">ToolStripItem to set text/tooltip for.</param>
		/// <param name="tip">ToolTip object</param>
		/// <history>
		/// [Curtis_Beard]		09/27/2012	Initial: 1741735, support ToolStripItem
		/// </history>
		public static void SetToolStripItemText(ToolStripItem control, ToolTip tip)
		{
			if (Language.TextRoot != null)
			{
				string formName = control.Owner.FindForm().Name;
				XmlNode node = Language.TextRoot.SelectSingleNode("screen[@name='" + formName + "']");
				XmlNode controlNode;

				if (node != null)
				{
					//node found, find control
					controlNode = node.SelectSingleNode("control[@name='" + control.Name + "']");

					if (controlNode != null)
					{
						//found control node

						//text
						if (controlNode.Attributes["value"] != null)
							control.Text = controlNode.Attributes["value"].Value;

						//tooltip
						if (tip != null && controlNode.Attributes["tooltip"] != null)
						{
							control.ToolTipText = controlNode.Attributes["tooltip"].Value;
						}
					}
				}
			}
		}

		/// <summary>
		/// Generate xml data for a given control.
		/// </summary>
		/// <param name="control">Control to process</param>
		/// <param name="rootNode">Root xml node</param>
		/// <param name="xmlDoc">Xml Document</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		private static void GenerateXmlControl(Control control, XmlNode rootNode, XmlDocument xmlDoc)
		{
			if (rootNode != null)
			{
				XmlNode node = xmlDoc.CreateElement("control");
				XmlAttribute attrib = xmlDoc.CreateAttribute("name");

				if (!control.Name.Equals(string.Empty) && !control.Text.Equals(string.Empty))
				{
					//Name
					attrib.Value = control.Name;
					node.Attributes.Append(attrib);

					//Text
					attrib = xmlDoc.CreateAttribute("value");
					attrib.Value = control.Text;
					node.Attributes.Append(attrib);

					//Tooltip

					rootNode.AppendChild(node);
				}
			}
		}

		/// <summary>
		/// Generate xml data for a given control and its children.
		/// </summary>
		/// <param name="control">Control to process</param>
		/// <param name="rootNode">Root xml node</param>
		/// <param name="xmlDoc">Xml Document</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		private static void GenerateXmlControls(Control control, XmlNode rootNode, XmlDocument xmlDoc)
		{
			if (control.Controls.Count == 0)
				GenerateXmlControl(control, rootNode, xmlDoc);
			else
			{
				foreach (Control child in control.Controls)
					GenerateXmlControls(child, rootNode, xmlDoc);

				GenerateXmlControl(control, rootNode, xmlDoc);
			}
		}

		/// <summary>
		/// Generate xml data for a given MenuItem control.
		/// </summary>
		/// <param name="item">MenuItem to process</param>
		/// <param name="rootNode">Root xml node</param>
		/// <param name="xmlDoc">Xml Document</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		private static void GenerateXmlMenuItem(MenuItem item, XmlNode rootNode, XmlDocument xmlDoc)
		{
			if (rootNode != null)
			{
				if (!item.Text.Equals(string.Empty))
				{
					XmlNode node = xmlDoc.CreateElement("menuitem");
					XmlAttribute attrib = xmlDoc.CreateAttribute("index");

					//index
					attrib.Value = item.Index.ToString();
					node.Attributes.Append(attrib);

					//Text
					attrib = xmlDoc.CreateAttribute("value");
					attrib.Value = item.Text;
					node.Attributes.Append(attrib);

					rootNode.AppendChild(node);
				}
			}
		}

		/// <summary>
		/// Generate xml data for a given MenuItem control and its children.
		/// </summary>
		/// <param name="item">MenuItem to process</param>
		/// <param name="rootNode">Root xml node</param>
		/// <param name="xmlDoc">Xml Document</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// </history>
		private static void GenerateXmlMenuItems(MenuItem item, XmlNode rootNode, XmlDocument xmlDoc)
		{
			if (item.MenuItems.Count == 0)
				GenerateXmlMenuItem(item, rootNode, xmlDoc);
			else
			{
				foreach (MenuItem child in item.MenuItems)
					GenerateXmlMenuItems(child, rootNode, xmlDoc);
			}
		}

		/// <summary>
		/// Retrieves the top most control (parent) of the given control.
		/// </summary>
		/// <param name="ctrl">Control to find parent</param>
		/// <returns>Control that is the top most parent of the given control</returns>
		private static Control GetParentControl(Control ctrl)
		{
			if (ctrl.Parent == null)
			{
				return ctrl;
			}
			else
			{
				return GetParentControl(ctrl.Parent);
			}
		}

		/// <summary>
		/// Process a given control to set its text property.
		/// </summary>
		/// <param name="control">Control to process</param>
		/// <param name="tip">ToolTip for control</param>
		/// <history>
		/// [Curtis_Beard]		07/31/2006	Created
		/// [Curtis_Beard]		02/01/2012	ADD: support for control's contextmenu
		/// [Curtis_Beard]		09/18/2013	ADD: 65, support for contextmenu sub items
		/// [Curtis_Beard]		08/20/2019	CHG: support ToolStrip and StatusStrip controls outside main form
		/// [Curtis_Beard]		08/25/2022	ADD: support for ContextMenuStrip
		/// </history>
		private static void ProcessControl(Control control, ToolTip tip)
		{
			if (control.Controls.Count == 0)
			{
				if (control.GetType() == typeof(ToolStrip) || control.GetType() == typeof(StatusStrip))
				{
					ProcessToolStrip((ToolStrip)control, tip);
				}
				else
				{
					SetControlText(control, tip);

					// set context menu if available
					if (control.ContextMenu != null && control.ContextMenu.MenuItems.Count > 0)
					{
						foreach (MenuItem item in control.ContextMenu.MenuItems)
						{
							SetContextMenuItemText(control, item);

							// process any sub menu items
							if (item.MenuItems != null && item.MenuItems.Count > 0)
							{
								foreach (MenuItem subItem in item.MenuItems)
								{
									SetContextMenuItemText(control, item, subItem);
								}
							}
						}
					}

					// set context menu strip if available
					if (control.ContextMenuStrip != null && control.ContextMenuStrip.Items.Count > 0)
					{
						for (int i = 0; i < control.ContextMenuStrip.Items.Count; i++)
						{
							if (control.ContextMenuStrip.Items[i] is ToolStripMenuItem item)
							{
								SetContextMenuStripItemText(control, item, i);

								// process any sub menu items
								if (item.DropDownItems != null && item.DropDownItems.Count > 0)
								{
									for (int j = 0; j < item.DropDownItems.Count; j++)
									{
										if (item.DropDownItems[j] is ToolStripMenuItem subItem)
										{
											SetContextMenuStripItemText(control, item, subItem, i, j);
										}
									}
								}
							}
						}
					}
				}
			}
			else
			{
				if (control.GetType() == typeof(ToolStrip) || control.GetType() == typeof(StatusStrip))
				{
					ProcessToolStrip((ToolStrip)control, tip);
				}
				else
				{
					foreach (Control child in control.Controls)
					{
						ProcessControl(child, tip);
					}

					SetControlText(control, tip);
				}
			}
		}

		private static void ProcessMainMenuItem(MenuItem mainMenuItem)
		{
			if (mainMenuItem.MenuItems != null && mainMenuItem.MenuItems.Count > 0)
			{
				foreach (MenuItem item in mainMenuItem.MenuItems)
				{
					ProcessMenuItems(item, mainMenuItem.Index);
				}
			}
		}

		private static void ProcessMainToolStripMenuItem(ToolStripMenuItem mainMenuItem, List<int> indexes)
		{
			if (mainMenuItem.DropDownItems != null && mainMenuItem.DropDownItems.Count > 0)
			{
				for (int i = 0; i < mainMenuItem.DropDownItems.Count; i++)
				{
					indexes.Add(i);
					ProcessToolStripMenuItems(mainMenuItem.DropDownItems[i], indexes);
					indexes.RemoveAt(indexes.Count - 1);
				}
			}
		}

		private static void ProcessMenuItems(MenuItem menuItem, int mainMenuIndex)
		{
			if (menuItem.MenuItems.Count == 0)
			{
				SetMenuItemText(menuItem, mainMenuIndex);
			}
			else
			{
				// set text, then process children
				SetMenuItemText(menuItem, mainMenuIndex);

				foreach (MenuItem subItem in menuItem.MenuItems)
				{
					ProcessMenuItems(subItem, mainMenuIndex);
				}
			}
		}

		/// <summary>
		/// Process each ToolStripItem in the ToolStrip
		/// </summary>
		/// <param name="control">ToolStrip to process</param>
		/// <param name="tip">ToolTip object</param>
		/// <history>
		/// [Curtis_Beard]		09/27/2012	Initial: 1741735, support ToolStripItem
		/// </history>
		private static void ProcessToolStrip(ToolStrip control, ToolTip tip)
		{
			foreach (ToolStripItem child in control.Items)
			{
				ProcessToolStripItem(child, tip);
			}
		}

		/// <summary>
		/// Process each ToolStripItem.
		/// </summary>
		/// <param name="control">ToolStripItem to process</param>
		/// <param name="tip">ToolTip object</param>
		/// <history>
		/// [Curtis_Beard]		09/27/2012	Initial: 1741735, support ToolStripItem
		/// </history>
		private static void ProcessToolStripItem(ToolStripItem control, ToolTip tip)
		{
			SetToolStripItemText(control, tip);
		}

		private static void ProcessToolStripMenuItems(ToolStripItem menuItem, List<int> indexes)
		{
			if ((menuItem is ToolStripMenuItem && ((menuItem as ToolStripMenuItem).DropDownItems == null || (menuItem as ToolStripMenuItem).DropDownItems.Count == 0)) || menuItem is ToolStripSeparator)
			{
				SetToolStripMenuItemText(menuItem, indexes);
			}
			else
			{
				// set text, then process children
				SetToolStripMenuItemText(menuItem, indexes);

				if (menuItem is ToolStripMenuItem)
				{
					var item = menuItem as ToolStripMenuItem;
					for (int i = 0; i < item.DropDownItems.Count; i++)
					{
						indexes.Add(i);
						SetToolStripMenuItemText(item.DropDownItems[i], indexes);
						indexes.RemoveAt(indexes.Count - 1);
					}
				}
			}
		}

		private static void SetMenuItemText(MenuItem item, int mainMenuIndex)
		{
			if (Language.TextRoot != null)
			{
				string formName = item.GetMainMenu().GetForm().Name;
				MenuItem mainMenuItem = item.GetMainMenu().MenuItems[mainMenuIndex];
				Menu parentItem = item.Parent;

				System.Text.StringBuilder builder = new System.Text.StringBuilder();

				// start at current level
				builder.Insert(0, string.Format("/menuitem[@index='{0}']", item.Index));

				while (parentItem != null && parentItem is MenuItem && mainMenuItem != (parentItem as MenuItem))
				{
					MenuItem currentItem = parentItem as MenuItem;
					builder.Insert(0, string.Format("/menuitem[@index='{0}']", currentItem.Index));

					parentItem = currentItem.Parent;
				}

				builder.Insert(0, string.Format("screen[@name='{0}']/menu[@index='{1}']", formName, mainMenuIndex));

				XmlNode node = Language.TextRoot.SelectSingleNode(builder.ToString());
				if (node != null)
				{
					if (node.Attributes["value"] != null)
						item.Text = node.Attributes["value"].Value;
				}
			}
		}

		private static void SetToolStripMenuItemText(ToolStripItem item, List<int> indexes)
		{
			if (Language.TextRoot != null)
			{
				ToolStrip owner = item.Owner;
				while (owner is ToolStripDropDownMenu)
				{
					owner = (owner as ToolStripDropDownMenu).OwnerItem.Owner;
				}
				string formName = owner.FindForm().Name;

				System.Text.StringBuilder builder = new System.Text.StringBuilder();

				builder.AppendFormat("screen[@name='{0}']", formName);

				for (int i = 0; i < indexes.Count; i++)
				{
					builder.AppendFormat("/menu{0}[@index='{1}']", i == 0 ? string.Empty : "item", indexes[i]);
				}

				XmlNode node = Language.TextRoot.SelectSingleNode(builder.ToString());
				if (node != null)
				{
					if (node.Attributes["value"] != null)
						item.Text = node.Attributes["value"].Value;
				}
			}
		}

	}
}