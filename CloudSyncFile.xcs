using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace S3Sync
{
    enum CloudContext { E, local, cloud }

    class CloudSyncFile
    {
        public CloudContext Context { get; set; }
        public string Root { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public string MD5 { get; set; }

        public CloudSyncFile(CloudContext source, string root, string path, string md5, long size)
        {
            Context = source;
            Root = root;
            Path = path;
            MD5 = md5;
            Size = size;
        }

        public string FullPath()
        {
            return Root + Path;
        }

        /*
        public string FullPath(int length)
        {
            return ClipPath(Root + Path, length);
          
            if(length <= 0)
                return "";

            string s = Root + Path;

            if (s.Length <= length || length < 10)
                return s;

            length -= 10;
            string t = s.Substring(0,6) + "..." + s.Substring(s.Length - length); 
            return t;
        }
        */

        public string FileName()
        {
            return System.IO.Path.GetFileName(Path);
        }



        public string ppFileInfo()
        {
            return string.Format("{0} ({1})", System.IO.Path.GetFileName(Path), Program.ppSize(Size));
        }

        public string ppFullFileInfo()
        {
            return string.Format("{0} ({1})", FullPath(), Program.ppSize(Size)
                );
        }

        /*
        public static string ClipPath(string path, int length)
        {
            if(length <= 0)
                return "";

            if (path.Length <= length || length < 10)
                return path;

            return(path.Substring(0,6) + "..." + path.Substring(path.Length - (length - 10))); 
        }
        */
    }
}
