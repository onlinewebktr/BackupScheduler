using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupScheduler
{
    class Logger
    {
        private static bool _cleanupDone = false;

        public static void Write(string message)
        {
            string folder =
         @"C:\inetpub\vhosts\nextronsoft.com\dbabakupwbak.nextronsoft.com\Logs";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            // Sirf ek baar cleanup chale
            if (!_cleanupDone)
            {
                DeleteOldLogs(folder);
                _cleanupDone = true;
            }
            string filePath = Path.Combine(
                folder,
                DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            File.AppendAllText(
                filePath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + " | "
                + message
                + Environment.NewLine);


        }

        private static void DeleteOldLogs(string folder)
        {
            try
            {
                foreach (string file in Directory.GetFiles(folder, "*.log"))
                {
                    FileInfo fi = new FileInfo(file);

                    if (fi.CreationTime < DateTime.Now.AddDays(-7))
                    {
                        fi.Delete();
                    }
                }
            }
            catch
            {
            }
        }
    }
}
