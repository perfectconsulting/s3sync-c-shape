using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace S3Sync
{
    enum CloudAction { E, upload, download, delete };
    enum CloudStatus { E, ok, failed };

    class CloudSyncAction
    {
        public CloudAction Action { get; set; }
        public CloudSyncFile SourceFile { get; set;  }
        public CloudSyncFile DestinationFile { get; set; }
        public CloudStatus Status { get; set; }

        public CloudSyncAction(CloudAction action, CloudSyncFile source, CloudSyncFile destination)
        {
            Action = action;
            SourceFile = source;
            Status = CloudStatus.E;
            DestinationFile = destination;
        }
    }
}
