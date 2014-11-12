using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Security.Cryptography;

namespace S3Sync
{
    class CloudSyncFactory
    {
        private static CloudSyncFactory instance;

        private CloudSyncFactory() { }

        public static CloudSyncFactory Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new CloudSyncFactory();
                }
                return instance;
            }
        }

        public CloudSyncFileLists CreateCloudSyncList(AmazonS3 client, string bucketName, string prefix)
        {
            try
            {
                CloudSyncFileLists fileList = new  CloudSyncFileLists(CloudContext.cloud, prefix);
                ListObjectsResponse response = null;
                string marker = "";

                for(int n=0; n < 20; n++)
                {
                    response = client.ListObjects(new ListObjectsRequest() { BucketName = bucketName, Prefix = prefix, Marker = marker});
                    _CreateCloudSyncList(response, fileList, prefix);

                    if (response.IsTruncated)
                    {
                        marker = response.NextMarker;
                    }
                    else
                    {
                        break;
                    }
                }

                return fileList;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public CloudSyncFileLists _CreateCloudSyncList(ListObjectsResponse response, CloudSyncFileLists list, string prefix)
        {
            foreach (S3Object file in response.S3Objects.ToList())
            {
                if (!file.Key.EndsWith("/"))
                {
                    string key = file.Key.Substring(prefix.Length);
                    list.Add(key, new CloudSyncFile(CloudContext.cloud, prefix, key, file.ETag, file.Size));
                }
            }

            return list;
        }

        public CloudSyncFileLists CreateLocalSyncList(string root, string prefix)
        {
            try
            {
                return _CreateLocalSyncList(new CloudSyncFileLists(CloudContext.local, root), root, prefix);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private CloudSyncFileLists _CreateLocalSyncList(CloudSyncFileLists list, string root, string prefix)
        {
            if(!root.EndsWith("\\"))
                root += "\\";

            string[] files = Directory.GetFiles(root + prefix);
            foreach (string file in files)
            {
                try
                {
                    if (!HiddenFile(file) && !SystemFile(file))
                    {
                        string fileName = file.Substring(root.Length);
                        list.Add(Windows2Unix(fileName), new CloudSyncFile(CloudContext.local, root, fileName, "\"" + GetMd5HashFromFile(file) + "\"", GetFileSize(file)));
                    }
                }
                catch(Exception ex)
                {
                }
            }

            string[] folders = Directory.GetDirectories(root + prefix);
            foreach (string folder in folders)
                if(!HiddenFolder(folder))
                    _CreateLocalSyncList(list, root, folder.Substring(root.Length));
            
            return list;
        }

        public List<CloudSyncAction> CreatePushSyncList(long maxTransfer, CloudSyncFileLists localList, CloudSyncFileLists cloudList)
        {
            int n = 0;
            List<CloudSyncAction> list = new List<CloudSyncAction>();
            foreach (KeyValuePair<string, CloudSyncFile> pair in cloudList)
            {
                CloudSyncFile cloudFile = pair.Value;

                if (localList.ContainsKey(pair.Key))
                {
                    CloudSyncFile localFile = localList[pair.Key];
                    if (cloudFile.MD5 != localFile.MD5)
                    {
                        list.Add(new CloudSyncAction(CloudAction.upload, localFile, cloudFile));
                        if (maxTransfer > 0)
                        {
                            if (++n > maxTransfer)
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    list.Add(new CloudSyncAction(CloudAction.delete, cloudFile, null));
                }
            }

            foreach (KeyValuePair<string, CloudSyncFile> pair in localList)
            {
                CloudSyncFile localFile = pair.Value;

                if (!cloudList.ContainsKey(pair.Key))
                {
                    CloudSyncFile cloudFile = new CloudSyncFile(CloudContext.cloud, cloudList.Root, Windows2Unix(localFile.Path), "", localFile.Size);
                    list.Add(new CloudSyncAction(CloudAction.upload, localFile, cloudFile));
                    if (maxTransfer > 0)
                    {
                        if (++n >= maxTransfer)
                        {
                            break;
                        }
                    }
                }
            }

            return list;
        }

        public List<CloudSyncAction> CreatePullSyncList(long maxTransfer, CloudSyncFileLists localList, CloudSyncFileLists cloudList)
        {
            int n = 0;
            List<CloudSyncAction>list = new List<CloudSyncAction>();
            foreach (KeyValuePair<string, CloudSyncFile>pair in localList) 
            {
                CloudSyncFile localFile = pair.Value;

                if(cloudList.ContainsKey(pair.Key))
                {
                    CloudSyncFile cloudFile = cloudList[pair.Key];
                    if(localFile.MD5 != cloudFile.MD5)
                    {
                        list.Add(new CloudSyncAction(CloudAction.download,cloudFile,localFile));
                        if (maxTransfer > 0)
                        {
                            if (++n >= maxTransfer)
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    list.Add(new CloudSyncAction(CloudAction.delete, localFile, null));
                }

            }

            foreach (KeyValuePair<string, CloudSyncFile>pair in cloudList) 
            {
                CloudSyncFile cloudFile = pair.Value;

                if(!localList.ContainsKey(pair.Key))
                {
                    CloudSyncFile localFile = new CloudSyncFile(CloudContext.local, localList.Root, Unix2Windows(cloudFile.Path), "", cloudFile.Size);
                    list.Add(new CloudSyncAction(CloudAction.download, cloudFile, localFile));
                    if (maxTransfer > 0)
                    {
                        if (++n > maxTransfer)
                        {
                            break;
                        }
                    }
                }
            }
                                   
            return list;
        }
        
        public List<List<CloudSyncAction>>SplitSynList(List<CloudSyncAction>list, int parts)
        {
            if (parts <= 0)
                return null;

            List<List<CloudSyncAction>> sublist = new List<List<CloudSyncAction>>();
            int i;
            for (i = 0; i < parts; i++)
                sublist.Add(new List<CloudSyncAction>());

            i = 0;
            foreach(CloudSyncAction sync in list)
            {
                sublist[i].Add(sync);
                i = (i + 1) % parts;
            }

            return sublist;
        }

        public static string Windows2Unix(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";

            return path.Replace("\\", "/");
        }

        public static string Unix2Windows(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";

            return path.Replace("/", "\\");
        }

        public static string GetMd5HashFromFile(string fileName)
        {
            if (!File.Exists(fileName))
                return "";

            try
            {
                FileStream file = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(file);
                file.Close();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public static long GetFileSize(string fileName)
        {
            try
            {
                FileInfo info = new FileInfo(fileName);
                return info.Length;
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        public static bool HiddenFile(string fileName)
        {
            if (Path.GetFileName(fileName).StartsWith("_"))
                return true;

            return ((File.GetAttributes(fileName) & FileAttributes.Hidden) == FileAttributes.Hidden);
        }

        public static bool SystemFile(string fileName)
        {
            return ((File.GetAttributes(fileName) & FileAttributes.System) == FileAttributes.System);
        }

        public static bool HiddenFolder(string folderName)
        {
            return Path.GetFileName(folderName).StartsWith("_");
        }

    }
}
