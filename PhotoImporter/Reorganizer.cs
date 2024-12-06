using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoImporter
{
    public class Reorganizer
    {
        #region Properties
        private readonly static Dictionary<string, string> s_monthFolders = new Dictionary<string, string>()
        {
            { "01", "01-January" },
            { "02", "02-February" },
            { "03", "03-March" },
            { "04", "04-April" },
            { "05", "05-May" },
            { "06", "06-June" },
            { "07", "07-July" },
            { "08", "08-August" },
            { "09", "09-September" },
            { "10", "10-October" },
            { "11", "11-November" },
            { "12", "12-December" }
        };

        public string FolderToReorganize { get; }

        private readonly object _logLocker = new object();

        private bool _cancelled = false;

        public bool Running { get; private set; } = false;
        #endregion

        #region Event Declarations
        public event EventHandler<LogMessage>? MessageLogged;
        #endregion

        #region Initialisation
        public Reorganizer(string folderToReorganize)
        {
            if (string.IsNullOrWhiteSpace(folderToReorganize))
            {
                throw new ArgumentNullException(nameof(folderToReorganize));
            }
            if (!Directory.Exists(folderToReorganize))
            {
                {
                    throw new ArgumentException($"Specified directory ({folderToReorganize}) does not exist", nameof(FormatException));
                }
            }
            FolderToReorganize = folderToReorganize;
        }
        #endregion

        #region Public methods
        public void ReorganizeFolder()
        {
            DirectoryInfo mainFolder = new DirectoryInfo(FolderToReorganize);
            foreach (DirectoryInfo subFolder in mainFolder.EnumerateDirectories())
            {
                if (s_monthFolders.TryGetValue(subFolder.Name, out string correctedName))
                {
                    string newPath = Path.Combine(FolderToReorganize, correctedName);
                    subFolder.MoveTo(newPath);
                    foreach (DirectoryInfo nonMainFolder in subFolder.EnumerateDirectories())
                    {
                        _CopyContentsToRoute(nonMainFolder, newPath);
                        nonMainFolder.Delete();
                    }
                }
                else if (s_monthFolders.Values.Any(v => v.Equals(subFolder.Name)))
                {
                    foreach (DirectoryInfo nonMainFolder in subFolder.EnumerateDirectories())
                    {
                        _CopyContentsToRoute(nonMainFolder, subFolder.FullName);
                        nonMainFolder.Delete();
                    }
                }
            }
        }

        public void ReorganizeFileDates()
        {
            List<string> movedFiles = new List<string>();
            DirectoryInfo mainFolder = new DirectoryInfo(FolderToReorganize);
            foreach (DirectoryInfo subFolder in mainFolder.EnumerateDirectories())
            {
                _ReorganizeFileDates(mainFolder, subFolder, ref movedFiles);
            }
        }
        #endregion

        #region Private methods
        private void _ReorganizeFileDates(DirectoryInfo mainFolder, DirectoryInfo toScan, ref List<string> movedFiles)
        {
            foreach (DirectoryInfo subFolder in toScan.EnumerateDirectories())
            {
                _ReorganizeFileDates(mainFolder, subFolder, ref movedFiles);
            }

            IEnumerable<FileInfo> validFiles = toScan.EnumerateFiles().Where(f => Utilities.ValidFileTypes.Contains(f.Extension.ToLower()));
            if (!validFiles.Any())
            {
                _LogMessage($"{toScan.FullName} does not contain any valid images or videos...");
            }

            int importCount = validFiles.Count();

            _LogMessage($"{toScan.FullName} contains {importCount} valid images...");
            int importedCount = 1;
            foreach (FileInfo validFile in validFiles)
            {
                if (_cancelled)
                {
                    return;
                }

                _LogMessage($"...checking {importedCount} of {importCount}...");
                if (movedFiles.Contains(validFile.FullName))
                {
                    continue;
                }

                try
                {
                    DateTime fileDate = Utilities.GetDateTakenFromImage(validFile);
                    string outputFolder = Path.Combine(mainFolder.FullName, fileDate.ToString("yyyy"), $"{fileDate:MM}-{fileDate:MMMM}");
                    if (toScan.FullName == outputFolder)
                    {
                        continue;
                    }

                    if (!Directory.Exists(outputFolder))
                    {
                        Directory.CreateDirectory(outputFolder);
                    }
                    string outputFile = Path.Combine(outputFolder, validFile.Name);
                    File.Move(validFile.FullName, outputFile);
                    movedFiles.Add(outputFile);
                }
                catch (Exception ex)
                {
                    _LogMessage($"Error reorganizing {validFile.FullName}: {ex.Message}", Color.Red);
                }
            }
        }

        private void _CopyContentsToRoute(DirectoryInfo toMove, string moveToPath)
        {
            foreach (DirectoryInfo subDirectory in toMove.EnumerateDirectories())
            {
                _CopyContentsToRoute(subDirectory, moveToPath);
            }
            Dictionary<string, string> filesToMove = new Dictionary<string, string>();
            foreach (FileInfo file in toMove.EnumerateFiles())
            {
                if (file.Name == "PhotoOrganizer.xml")
                {
                    file.Delete();
                    continue;
                }
                filesToMove.Add(file.FullName, file.Name);
            }
            foreach (KeyValuePair<string, string> filePath in filesToMove)
            {
                string newPath = Path.Combine(moveToPath, filePath.Value);
                string testPath = newPath;
                int counter = 1;
                while (File.Exists(newPath))
                {
                    FileInfo newPathInfo = new FileInfo(testPath);
                    newPath = Path.Combine(newPathInfo.DirectoryName, Path.GetFileNameWithoutExtension(newPathInfo.Name) + $" ({counter}){newPathInfo.Extension}");
                    counter++;
                }
                File.Move(filePath.Key, newPath);
            }
        }

        private void _LogMessage(string message)
        {
            _LogMessage(message, Color.Black);
        }

        private void _LogMessage(string message, Color logColour)
        {
            lock (_logLocker)
            {
                MessageLogged?.Invoke(this, new LogMessage() { Message = message, MessageTime = DateTime.Now, MessageColour = logColour });
            }
        }
        #endregion
    }
}
