using System.ComponentModel;

namespace IcedAstroGrep
{
	/// <summary>
	/// Marshals work onto the UI thread. Lives in the shell because it is a WinForms concern.
	/// </summary>
	/// <remarks>
	/// Kept in the IcedAstroGrep namespace on purpose: the extension is then in scope for every form
	/// without an added using, so the call sites did not have to change when it moved out of Convertors.
	/// </remarks>
	public static class ControlInvokeExtensions
	{
		/// <summary>
		/// Invoke action delegate on main thread if required.
		/// </summary>
		/// <param name="obj">Object to check for InvokeRequired</param>
		/// <param name="action">Action delegate to perform (either on the current thread or invoked).</param>
		/// <remarks>
		/// Extension for any object that supports the ISynchronizeInvoke interface (such as WinForms
		/// controls). This will handle the InvokeRequired check and call the action delegate from
		/// the appropriate thread.
		/// </remarks>
		/// <example>
		/// <code>
		/// <![CDATA[
		/// private void DB_OfflineModeChanged(object sender, Lib.DB.OfflineModeEventArgs e)
		/// {
		/// // This code could be ran from a background thread
		/// this.InvokeIfRequired(() =>
		/// {
		/// // Code to run after invoking if required
		/// OfflineStatusLabel.Visible = e.OfflineMode;
		/// });
		/// }
		/// ]]>
		/// </code>
		/// </example>
		/// <history>
		/// [Curtis_Beard]		03/05/2020	CHG: use async BeginInvoke for performance
		/// [Curtis_Beard]		05/18/2020	CHG: switch back to Invoke due to UI update issues
		/// </history>
		public static void InvokeIfRequired(this ISynchronizeInvoke obj, System.Windows.Forms.MethodInvoker action)
		{
			if (obj.InvokeRequired)
			{
				var args = new object[0];
				try
				{
					obj.Invoke(action, args);
				}
				catch { }
			}
			else
			{
				action();
			}
		}
	}
}
