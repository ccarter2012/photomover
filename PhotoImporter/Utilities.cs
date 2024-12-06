using Shell32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PhotoImporter
{
    internal static class Utilities
    {
        private readonly static List<string> s_validImageTypes = new List<string>() { ".jpg", ".jpeg", ".png", ".bmp" };
        private readonly static List<string> s_validMovieTypes = new List<string>() { ".mp4", ".mov", ".wmv", ".avi", ".mpg", ".m4v" };
        private readonly static Regex s_imageDateRegex = new Regex(":");
        private readonly static char[] s_charactersToRemove = { (char)8206, (char)8207 };

        private readonly static List<string> s_validFileTypes = new List<string>(s_validImageTypes.Concat(s_validMovieTypes));
        internal static List<string> ValidFileTypes { get { return s_validFileTypes; } }

        internal static bool FileIsDuplicate(FileInfo compareFrom, FileInfo compareTo)
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

        public static DateTime GetDateTakenFromImage(FileInfo toCheck)
        {
            DateTime result = DateTime.Now;
            try
            {
                result = toCheck.LastWriteTime;

                Shell shell = new Shell();
                Folder objFolder = shell.NameSpace(toCheck.DirectoryName);
                FolderItem folderItem = objFolder.ParseName(toCheck.Name);

                for (int j = 0; j < 0xFFFF; j++)
                {
                    string detail = objFolder.GetDetailsOf(null, j);
                    if (string.IsNullOrEmpty(detail))
                    {
                        continue;
                    }
                    if (detail.Equals("Date taken", StringComparison.InvariantCultureIgnoreCase))
                    {
                        DateTime? propertyResult = _GetDateFromProperty(objFolder, folderItem, j);
                        if (propertyResult != null)
                        {
                            return propertyResult.Value;
                        }
                    }
                    if (detail.Equals("Media created", StringComparison.InvariantCultureIgnoreCase))
                    {
                        DateTime? propertyResult = _GetDateFromProperty(objFolder, folderItem, j);
                        if (propertyResult != null)
                        {
                            return propertyResult.Value;
                        }
                    }
                }                
            }
            catch
            {
            }

            return result;
        }

        private static DateTime? _GetDateFromProperty(Folder objFolder, FolderItem folderItem, int propertyNumber)
        {
            try
            {
                string value = objFolder.GetDetailsOf(folderItem, propertyNumber).Trim();
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }
                foreach (char c in s_charactersToRemove)
                {
                    value = value.Replace((c).ToString(), "").Trim();
                }
                return DateTime.Parse(value);
            }
            catch
            {
                return null;
            }
        }
    }
}
