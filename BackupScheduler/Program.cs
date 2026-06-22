using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
namespace BackupScheduler
{
    class Program
    {
        static void Main(string[] args)
        {
            Mutex mutex =
       new Mutex(
           true,
           "BackupScheduler",
           out bool createdNew);

            if (!createdNew)
            {
                return;
            }

            SchedulerService.Run();

            GC.KeepAlive(mutex);
        }
    }
}
