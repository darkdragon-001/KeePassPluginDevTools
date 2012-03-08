﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using KeePass.Plugins;
using System.Reflection;

namespace KeePassPluginTestUtil
{
	/// <summary>
	/// used to start and stop KeePass for use with testing
	/// </summary>
	public static class KeePassControl
	{
		private const double defaultTimeout = 2000; // msec
		private const string keepassProc = "KeePass";
		private const string keepassExe = "KeePass.exe";
		private const string configFile = "KeePass.config.xml";
		private const string dbFile = "test.kdbx";
		private const string password = "test";
		
		/// <summary>
		/// Sends signal for all running instances of KeePass to exit.
		/// </summary>
		public static void ExitAll()
		{
			/* vars */
			string[] args;
			NotifyIcon notifyIcon;
			Stopwatch stopwatch;
			
			/* grab notification icon */
			if (KeePass.Program.MainForm != null) {
				notifyIcon = KeePass.Program.MainForm.MainNotifyIcon;
			} else {
				notifyIcon = null;
			}

			args = new string[] { "--exit-all" };
			KeePass.Program.Main(args);

			/* wait for program to close */
			stopwatch = new Stopwatch();
			stopwatch.Start();
			while ((stopwatch.ElapsedMilliseconds < defaultTimeout) && (KeePass.Program.MainForm != null)) {
				Thread.Sleep(250);
			}
			// workaround for notification icon not closing
			if ((KeePass.Program.MainForm == null) && (notifyIcon != null)) {
				notifyIcon.Dispose();
			}
		}

		/// <summary>
		/// Starts KeePass with config and database files from Resources directory.
		/// </summary>
		/// <returns>IPluginHost object from KeePass</returns>
		public static IPluginHost StartKeePass()
		{
			return StartKeePass(defaultTimeout);
		}

		/// <summary>
		/// Starts KeePass with config and database files from Resources directory
		/// </summary>
		/// <param name="timeout">time to wait for KeePass to start before showing error message</param>
		/// <returns>IPluginHost object from KeePass</returns>
		public static IPluginHost StartKeePass(double timeout)
		{
			/* vars */
			string debugDir;
			string debugConfigFile, debugDbFile;
			string keepassExeFile;
			string[] args;
			Assembly assembly;
			Stopwatch stopwatch;
			DialogResult result;

			ExitAll(); // close any open instances of keepass

			/* wait for processes to end */
			stopwatch = new Stopwatch();
			stopwatch.Start();
			while ((stopwatch.ElapsedMilliseconds < timeout) && (Process.GetProcessesByName(keepassProc).Length > 0)) {
				Thread.Sleep(250);
			}
			stopwatch.Stop();

			/* verify all running instances of KeePass have ended */
			while (Process.GetProcessesByName(keepassProc).Length > 0) {
				result = ShowErrorMessage("Running instances of KeyPass did not stop within the specified timeout." +
					"\n\nClick OK when all running instances of KeyPass are closed.", true);
				if (result == DialogResult.Cancel) {
					return null;
				}
			}

			/* verify directories */

			assembly = Assembly.GetAssembly(typeof(KeePass.Program));
			debugDir = Path.GetDirectoryName(assembly.Location);
			// really shouldn't need to check this
			if (!Directory.Exists(debugDir)) {
				ShowErrorMessage("Debug directory '" + debugDir + "' does not exist." +
					"\nIt should be the location of " + keepassExe);
				return null;
			}

			/* verify files */			
			
			keepassExeFile = Path.Combine(debugDir, keepassExe);
			if (!File.Exists(keepassExeFile)) {
				ShowErrorMessage("KeePass executable file '" + keepassExeFile + "' does not exist." +
					"\nPlease make sure it is set up in References and 'Copy Local' property is set to true" +
					"\nor fix 'keepassExe' in 'KeePassControl.cs'");
				return null;
			}

			debugConfigFile = Path.Combine(debugDir, configFile);
			try {				
				File.WriteAllText(debugConfigFile, Properties.Resources.KeePass_config);
			} catch (Exception ex) {
				ShowErrorMessage("Error writing config file '" + debugConfigFile + "'." +
					"\n\n" + ex.Message);
				return null;
			}

			debugDbFile = Path.Combine(debugDir, dbFile);
			try {
				File.WriteAllBytes(debugDbFile, Properties.Resources.test);
			} catch (Exception ex) {
				ShowErrorMessage("Error writing database file '" + debugDbFile + "'." +
					"\n\n" + ex.Message);
				return null;
			}			

			/* start keepass with test db */
			try {
				args = new string[] { 
					debugDbFile,
					"-pw:" + password,
					"--debug"
				};

				ThreadStart startKeePass = new ThreadStart(delegate()
				{
					KeePass.Program.Main(args);
				});

				Thread kpThread = new Thread(startKeePass);
				kpThread.SetApartmentState(ApartmentState.STA);
				kpThread.Start();
			} catch (Exception ex) {
				ShowErrorMessage("An exception occured while starting KeePass" +
					"\n\n" + ex.Message);
				return null;
			}

			/* wait for KeyPass to open */
			stopwatch.Restart();
			while ((stopwatch.ElapsedMilliseconds < timeout) && 
				((KeePass.Program.MainForm == null) || (KeePass.Program.MainForm.PluginHost == null))) {
				Thread.Sleep(250);
			}
			stopwatch.Stop();

			/* verify that program started and file is open */
			while (KeePass.Program.MainForm == null) {
				result = ShowErrorMessage("KeePass did not start within the specified timeout." +
					"\n\nClick OK when KeyPass has started.", true);
				if (result == DialogResult.Cancel) {
					return null;
				}
			}
			while (KeePass.Program.MainForm.PluginHost == null) {
				result = ShowErrorMessage("Cannot get PluginHost object. Make sure a file is open in KeePass." +
					"\n\nClick OK when file is open.", true);
				if (result == DialogResult.Cancel) {
					return null;
				}
			}

			return KeePass.Program.MainForm.PluginHost;
		}

		/// <summary>
		/// Helper method for showing Error MessageBox with OK button
		/// </summary>
		/// <param name="message">Message to show</param>
		/// <returns>DialogResult from MessageBox</returns>
		private static DialogResult ShowErrorMessage(string message)
		{
			return ShowErrorMessage(message, false);
		}

		/// <summary>
		/// Helper method for showing Error MessageBox
		/// </summary>
		/// <param name="message">Message to show</param>
		/// <param name="cancelable">If true, shows OK and Cancel button. If false, shows OK button only</param>
		/// <returns>DialogResult from MessageBox</returns>
		private static DialogResult ShowErrorMessage(string message, bool cancelable)
		{
			MessageBoxButtons buttons;
			if (cancelable) {
				buttons = MessageBoxButtons.OKCancel;
			} else {
				buttons = MessageBoxButtons.OK;
			}
			return MessageBox.Show(message, "Error", buttons, MessageBoxIcon.Error);
		}
	}
}