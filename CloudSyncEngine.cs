using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace S3Sync
{
    class CloudSyncEngine
    {
        private CloudSyncConfiguration Configuration = null;
        private static short TotalEngines = 0;
        public short EngineID { get; set; }
        private AmazonS3 Client;
        private List<CloudSyncAction> Queue;

        public CloudSyncEngine(CloudSyncConfiguration configuration, AmazonS3 client, List<CloudSyncAction> queue)
        {
            EngineID = ++TotalEngines;

            Configuration = configuration;
            Client = client;
            Queue = queue;
        }

        public void Run()
        {
            foreach (CloudSyncAction sync in Queue)
            {
                string action;
                int retry;
                switch (sync.Action)
                {
                    case CloudAction.download:
                        action = "Downloading";
                        Program.FileOnlyLogEvent(string.Format("[{0}] {1} {2}", EngineID, action, sync.SourceFile.ppFullFileInfo()));
                        Program.ConsoleOnlyLogEvent(string.Format("[{0}] {1} {2}", EngineID, action, sync.SourceFile.ppFileInfo()));

                        for (retry = 0; retry < Configuration.MaxRetry; retry++)
                        {
                            if (!Configuration.DryRun)
                            {
                                if (GetAmazonS3Object(Client, Configuration.BucketName, sync.DestinationFile.FullPath(), sync.SourceFile.FullPath()))
                                {
                                    //Console.WriteLine(" ok");
                                    sync.Status = CloudStatus.ok;
                                    Program.DownLoadCount++;
                                    Program.DownLoadSize += sync.SourceFile.Size;
                                    break;
                                }
                                else
                                {
                                    action = "Re-Downloading";
                                }
                            }
                            else
                            {
                                sync.Status = CloudStatus.ok;
                                Program.DownLoadCount++;
                                Program.DownLoadSize += sync.SourceFile.Size;
                                break;
                            }
                        }

                        if (retry == Configuration.MaxRetry)
                        {
                            Program.LogEvent(string.Format("[{0}] Download failed", EngineID));
                            sync.Status = CloudStatus.failed;
                            Program.FailedCount++;
                        }

                        break;

                    case CloudAction.upload:
                        action = "Uploading";

                        for (retry = 0; retry < Configuration.MaxRetry; retry++)
                        {
                            Program.FileOnlyLogEvent(string.Format("[{0}] {1} {2}", EngineID, action, sync.SourceFile.ppFullFileInfo()));
                            Program.ConsoleOnlyLogEvent(string.Format("[{0}] {1} {2}", EngineID, action, sync.SourceFile.ppFileInfo()));

                            if (!Configuration.DryRun)
                            {
                                if (PutAmazonS3Object(Client, Configuration.BucketName, sync.SourceFile.FullPath(), sync.DestinationFile.FullPath()))
                                {
                                    //Console.WriteLine(" ok");
                                    sync.Status = CloudStatus.ok;
                                    Program.UpLoadCount++;
                                    Program.UpLoadSize += sync.SourceFile.Size;
                                    break;
                                }
                                else
                                {
                                    action = "Re-Uploading";
                                }
                            }
                            else
                            {
                                sync.Status = CloudStatus.ok;
                                Program.UpLoadCount++;
                                Program.UpLoadSize += sync.SourceFile.Size;
                                break;
                            }
                        }

                        if (retry == Configuration.MaxRetry)
                        {
                            Program.LogEvent(string.Format("[{0}] Upload failed", EngineID));
                            sync.Status = CloudStatus.failed;
                            Program.FailedCount++;
                        }

                        break;

                    case CloudAction.delete:
                        Program.FileOnlyLogEvent(string.Format("[{0}] Deleting {1}", EngineID, sync.SourceFile.ppFullFileInfo()));
                        Program.ConsoleOnlyLogEvent(string.Format("[{0}] Deleting {1}", EngineID, sync.SourceFile.ppFileInfo()));

                        if (!Configuration.DryRun)
                        {
                            try
                            {
                                switch (sync.SourceFile.Context)
                                {
                                    case CloudContext.local:
                                        File.Delete(sync.SourceFile.FullPath());
                                        sync.Status = CloudStatus.ok;
                                        Program.DeleteCount++;
                                        break;

                                    case CloudContext.cloud:
                                        if (DeleteAmazonS3Object(Client, Configuration.BucketName, sync.SourceFile.FullPath()))
                                        {
                                            sync.Status = CloudStatus.ok;
                                            Program.DeleteCount++;
                                        }
                                        else
                                        {
                                            sync.Status = CloudStatus.failed;
                                            Program.FailedCount++;
                                        }
                                        break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Program.LogEvent(string.Format("[{0}] Delete failed", EngineID));
                                sync.Status = CloudStatus.failed;
                                Program.FailedCount++;
                            }
                        }
                        else
                        {
                            sync.Status = CloudStatus.ok;
                            Program.DeleteCount++;
                        }
                        break;
                }
            }

            Client.Dispose();
        }

        private static bool GetAmazonS3Object(AmazonS3 client, string bucketName, string local, string server)
        {
            if (client == null)
                return false;

            using (GetObjectResponse s3Object = client.GetObject(new GetObjectRequest() { BucketName = bucketName, Key = server }))
            {
                try
                {
                    s3Object.WriteResponseStreamToFile(local);
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PutAmazonS3Object(AmazonS3 client, string bucketName, string local, string server)
        {
            if (client == null)
                return false;

            try
            {
                PutObjectRequest s3Request = new PutObjectRequest();
                s3Request.WithBucketName(bucketName);
                s3Request.WithKey(server);
                s3Request.WithFilePath(local);
                s3Request.Timeout = 3600000;
                client.PutObject(s3Request);
            }
            catch(Exception ex)
            {
                return false;
            }

            return true;
        }

        private static bool DeleteAmazonS3Object(AmazonS3 client, string bucketName, string server)
        {
            if (string.IsNullOrEmpty(server))
                return false;

            try
            {
                DeleteObjectRequest s3Request = new DeleteObjectRequest();
                s3Request.WithBucketName(bucketName);
                s3Request.WithKey(server);

                client.DeleteObject(s3Request);
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        public static bool DeleteLocalEmptyFolders(string root)
        {
            if (!root.EndsWith("\\"))
                root += "\\";

            string[] folders = Directory.GetDirectories(root);
            foreach (string folder in folders)
            {
                if (DeleteLocalEmptyFolders(folder))
                {
                    Program.FileOnlyLogEvent(string.Format("Deleting {0}\\", folder));
                    Program.ConsoleOnlyLogEvent(string.Format("Deleting {0}\\", folder.Substring(root.Length)));
                    Directory.Delete(folder);
                }
            }

            return(Directory.GetFiles(root).Length == 0);
        }

        public static void DeleteCloudEmptyFolders(AmazonS3 client, string bucketName, string prefix)
        {
            ListObjectsResponse response = client.ListObjects(new ListObjectsRequest() { BucketName = bucketName, Prefix = prefix });
            List<string>folders = new List<string>();

            foreach (S3Object file in response.S3Objects.ToList())
                if (file.Key.EndsWith("/"))
                    folders.Add(file.Key);

            foreach (S3Object file in response.S3Objects.ToList())
                if (!file.Key.EndsWith("/"))
                    folders.Remove(Path.GetDirectoryName(file.Key) + "/");

            folders.Sort((a,b) => (a.Length > b.Length)?-1:0);

            foreach (string folder in folders)
            {
                Program.FileOnlyLogEvent(string.Format("Deleting {0}", folder));
                Program.ConsoleOnlyLogEvent(string.Format("Deleting {0}", folder.Substring(prefix.Length)));
                DeleteAmazonS3Object(client, bucketName, folder );
            }
        }
    }
}
