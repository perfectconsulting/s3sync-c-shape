using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace S3Sync
{
    enum CloudLogPosition { head, tail }

    class CloudSyncLog
    {
        public const string tempLogFile = "_tmp.log";
        public string LogFile { get; set; }
        private static List<string> Log = new List<string>();

        public bool LogEvent(string s)
        {
            try
            {
                Log.Add(s);
                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }

        public bool Commit(CloudLogPosition position)
        {
            if (string.IsNullOrEmpty(LogFile))
                return false;

            switch (position)
            {
                case CloudLogPosition.head:

                    try
                    {
                        if (File.Exists(tempLogFile))
                            File.Delete(tempLogFile);

                        using (StreamWriter sw = File.AppendText(tempLogFile))
                        {
                            foreach (string s in Log)
                                sw.WriteLine(s);

                            if (File.Exists(LogFile))
                            {
                                using (StreamReader sr = File.OpenText(LogFile))
                                {
                                    while (!sr.EndOfStream)
                                        sw.WriteLine(sr.ReadLine());
                                }

                                File.Delete(LogFile);
                            }
                        }

                        File.Move(tempLogFile, LogFile);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                    break;

                case CloudLogPosition.tail:
                    try
                    {
                        using (StreamWriter sw = File.AppendText(LogFile))
                        {
                            foreach (string s in Log)
                                sw.WriteLine(s); ;
                        }
                        return true;
                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                    break;
            }

            return false;            
        }
    }
}
