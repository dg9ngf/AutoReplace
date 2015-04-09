using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Unclassified.Util;

namespace AutoReplace
{
	internal class Program
	{
		#region Private data

		/// <summary>
		/// Indicates whether debug information should be displayed on the console.
		/// </summary>
		private static bool showDebugOutput;

		#endregion Private data

		#region Main control flow

		private static int Main(string[] args)
		{
			try
			{
				// Give us some room in the debugger (the window usually only has 80 columns here)
				if (Debugger.IsAttached)
				{
					Console.WindowWidth = Math.Max(Console.WindowWidth, Math.Min(120, Console.LargestWindowWidth));
				}

				// Fix console things
				Console.OutputEncoding = Encoding.UTF8;
				Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
				ConsoleHelper.FixEncoding();

				// Run the actual program
				MainWrapper();

				ConsoleHelper.WaitIfDebug();
				return (int) ExitCodes.NoError;
			}
			catch (ConsoleException ex)
			{
				return ConsoleHelper.ExitError(ex.Message, (int) ex.ExitCode);
			}
		}

		/// <summary>
		/// Wrapped main program, uses <see cref="ConsoleException"/> as return code in case of
		/// error and does not wait at the end.
		/// </summary>
		private static void MainWrapper()
		{
			CommandLineHelper cmdLine = new CommandLineHelper();
			var showHelpOption = cmdLine.RegisterOption("help").Alias("h", "?");
			var showVersionOption = cmdLine.RegisterOption("version").Alias("ver");
			var debugOption = cmdLine.RegisterOption("debug");
			var utf8Option = cmdLine.RegisterOption("utf8");
			var utf16Option = cmdLine.RegisterOption("utf16");
			var utf8NoBomOption = cmdLine.RegisterOption("utf8nobom");
			var defaultEncodingOption = cmdLine.RegisterOption("defaultenc");

			try
			{
				//cmdLine.ReadArgs(Environment.CommandLine, true);   // Alternative split method, should have the same result
				cmdLine.Parse();
				showDebugOutput = debugOption.IsSet;
				if (showDebugOutput)
				{
					ShowDebugMessage(
						"Command line: " +
							Environment.GetCommandLineArgs()
								.Select(s => "[" + s + "]")
								.Aggregate((a, b) => a + " " + b));
				}
			}
			catch (Exception ex)
			{
				throw new ConsoleException(ex.Message, ExitCodes.CmdLineError);
			}

			// Handle simple text output options
			if (showHelpOption.IsSet)
			{
				ShowHelp();
				return;
			}
			if (showVersionOption.IsSet)
			{
				ShowVersion();
				return;
			}

			if (cmdLine.FreeArguments.Length < 2)
			{
				throw new ConsoleException("Too few arguments.", ExitCodes.CmdLineError);
			}

			// Prepare arguments
			string fileName = cmdLine.FreeArguments[0];
			string targetName = cmdLine.FreeArguments[1];

			Encoding encoding = null;
			if (utf8Option.IsSet) encoding = Encoding.UTF8;
			if (utf16Option.IsSet) encoding = Encoding.Unicode;
			if (utf8NoBomOption.IsSet) encoding = new UTF8Encoding(false);
			if (defaultEncodingOption.IsSet) encoding = Encoding.Default;

			Dictionary<string, string> data = new Dictionary<string, string>();
			for (int i = 2; i < cmdLine.FreeArguments.Length; i++)
			{
				string arg = cmdLine.FreeArguments[i];
				string[] parts = arg.Split(new[] { '=' }, 2);
				if (parts.Length < 2)
				{
					throw new ConsoleException("Invalid name value pair specification in argument " + (i + 1) + ".", ExitCodes.CmdLineError);
				}
				if (data.ContainsKey(parts[0]))
				{
					throw new ConsoleException("Duplicate name value pair specification in argument " + (i + 1) + ".", ExitCodes.CmdLineError);
				}
				data[parts[0]] = parts[1];
			}

			// Replace data in file
			ReplaceHelper rh = new ReplaceHelper(fileName, encoding);
			rh.ReplaceData(targetName, data);
		}

		#endregion Main control flow

		#region Debug output

		/// <summary>
		/// Shows a debug message on the console if debug messages are enabled.
		/// </summary>
		/// <param name="text">The text to display.</param>
		/// <param name="severity">0 (trace message), 1 (success), 2 (warning), 3 (error), 4 (raw output).</param>
		public static void ShowDebugMessage(string text, int severity = 0)
		{
			if (showDebugOutput)
			{
				var color = Console.ForegroundColor;
				switch (severity)
				{
					case 1:
						Console.ForegroundColor = ConsoleColor.DarkGreen;
						break;
					case 2:
						Console.ForegroundColor = ConsoleColor.DarkYellow;
						break;
					case 3:
						Console.ForegroundColor = ConsoleColor.DarkRed;
						break;
					case 4:
						Console.ForegroundColor = ConsoleColor.DarkCyan;
						break;
					default:
						Console.ForegroundColor = ConsoleColor.DarkGray;
						break;
				}
				Console.Error.WriteLine("» " + text.TrimEnd());
				Console.ForegroundColor = color;
			}
		}

		#endregion Debug output

		#region Simple text output options

		/// <summary>
		/// Prints the contents of the embedded file Manual.txt to the console.
		/// </summary>
		private static void ShowHelp()
		{
			using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoReplace.Manual.txt"))
			using (var sr = new StreamReader(resourceStream))
			{
				string content = sr.ReadToEnd().TrimEnd();
				ConsoleHelper.WriteWrappedFormatted(content, FormatText, true);
			}
		}

		/// <summary>
		/// Shows the application version and copyright.
		/// </summary>
		private static void ShowVersion()
		{
			string productName = "";
			string productVersion = "";
			string productDescription = "";
			string productCopyright = "";

			object[] customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productName = ((AssemblyProductAttribute) customAttributes[0]).Product;
			}
			customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productVersion = ((AssemblyFileVersionAttribute) customAttributes[0]).Version;
			}
			customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productDescription = ((AssemblyDescriptionAttribute) customAttributes[0]).Description;
			}
			customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productCopyright = ((AssemblyCopyrightAttribute) customAttributes[0]).Copyright;
			}

			Console.WriteLine(productName + " " + productVersion);
			ConsoleHelper.WriteWrapped(productDescription);
			ConsoleHelper.WriteWrapped(productCopyright);
		}

		private static bool FormatText(char ch)
		{
			switch (ch)
			{
				case '♦': Console.ForegroundColor = ConsoleColor.White; return false;
				case '♣': Console.ForegroundColor = ConsoleColor.Gray; return false;
			}
			return true;
		}

		#endregion Simple text output options
	}
}
