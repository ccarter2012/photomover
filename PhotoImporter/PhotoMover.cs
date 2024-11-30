using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Security.Cryptography;

namespace PhotoImporter
{
    public class PhotoMover
    {
        #region Properties
        private readonly static List<string> s_validFileTypes = new List<string>() { ".jpg", ".jpeg", ".png", ".mp4", ".mov", ".wmv", ".avi", ".mpg" };
        private readonly object _logLocker = new object();

        private bool _cancelled = false;

        public bool Running { get; private set; } = false;
        public ImportSettings ImportSettings { get; private set; }
        #endregion

        #region Event Declarations
        public event EventHandler<LogMessage>? MessageLogged;
        #endregion

        #region Initialisation
        public PhotoMover(ImportSettings importSettings)
        {
            if (importSettings == null)
            {
                throw new ArgumentNullException(nameof(importSettings));
            }

            if (string.IsNullOrWhiteSpace(importSettings.FromFolder))
            {
                throw new ArgumentNullException(nameof(importSettings.FromFolder));
            }
            if (!Directory.Exists(importSettings.FromFolder))
            {
                throw new DirectoryNotFoundException($"There is no folder on the specified path {importSettings.FromFolder}");
            }
            if (string.IsNullOrWhiteSpace(importSettings.ToFolder))
            {
                throw new ArgumentNullException(nameof(importSettings.ToFolder));
            }
            if (!Directory.Exists(importSettings.ToFolder))
            {
                throw new DirectoryNotFoundException($"There is no folder on the specified path {importSettings.ToFolder}");
            }
            ImportSettings = importSettings;
        }
        #endregion

        #region Public Methods
        public void MovePhotos()
        {
            if (Running)
            {
                return;
            }

            Running = true;
            _LogMessage($"Beginning import from {ImportSettings.FromFolder} to {ImportSettings.ToFolder}...");
            _cancelled = false;
            DirectoryInfo directory = new DirectoryInfo(ImportSettings.FromFolder);

            Thread importThread = new Thread(() =>
            {
                _MoveDirectoryPhotos(directory);
                _LogMessage($"Import complete");
                Running = false;
            });
            importThread.Start();
        }

        public void Cancel()
        {
            _cancelled = true;
        }
        #endregion

        #region Private Methods
        private void _MoveDirectoryPhotos(DirectoryInfo directory)
        {
            foreach (DirectoryInfo subDirectory in directory.EnumerateDirectories())
            {
                if (_cancelled)
                {
                    return;
                }
                _MoveDirectoryPhotos(subDirectory);
            }

            List<string> toDelete = new List<string>();

            IEnumerable<FileInfo> validFiles = directory.EnumerateFiles().Where(f => s_validFileTypes.Contains(f.Extension.ToLower()));
            if (!validFiles.Any())
            {
                _LogMessage($"{directory.FullName} does not contain any valid images or videos...");
            }

            int importCount = validFiles.Count();

            _LogMessage($"{directory.FullName} contains {importCount} valid images...");
            int importedCount = 1;
            foreach (FileInfo validFile in validFiles)
            {
                if (_cancelled)
                {
                    return;
                }

                _LogMessage($"...importing {importedCount} of {importCount}...");
                try
                {
                    DateTime fileDate = validFile.LastWriteTime;
                    string outputFolder = ImportSettings.ToFolder;
                    if (ImportSettings.CreateSubFolders)
                    {
                        outputFolder = Path.Combine(ImportSettings.ToFolder, fileDate.ToString("yyyy"), $"{fileDate:MM}-{fileDate:MMMM}");
                    }
                    if (!Directory.Exists(outputFolder) && ImportSettings.CreateSubFolders)
                    {
                        Directory.CreateDirectory(outputFolder);
                    }
                    string outputFile = Path.Combine(outputFolder, validFile.Name);
                    string testPath = outputFile;
                    int counter = 1;
                    bool copyFile = true;
                    while (File.Exists(outputFile))
                    {
                        FileInfo newPathInfo = new FileInfo(testPath);
                        if (ImportSettings.SkipDuplicates)
                        {
                            if (_FileIsDuplicate(validFile, newPathInfo))
                            {
                                copyFile = false;
                                break;
                            }
                        }
                        outputFile = Path.Combine(newPathInfo.DirectoryName, Path.GetFileNameWithoutExtension(newPathInfo.Name) + $" ({counter}){newPathInfo.Extension}");
                        counter++;
                    }
                    if (!copyFile)
                    {
                        _LogMessage($"Duplicate file detected, skipping...");
                        continue;
                    }
                    File.Copy(validFile.FullName, outputFile);
                    if (ImportSettings.DeleteFilesAfterImport)
                    {
                        File.Delete(validFile.FullName);
                    }
                }
                catch (Exception ex)
                {
                    _LogMessage($"Error importing {validFile.FullName}: {ex.Message}", Color.Red);
                }

                importedCount++;
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

        private bool _FileIsDuplicate(FileInfo compareFrom, FileInfo compareTo)
        {
            if (compareFrom.Length != compareTo.Length)
            {
                return false;
            }

            using (FileStream compareFromFileStream = File.OpenRead(compareFrom.FullName))
            {
                using (FileStream compareToFileStream = File.OpenRead(compareTo.FullName))
                {
                    byte[] firstHash = MD5.Create().ComputeHash(compareFromFileStream);
                    byte[] secondHash = MD5.Create().ComputeHash(compareToFileStream);

                    for (int i = 0; i < firstHash.Length; i++)
                    {
                        if (firstHash[i] != secondHash[i])
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
        #endregion
    }
}
