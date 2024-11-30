using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhotoImporter
{
    [Serializable]
    public class ImportSettings
    {
        public string FromFolder { get; set; } = "";
        public string ToFolder { get; set; } = "";
        public bool CreateSubFolders { get; set; }
        public bool DeleteFilesAfterImport { get; set; }
        public bool SkipDuplicates { get; set; }
    }
}
