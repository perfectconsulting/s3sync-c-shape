using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace S3Sync
{
    class Program
    {
        public const string rulerLine = "--------------------------------------------------------------------------------";


        private static CloudSyncConfiguration Config = new CloudSyncConfiguration();
        private static CloudSyncLog Log = new CloudSyncLog();
        private static Mutex mutex = new Mutex();

        public static long UpLoadCount { get; set; }
        public static long UpLoadSize { get; set; }
        public static long DownLoadCount { get; set; }
        public static long DownLoadSize { get; set; }
        public static long DeleteCount { get; set; }
        public static long FailedCount { get; set; }
 
     
        public static void Main(string[] args)
        {
            AssemblyName thisAssembly = Assembly.GetExecutingAssembly().GetName();
            string thisVersion = thisAssembly.Version.ToString();
            if (args.Count() == 0)
                args = "-AWSAccessKeyId AKIAJPJIQXPOAFCHAYQA -AWSSecretAccessKey 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 -Push -BucketName galvanisedbucket -LocalFolderPath \"e:\\Backup\\\" -S3FolderPath \"Backup\" -DeleteEmptyFolders -ThreadCount 2 -MaxTransfer 10 -Dry2Run -LogFile \"s3.log\"".Split(' ');

            ArgumentList Arguments = new ArgumentList(args);

            Config.AWSAccessKeyId = Arguments["AWSAccessKeyId"];
            Config.AWSSecretAccessKey = Arguments["AWSSecretAccessKey"];
            Config.BucketName = Arguments["BucketName"];
            Config.LocalFolderPath = Arguments["LocalFolderPath"];
            Config.S3FolderPath = Arguments["S3FolderPath"];
            Config.DeleteEmptyFolders = Arguments.Have("DeleteEmptyFolders");
            Config.DryRun = Arguments.Have("DryRun");
            Config.LogFile = Arguments["LogFile"];

            Program.LogEvent(string.Format("Amazon S3 Synchroniser v{0}", thisVersion));
            Program.ConsoleOnlyLogEvent("Copyright 2012 S. J. Consulting Ltd. All rights reserved");

            if (String.IsNullOrEmpty(Config.AWSAccessKeyId))
            {
                Program.LogEvent("Error, the AWSAccessKeyId is empty.");
                Environment.Exit(1);
            }
           
            if (String.IsNullOrEmpty(Config.AWSSecretAccessKey))
            {
                Program.LogEvent("Error, the AWSSecretAccessKey is empty.");
                Environment.Exit(1);
            }

            if (String.IsNullOrEmpty(Config.BucketName))
            {
                Program.LogEvent("Error, the BucketName is empty.");
                Environment.Exit(1);
            }

            if (String.IsNullOrEmpty(Config.LocalFolderPath))
            {
                Program.LogEvent("Error, the LocalFolderPath is empty.");
                Environment.Exit(1);
            }

            if (!Config.LocalFolderPath.EndsWith("\\"))
                Config.LocalFolderPath += "\\";

            if (!Directory.Exists(Config.LocalFolderPath.TrimEnd('\\')))
            {
                Program.LogEvent("Error, the LocalFolderPath does not exist.");
                Environment.Exit(1);
            }

     
            if (!string.IsNullOrEmpty(Arguments["ThreadCount"]))
            {
                int n;

                if (!int.TryParse(Arguments["ThreadCount"], out n))
                {
                    Program.LogEvent("Error, the ThreadCount must be a number.");
                    Environment.Exit(1);
                }

                Config.ThreadCount = n;
            }

            Config.ThreadCount = Math.Min(Config.ThreadCount, 6);

            if (!string.IsNullOrEmpty(Arguments["MaxRetry"]))
            {
                int n;
                if (!int.TryParse(Arguments["MaxRetry"], out n))
                {
                    Program.LogEvent("Error, the MaxRetry must be a number.");
                    Environment.Exit(1);
                }

                Config.MaxRetry = n;
            }
                
            Config.MaxRetry = Math.Min(Config.MaxRetry, 6);

            if (!string.IsNullOrEmpty(Arguments["MaxTransfer"]))
            {
                int n;
                if (!int.TryParse(Arguments["MaxTransfer"], out n))
                {
                    Program.LogEvent("Error, the MaxTransfer must be a number.");
                    Environment.Exit(1);
                }

                Config.MaxTransfer = n;
            }

            if (Arguments.Have("Pull") || Arguments.Have("Download"))
                Config.SyncDirection = CloudSyncDirection.pull;

            if (Arguments.Have("Push") || Arguments.Have("Upload"))
                Config.SyncDirection = CloudSyncDirection.push;

            if (!String.IsNullOrEmpty(Config.S3FolderPath))
            {
                if (!Config.S3FolderPath.EndsWith("/"))
                    Config.S3FolderPath += "/";
            }

            Program.ConsoleOnlyLogEvent(string.Format("AWSAccessKeyId={0}", Config.AWSAccessKeyId));
            Program.ConsoleOnlyLogEvent(string.Format("AWSSecretAccessKey={0}", Config.AWSSecretAccessKey));
            Program.ConsoleOnlyLogEvent(string.Format("BucketName={0}", Config.BucketName));
            Program.ConsoleOnlyLogEvent(string.Format("LocalFolderPath={0}", Config.LocalFolderPath));

            if (!string.IsNullOrEmpty(Config.S3FolderPath))
                Program.ConsoleOnlyLogEvent(string.Format("S3FolderPath={0}", Config.S3FolderPath));

            Program.ConsoleOnlyLogEvent(string.Format("DeleteEmptyFolders={0}", Config.DeleteEmptyFolders));
            Program.ConsoleOnlyLogEvent(string.Format("SyncDirection={0}", Config.SyncDirection.ToString()));

            Program.ConsoleOnlyLogEvent(string.Format("ThreadCount={0}", Config.ThreadCount));
            Program.ConsoleOnlyLogEvent(string.Format("MaxRetry={0}", Config.MaxRetry));
            if(Config.MaxTransfer > 0)
                Program.ConsoleOnlyLogEvent(string.Format("MaxTransfer={0}", Config.MaxTransfer));

            if (Config.DryRun)
                Program.ConsoleOnlyLogEvent(string.Format("TestRun={0}", Config.DryRun));

            if (!string.IsNullOrEmpty(Config.LogFile))
            {
                Program.Log.LogFile = Config.LogFile;
                Program.ConsoleOnlyLogEvent(string.Format("LogFile={0}", Config.LogFile));
            }

            AmazonS3 s3Client = AWSClientFactory.CreateAmazonS3Client(Config.AWSAccessKeyId, Config.AWSSecretAccessKey);

            Program.ConsoleOnlyLogEvent(string.Format("Checking local file system {0}", Config.LocalFolderPath));
            CloudSyncFileLists localList = CloudSyncFactory.Instance.CreateLocalSyncList(Config.LocalFolderPath, "");
            if (localList == null)
            {
                Program.LogEvent("Error, unable to check local file system");
                Environment.Exit(1);
            }

            Program.ConsoleOnlyLogEvent(string.Format("Checking cloud file system [{0}]/{1}", Config.BucketName, Config.S3FolderPath));
            CloudSyncFileLists cloudList = CloudSyncFactory.Instance.CreateCloudSyncList(s3Client, Config.BucketName, Config.S3FolderPath);
            if (cloudList == null)
            {
                Program.LogEvent("Error, unable to check clound file system");
                Environment.Exit(1);
            }

            Program.ConsoleOnlyLogEvent("Analysing differences...");

            List<CloudSyncAction> synList = null;
            if (Config.SyncDirection == CloudSyncDirection.pull)
                synList = CloudSyncFactory.Instance.CreatePullSyncList(Config.MaxTransfer, localList, cloudList);
            else
                synList = CloudSyncFactory.Instance.CreatePushSyncList(Config.MaxTransfer, localList, cloudList);

            //synList.Sort((a, b) => (a.SourceFile.Size < b.SourceFile.Size) ? -1 : 0);
            Config.ThreadCount = Math.Min(synList.Count, Config.ThreadCount);

            List<List<CloudSyncAction>> synSubList = CloudSyncFactory.Instance.SplitSynList(synList, Config.ThreadCount);
            List<Thread> threads = new List<Thread>();

            if (synList.Count > 0)
            {
                for (int i = 0; i < Config.ThreadCount; i++)
                {
                    CloudSyncEngine engine = new CloudSyncEngine(Config, AWSClientFactory.CreateAmazonS3Client(Config.AWSAccessKeyId, Config.AWSSecretAccessKey), synSubList[i]);
                    Thread engineThread = new Thread(new ThreadStart(engine.Run));
                    threads.Add(engineThread);

                    //Program.LogEvent(string.Format("[{0}] Starting thread...", engine.EngineID));
                    engineThread.Start();
                }

                int finishedCount = 0;
                while (finishedCount < Config.ThreadCount)
                {
                    finishedCount = 0;
                    foreach (Thread engineThread in threads)
                    {
                        if (!engineThread.IsAlive)
                            finishedCount++;
                    }
                }
                Program.ConsoleOnlyLogEvent("Finished");
                Program.LogEvent(string.Format("{0} ({1}) uploaded, {2} ({3}) download, {4} deleted, {5} failures", UpLoadCount, ppSize(UpLoadSize), DownLoadCount, ppSize(DownLoadSize), DeleteCount, FailedCount));
            }
            else
                Program.LogEvent("Files are up-to-date");

            if (Config.DeleteEmptyFolders)
                if (!Config.DryRun)
                    if (Config.SyncDirection == CloudSyncDirection.pull)
                        CloudSyncEngine.DeleteLocalEmptyFolders(Config.LocalFolderPath);
                    else
                        CloudSyncEngine.DeleteCloudEmptyFolders(s3Client, Config.BucketName, Config.S3FolderPath);

            s3Client.Dispose();
            s3Client = null;

            Program.FileOnlyLogEvent(rulerLine);
            Program.Log.Commit(CloudLogPosition.head);
        }

        public static void FileOnlyLogEvent(string s)
        {
            mutex.WaitOne();

            string time = DateTime.Now.ToString("yyy/MM/dd HH:mm:ss");

            Log.LogEvent(string.Format("{0} {1}", time, s));

            mutex.ReleaseMutex();
        }

        public static void ConsoleOnlyLogEvent(string s)
        {
            Console.WriteLine(s);
        }

        public static void LogEvent(string s)
        {
            FileOnlyLogEvent(s);
            ConsoleOnlyLogEvent(s);
        }

        public static string ppSize(double n)
        {
            double m = n;

            if (n <= 1024)
                return n.ToString() + "b";

            n = n / 1024;
            if (n <= 1024)
                return string.Format("{0:0.00}kb", n);

            n = n / 1024;
            if (n <= 1024)
                return string.Format("{0:0.00}mb", n);

            n = n / 1024;
            if (n <= 1024)
                return string.Format("{0:0.00}gb", n);

            return string.Format("{0}b", m);
        }
    }
}
