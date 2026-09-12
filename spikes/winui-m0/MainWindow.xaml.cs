using System;

using IcedAstroGrep;
using IcedAstroGrep.Core;

using Microsoft.UI.Xaml;

namespace WinUiSpike
{
	/// <summary>
	/// M0 window: it touches one thing per shell-agnostic layer, so a successful build and a window full
	/// of values is the proof that a WinUI 3 shell can use the engine and the services.
	/// </summary>
	/// <remarks>
	/// There is no search here on purpose: M0 answers "does the toolchain work and does the engine link",
	/// and M1 is where the search loop, the events and the cancellation wiring arrive.
	/// </remarks>
	public sealed partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			WindowText.Text = "window is up (WinUI 3, unpackaged)";

			// Core: the build identity, the same text the WinForms shell puts in its caption.
			try
			{
				EngineText.Text = "engine: " + ProductInformation.ApplicationVersionText;
			}
			catch (Exception ex)
			{
				EngineText.Text = "engine failed: " + ex.Message;
			}

			// AppServices: the plug-in manager plus the real built-in plug-ins, which also exercises the
			// embedded pdftotext resource and the settings files.
			try
			{
				PluginManager.Load();
				PluginText.Text = "plug-ins: " + PluginManager.Items.Count + " loaded from AppServices";
			}
			catch (Exception ex)
			{
				PluginText.Text = "plug-ins failed: " + ex.Message;
			}

			// AppServices: a localized string, which is what the language downshift bought.
			try
			{
				Language.Load("en-us");
				TextText.Text = "text: " + Language.GetGenericText("SearchGenericError", "(key missing)");
			}
			catch (Exception ex)
			{
				TextText.Text = "text failed: " + ex.Message;
			}
		}
	}
}
