using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace S3Sync
{
    public enum CloudSyncDirection { E, push, pull };

    class CloudSyncConfiguration
    {
        public string AWSAccessKeyId { get; set; }
        public string AWSSecretAccessKey { get; set; }
        public string BucketName { get; set; }
        public string LocalFolderPath { get; set; }
        public string S3FolderPath { get; set; }
        public bool DeleteEmptyFolders { get; set; }
        public int ThreadCount { get; set; }
        public int MaxRetry { get; set; }
        public long MaxTransfer { get; set; }
        public CloudSyncDirection SyncDirection { get; set; }
        public bool DryRun { get; set; }
        public string LogFile { get; set; }

        public CloudSyncConfiguration()
        {
            AWSAccessKeyId = "";
            AWSSecretAccessKey = "";
            BucketName = "";
            LocalFolderPath = "";
            S3FolderPath = "";
            DeleteEmptyFolders = false;
            ThreadCount = 1;
            MaxRetry = 3;
            MaxTransfer = 0;
            SyncDirection = CloudSyncDirection.pull;
            DryRun = false;
            LogFile = "";            
        }
    }
}
