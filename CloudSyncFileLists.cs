using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace S3Sync
{
    class CloudSyncFileLists : Dictionary<string, CloudSyncFile>
    {
        public CloudContext Source { set; get; }
        public string Root { get; set; }

        public CloudSyncFileLists(CloudContext source, string root)
            : base()
        {
            Source = source;
            Root = root;
        }
    }
}
