using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoReplace
{
	internal class ReplaceHelper
	{
		#region Private data

		private string fileName;
		private Encoding encoding;
		private string[] lines;
		private string lineCommentPrefix;
		private string commentStart;
		private string commentEnd;

		#endregion Private data

		#region Constructor

		public ReplaceHelper(string fileName, Encoding encoding)
		{
			this.fileName = fileName;
			this.encoding = encoding;

			AnalyseFile();
		}

		#endregion Constructor

		#region Public methods

		public void ReplaceData(string targetName, Dictionary<string, string> data)
		{
			Program.ShowDebugMessage("Replacing data in file \"" + fileName + "\"…");
			string backupFileName = CreateBackup();

			// Read the backup file. If the backup was created earlier, it still contains the source
			// file while the regular file may have been corrupted.
			ReadFileLines(backupFileName);

			// Process all lines in the file
			ReplaceAllLines(targetName, data);

			// Write back all lines to the file
			WriteFileLines();

			DeleteBackup();
		}

		#endregion Public methods

		#region Analysis

		/// <summary>
		/// Analyses the file and detects language-specific values.
		/// </summary>
		private void AnalyseFile()
		{
			switch (Path.GetExtension(fileName).ToLower())
			{
				case ".c":
				case ".cpp":
				case ".cs":
					lineCommentPrefix = "//";
					commentStart = @"/\*";
					commentEnd = @"\*/";
					break;
				case ".css":
					lineCommentPrefix = "//";   // Custom extension by preprocessor
					commentStart = @"/\*";
					commentEnd = @"\*/";
					break;
				case ".iss":
					lineCommentPrefix = "(?:;|//)";   // Combined for both setup and code sections
					break;
				case ".js":
				case ".json":   // May not apply, but that's what we'd do
					lineCommentPrefix = "//";
					commentStart = @"/\*";
					commentEnd = @"\*/";
					break;
				case ".pas":
					commentStart = @"\{";
					commentEnd = @"\}";
					break;
				case ".php":
					lineCommentPrefix = "(?:#|//)";
					commentStart = @"/\*";
					commentEnd = @"\*/";
					break;
				case ".ps1":
					lineCommentPrefix = "#";
					break;
				case ".bas":
				case ".vb":
				case ".vbs":
					lineCommentPrefix = "'";
					break;
				case ".html":
				case ".xhtml":
				case ".xml":
					commentStart = "<!--";
					commentEnd = "-->";
					break;
				default:
					throw new ConsoleException("Unsupported file extension: " + Path.GetExtension(fileName).ToLower(), ExitCodes.UnsupportedLanguage);
			}
		}

		#endregion Analysis

		#region File access

		/// <summary>
		/// Gets the name of the backup file for the current file.
		/// </summary>
		/// <returns>The backup file name.</returns>
		private string GetBackupFileName()
		{
			return fileName + ".bak";
		}

		/// <summary>
		/// Creates a backup of the file if it does not already exist.
		/// </summary>
		/// <returns>The name of the backup file.</returns>
		private string CreateBackup()
		{
			string backup = GetBackupFileName();
			if (!File.Exists(backup))
			{
				File.Copy(fileName, backup);
				Program.ShowDebugMessage("Created backup to \"" + Path.GetFileName(backup) + "\".");
			}
			else
			{
				Program.ShowDebugMessage("Backup \"" + Path.GetFileName(backup) + "\" already exists, skipping.", 2);
			}
			return backup;
		}

		/// <summary>
		/// Deletes the backup of the file if it exists.
		/// </summary>
		private void DeleteBackup()
		{
			string backup = GetBackupFileName();
			File.Delete(backup);
			Program.ShowDebugMessage("Deleted backup \"" + Path.GetFileName(backup) + "\".");
		}

		/// <summary>
		/// Reads all lines of a file.
		/// </summary>
		/// <param name="readFileName">The name of the file to read.</param>
		private void ReadFileLines(string readFileName)
		{
			// Prepare the encoding to use for reading
			Encoding readEncoding = encoding;
			bool detectBom = false;
			if (readEncoding == null)
			{
				readEncoding = Encoding.Default;
				detectBom = true;
			}

			// Open file for reading
			List<string> linesList = new List<string>();
			using (StreamReader sr = new StreamReader(readFileName, readEncoding, detectBom))
			{
				// Read all lines from source file into lines buffer
				while (!sr.EndOfStream)
				{
					string line = sr.ReadLine();
					linesList.Add(line);
				}

				// If we've preset an encoding, this should not change, but technically it's safer
				// to always write in the same encoding we've read the file with
				encoding = sr.CurrentEncoding;
			}
			lines = linesList.ToArray();
		}

		/// <summary>
		/// Writes all lines into the file.
		/// </summary>
		private void WriteFileLines()
		{
			// Write (or overwrite) file in the same encoding as we've read it
			using (StreamWriter sw = new StreamWriter(fileName, false, encoding))
			{
				foreach (string line in lines)
				{
					sw.WriteLine(line);
				}
			}
		}

		#endregion File access

		#region Replacing

		private void ReplaceAllLines(string targetName, Dictionary<string, string> data)
		{
			// Process all lines
			Match match;
			string pattern = null;
			string replacement = null;
			int matchLine = -2;
			for (int i = 0; i < lines.Length; i++)
			{
				// Search for a pattern definition
				if (lineCommentPrefix != null)
				{
					match = Regex.Match(
						lines[i],
						@"^\s*" + lineCommentPrefix + @"\s*autoreplace\s+for\s+" + Regex.Escape(targetName) + @"\s+(.+?)\s+to\s+(.+)\s*$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						pattern = match.Groups[1].Value;
						replacement = match.Groups[2].Value;
						matchLine = i;
						Program.ShowDebugMessage("Found pattern specification.");
					}
				}
				if (commentStart != null && commentEnd != null)   // Only one comment pattern can match in a line
				{
					match = Regex.Match(
						lines[i],
						@"^\s*" + commentStart + @"\s*autoreplace\s+for\s+" + Regex.Escape(targetName) + @"\s+(.+?)\s+to\s+(.+)\s*" + commentEnd + @"\s*$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						pattern = match.Groups[1].Value;
						replacement = match.Groups[2].Value;
						matchLine = i;
						Program.ShowDebugMessage("Found pattern specification.");
					}
				}

				// Match the pattern in the following line
				if (i == matchLine + 1)
				{
					match = Regex.Match(
						lines[i],
						@"^(.*?)(" + pattern + ")(.*)$");
					if (match.Success)
					{
						// Resolve placeholders in replacement text with current data
						string newValue = replacement;
						foreach (var kvp in data)
						{
							newValue = newValue.Replace("{" + kvp.Key + "}", kvp.Value);
						}

						// Replace entire line with new value in place of the matched pattern
						lines[i] = match.Groups[1].Value + newValue + match.Groups[3].Value;
						Program.ShowDebugMessage("Replaced \"" + match.Groups[2].Value + "\" with \"" + newValue + "\" in line " + (i + 1) + ".", 1);

						Match match2 = Regex.Match(
							lines[i],
							@"^(.*?)(" + pattern + ")(.*)$");
						if (!match2.Success)
						{
							Program.ShowDebugMessage("Pattern does not match new line anymore!", 2);
						}
					}
				}
			}
		}

		#endregion Replacing
	}
}
