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

        public static void Write(string message)
        {
            string folder =
                @"C:\BackupScheduler\Logs";

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string filePath =
                Path.Combine(
                    folder,
                    DateTime.Now.ToString("yyyy-MM-dd") + ".log");

            File.AppendAllText(
                filePath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + " | "
                + message
                + Environment.NewLine);
        }
    }
}
