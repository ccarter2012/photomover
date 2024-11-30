using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

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
            foreach(DirectoryInfo subFolder in mainFolder.EnumerateDirectories())
            {
                if(s_monthFolders.TryGetValue(subFolder.Name, out string correctedName))
                {
                    string newPath = Path.Combine(FolderToReorganize, correctedName);
                    subFolder.MoveTo(newPath);
                    foreach(DirectoryInfo nonMainFolder in subFolder.EnumerateDirectories())
                    {
                        _CopyContentsToRoute(nonMainFolder, newPath);
                        nonMainFolder.Delete();
                    }
                }   
                else if(s_monthFolders.Values.Any(v => v.Equals(subFolder.Name)))
                {
                    foreach (DirectoryInfo nonMainFolder in subFolder.EnumerateDirectories())
                    {
                        _CopyContentsToRoute(nonMainFolder, subFolder.FullName);
                        nonMainFolder.Delete();
                    }
                }
            }
        }
        #endregion

        #region Private methods
        private void _CopyContentsToRoute(DirectoryInfo toMove, string moveToPath)
        {
            foreach (DirectoryInfo subDirectory in toMove.EnumerateDirectories())
            {
                _CopyContentsToRoute(subDirectory, moveToPath);
            }
            Dictionary<string, string> filesToMove = new Dictionary<string, string>();
            foreach (FileInfo file in toMove.EnumerateFiles())
            {
                if(file.Name == "PhotoOrganizer.xml")
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
        #endregion
    }
}
