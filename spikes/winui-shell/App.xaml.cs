using Microsoft.UI.Xaml;

namespace WinUiShell
{
	/// <summary>
	/// M0 entry point: start the window. There is no message loop to set up, no visual styles call and
	/// no high DPI call, because WinUI 3 owns all of that.
	/// </summary>
	public partial class App : Application
	{
		private Window window;

		public App()
		{
			InitializeComponent();
		}

		protected override void OnLaunched(LaunchActivatedEventArgs args)
		{
			window = new MainWindow();
			window.Activate();
		}
	}
}
