using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BackupScheduler
{
    public class SchedulerService
    {
        private static bool IsSyncRunning = false;
        public static void Run()
        {
           // Console.WriteLine("Scheduler Started");
           // Logger.Write("Scheduler Started");
           

            DataTable dt = GetPendingBackup();
            Console.WriteLine("Pending Backup : " + dt.Rows.Count);
           // Logger.Write("Pending Backup : " + dt.Rows.Count);
            ConnectAllDestinations();
            foreach (DataRow row in dt.Rows)
            {
                int scheduleId = Convert.ToInt32(row["Id"]);
                int databaseId = Convert.ToInt32(row["DatabaseId"]);
                string dbname = row["dbname"].ToString();
                Console.WriteLine("--------------------");
                Console.WriteLine("Schedule ID : " + scheduleId);
                Console.WriteLine("Database ID : " + databaseId);
               // Logger.Write("Schedule ID : " + dbname);
                Console.WriteLine("Frequency : " + row["Frequency"]);
                Console.WriteLine("Backup Time : " + row["BackupTime"]);
                Logger.Write("Schedule ID : " + scheduleId+ "Schedule ID : " + scheduleId+ "Database Name : " + dbname + "Frequency : " + row["Frequency"]+ "Backup Time : " + row["BackupTime"]);
                CheckDiskSpace();
                // next step me yaha backup chalega
                try
                {
                    Console.WriteLine("Backup Running...");
                    Logger.Write("Backup Running...");
                    ExecuteBackup(databaseId);
                  
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                       "Backup Error : " + ex.Message
                     );
                    Logger.Write("Backup Error : " + ex.Message);
                    EmailHelper.SendMail(
    "🚨 Backup Failed - " + dbname,
    $@"
    <div style='font-family:Arial,sans-serif;font-size:14px;line-height:1.6'>
        <h2 style='color:#dc3545;'>🚨 Database Backup Failed</h2>

        <p>A database backup operation has failed.</p>

        <p>
            <b>Database:</b> {dbname}<br/>
            <b>Server:</b> {Environment.MachineName}<br/>
            <b>Time:</b> {DateTime.Now:dd-MMM-yyyy hh:mm:ss tt}
        </p>

        <p>
            <b>Error Details:</b><br/>
            <span style='color:#dc3545'>{ex.Message}</span>
        </p>

        <hr/>

        <p style='color:#666'>
    Thanks & Regards,<br/>
    <b>Team NextronSoft</b><br/>
    Database Backup Monitoring Service
</p>
    </div>"
);
                }
                try
                {
                    UpdateScheduleTime(
      scheduleId,
      row["Frequency"].ToString(),
      TimeSpan.Parse(row["BackupTime"].ToString()), dbname
  );
                    Console.WriteLine(
                   "Next Schedule Updated");
                    Console.WriteLine("Backup Finished");
                }
                catch
                {

                }
               
            }
           
            Console.WriteLine("Retention Check Started");
            DeleteOldBackupFiles();
            Logger.Write("Daily Sync Started file copy");
            SyncToAllDestinations();
            SendDailySummaryIfRequired();
            Console.WriteLine(
    "Daily Summary Email Sent");

            DisconnectAllDestinations();
           // Logger.Write("Scheduler End");
           // Console.WriteLine("Scheduler End");

        }


     
        private static void CheckDiskSpace()
        {
            DriveInfo drive = new DriveInfo("C");

            double freeGB =
                drive.AvailableFreeSpace /
                1024d / 1024d / 1024d;

            Console.WriteLine(
                "Free Space : " +
                freeGB.ToString("0.00") + " GB");

            if (freeGB <= 3)
           /* if (freeGB <= 1000)*/
            {
                EmailHelper.SendMail(
                    "🚨 Critical Disk Space Alert",
                    @"<h2>Disk Space Critical</h2>

            <b>Server :</b> " +
                    Environment.MachineName + @"<br>

            <b>Available Space :</b> " +
                    freeGB.ToString("0.00") + @" GB<br>

            <b>Drive :</b> C:\<br>

            <p style='color:red'>
            Backup may fail due to low disk space.
            Immediate action required.
            </p>"
                );

                throw new Exception(
                    "Low Disk Space. Only "
                    + freeGB.ToString("0.00")
                    + " GB Available");
            }
        }

        //-------------------Email Summery------------------
        private static void SendDailySummaryIfRequired()
        {
            try
            {
                using (SqlConnection con =
                    new SqlConnection(SqlHelper.ConStr))
                {
                    con.Open();

                    SqlCommand cmd =
                        new SqlCommand(
                        @"SELECT TOP 1
                   DailySummaryEmail,
    LastSummaryEmailDate,
    SummaryEmailTime
                  FROM tbl_SystemSettings",
                        con);

                    SqlDataReader dr =
                        cmd.ExecuteReader();

                    bool enabled = false;
                    DateTime? lastDate = null;
                    TimeSpan summaryTime = new TimeSpan(23, 55, 0);

                    if (dr.Read())
                    {
                        enabled = Convert.ToBoolean(
     dr["DailySummaryEmail"]);

                        if (dr["LastSummaryEmailDate"] != DBNull.Value)
                        {
                            lastDate = Convert.ToDateTime(
                                dr["LastSummaryEmailDate"]);
                        }

                        if (dr["SummaryEmailTime"] != DBNull.Value)
                        {
                            summaryTime =
                                TimeSpan.Parse(
                                dr["SummaryEmailTime"].ToString());
                        }
                    }

                    dr.Close();

                    SqlCommand cmdTotal = new SqlCommand(
            @"SELECT COUNT(*)
  FROM tbl_DatabaseMaster
  WHERE IsActive = 1", con);

                    int totalDb = Convert.ToInt32(cmdTotal.ExecuteScalar());

                    SqlCommand cmdProcessed = new SqlCommand(
  @"SELECT COUNT(DISTINCT DatabaseName)
  FROM tbl_BackupLog
  WHERE CAST(CreatedDate AS DATE)=CAST(GETDATE() AS DATE)", con);

                    int processedDb = Convert.ToInt32(cmdProcessed.ExecuteScalar());

                    Logger.Write("TOTAL DB : " + totalDb);
                    Logger.Write("PROCESSED DB : " + processedDb);

                    if (processedDb < totalDb)
                    {
                        Logger.Write("Waiting For Remaining Backup...");
                        return;
                    }

                    if (!enabled)
                        return;

                   
                    if (lastDate != null
                        && lastDate.Value.Date
                        == DateTime.Today)
                    {
                        return;
                    }

                    if (SendDailySummaryEmail())
                    {
                        SqlCommand cmdUpdate =
                            new SqlCommand(
                            @"UPDATE tbl_SystemSettings
          SET LastSummaryEmailDate=@Date",
                            con);

                        cmdUpdate.Parameters.AddWithValue(
                            "@Date",
                            DateTime.Today);

                        cmdUpdate.ExecuteNonQuery();
                       
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Summary Email Error : "
                    + ex.Message);
            }
        }
        private static bool SendDailySummaryEmail()
        {
            try
            {
                string html = BuildDailySummaryHtml();

                EmailHelper.SendMail(
                    "📦 Daily Backup Summary - "
                    + DateTime.Now.ToString("dd-MM-yyyy"),
                    html
                );

                Console.WriteLine(
                    "Daily Summary Email Sent");
                Logger.Write("Daily Summary Email Sent");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Summary Email Failed : "
                    + ex.Message);

                return false;
            }
        }

        private static string BuildDailySummaryHtml()
        {
            int total = 0;
            int success = 0;
            int failed = 0;
            decimal totalSize = 0;
            int copySuccess = 0;
            int copyFailed = 0;
            StringBuilder rows = new StringBuilder();
            using (SqlConnection con = new SqlConnection(SqlHelper.ConStr))
            {
                con.Open();

                SqlCommand cmd = new SqlCommand(@"
SELECT
    DatabaseName,
    BackupType,
    Status,
    ISNULL(Cloud_Copy_Status,'-') AS Cloud_Copy_Status,
    ISNULL(ZipFileSizeMB,0) AS ZipFileSizeMB,
    DurationSeconds,
    BackupStartTime
FROM tbl_BackupLog
WHERE CAST(CreatedDate AS DATE)=CAST(GETDATE() AS DATE)
ORDER BY Id DESC", con);

                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    total++;

                    string status = dr["Status"].ToString();

                    if (status == "Success")
                        success++;
                    else
                        failed++;

                    totalSize += Convert.ToDecimal(dr["ZipFileSizeMB"]);

                    rows.Append(@"
<tr>
    <td>" + dr["DatabaseName"] + @"</td>
    <td>" + dr["BackupType"] + @"</td>

    <td style='color:" +
                   (status == "Success" ? "green" : "red") +
                   @";font-weight:bold'>" + status + @"</td>

 <td style='color:" +
       (dr["Cloud_Copy_Status"].ToString() == "Success"
           ? "green"
           : dr["Cloud_Copy_Status"].ToString() == "Failed"
               ? "red"
               : "#666") +
       @";font-weight:bold'>" +
       dr["Cloud_Copy_Status"] + @"</td>

    <td>" + dr["ZipFileSizeMB"] + @" MB</td>
    <td>" + dr["DurationSeconds"] + @" Sec</td>
    <td>" +
                   Convert.ToDateTime(dr["BackupStartTime"])
                   .ToString("dd-MM-yyyy hh:mm tt")
                   + @"</td>
</tr>");
                }

                dr.Close();
               

                SqlCommand cmdCopySuccess = new SqlCommand(@"
SELECT COUNT(*)
FROM tbl_BackupLog
WHERE Cloud_Copy_Status='Success'
AND CAST(CreatedDate AS DATE)=CAST(GETDATE() AS DATE)", con);

                copySuccess =
                    Convert.ToInt32(cmdCopySuccess.ExecuteScalar());

                SqlCommand cmdCopyFailed = new SqlCommand(@"
SELECT COUNT(*)
FROM tbl_BackupLog
WHERE Cloud_Copy_Status='Failed'
AND CAST(CreatedDate AS DATE)=CAST(GETDATE() AS DATE)", con);

                copyFailed =
                    Convert.ToInt32(cmdCopyFailed.ExecuteScalar());




            }

            return @"
<html>
<head>
<style>
body{
font-family:Segoe UI,Arial;
background:#f5f6fa;
padding:20px;
}

.card{
background:#fff;
border-radius:10px;
padding:20px;
box-shadow:0 2px 10px rgba(0,0,0,.1);
}

table{
width:100%;
border-collapse:collapse;
margin-top:15px;
}

th{
background:#0161b6;
color:white;
padding:10px;
border:1px solid #ddd;
}

td{
padding:8px;
border:1px solid #ddd;
}

.summary{
display:flex;
gap:15px;
margin-top:15px;
margin-bottom:20px;
}

.box{
padding:12px;
border-radius:8px;
color:white;
font-weight:bold;
}

.total{background:#0161b6;}
.success{background:#279b24;}
.failed{background:#dc3545;}
.size{background:#6532b4;}
</style>
</head>

<body>

<div class='card'>

<h2 style='color:#0161b6'>
📦 Daily Backup Summary Report
</h2>

<p>
<b>Date :</b> " + DateTime.Now.ToString("dd-MM-yyyy") + @"<br/>
<b>Server :</b> " + Environment.MachineName + @"<br/>
<b>Generated :</b> " + DateTime.Now.ToString("dd-MM-yyyy hh:mm tt") + @"
</p>

<div class='summary'>

<div class='box total'>
Total Backups<br/>" + total + @"
</div>

<div class='box success'>
Success<br/>" + success + @"
</div>

<div class='box failed'>
Failed<br/>" + failed + @"
</div>

<div class='box size'>
Total Size<br/>" + Math.Round(totalSize, 2) + @" MB
</div>

<div class='box success'>
Copy Success<br/>" + copySuccess + @"
</div>

<div class='box failed'>
Copy Failed<br/>" + copyFailed + @"
</div>

</div>

<table>

<tr>
<th>Database</th>
<th>Type</th>
<th>Backup Status</th>
<th>Copy Status</th>
<th>Size</th>
<th>Duration</th>
<th>Backup Time</th>
</tr>

" + rows + @"

</table>

<br/>

<div style='font-size:12px;color:#666'>
This is an automated email generated by
<b>Nextron Backup Manager v1.0</b>
</div>
<hr  style='border: none; border - top:1px solid #eee;'/>

<p style='color:#666'>
    Thanks & Regards,<br/>
    <b>Team NextronSoft</b><br/>
    Database Backup Monitoring Service
</p>


</div>

</body>
</html>";
        }

        //---------------------End Email summery---------------
        private static DataTable GetPendingBackup()
        {
            DataTable dt = new DataTable();

            using (SqlConnection con =
                new SqlConnection(SqlHelper.ConStr))
            {
                string query = @"

                SELECT *,(select top 1 DatabaseName from tbl_DatabaseMaster where Db_id=tbl_BackupScheduler.DatabaseId) as dbname
                FROM tbl_BackupScheduler
                WHERE IsActive = 1
                AND
                (
                    NextRunTime IS NULL
                    OR
                    NextRunTime <= GETDATE()
                )

                ";

                SqlCommand cmd =
                    new SqlCommand(query, con);


                SqlDataAdapter da =
                    new SqlDataAdapter(cmd);

                da.Fill(dt);
            }

            return dt;
        }
        private static void ExecuteBackup(int databaseId)
        {
            using (SqlConnection con =
                new SqlConnection(SqlHelper.ConStr))
            {
                using (SqlCommand cmd =
                    new SqlCommand("sp_Backup", con))
                {
                    cmd.CommandType =
                        CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue(
                        "@sp_status",
                        "USP_ManualBackup");
                    cmd.Parameters.AddWithValue(
    "@BackupType",
    "Auto");
                    cmd.Parameters.AddWithValue(
                        "@Db_id",
                        databaseId);

                    con.Open();

                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            int error =
                            Convert.ToInt32(dr["error"]);

                            if (error == 0)
                            {
                                long logId =
                                Convert.ToInt64(dr["LogId"]);

                                string bakFile =
                                dr["FilePath"].ToString();


                                Console.WriteLine(
                                "BAK Created : " + bakFile);


                                CreateZipAndUpdate(
                                    logId,
                                    bakFile
                                );
                            }
                            else
                            {
                                Console.WriteLine(
                                dr["message"].ToString());
                            }
                        }
                    }
                }
            }
        }
        private static void CreateZipAndUpdate(long logId, string bakFile)
        {
            string zipFile =
                Path.ChangeExtension(bakFile, ".zip");

            if (File.Exists(zipFile))
            {
                File.Delete(zipFile);
            }


            using (ZipArchive archive =
                ZipFile.Open(zipFile, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(
                    bakFile,
                    Path.GetFileName(bakFile),
                    CompressionLevel.Optimal);
            }


            FileInfo fi = new FileInfo(zipFile);

            decimal size =
                Math.Round(
                (decimal)fi.Length / 1024 / 1024,
                2);


            UpdateZipLog(
                logId,
                zipFile,
                size);
            CopyToAllDestinations(logId, zipFile);



            // CopyToCloud(zipFile);

            if (File.Exists(bakFile))
            {
                File.Delete(bakFile);
            }


            Console.WriteLine(
                "ZIP Created Successfully");
            Logger.Write(
    "ZIP Created : " + zipFile);


        }

    

        private static void UpdateZipLog(
long logId,
string zipFile,
decimal size)
        {

            using (SqlConnection con =
            new SqlConnection(SqlHelper.ConStr))
            {

                using (SqlCommand cmd =
                new SqlCommand("sp_Backup", con))
                {

                    cmd.CommandType =
                    CommandType.StoredProcedure;


                    cmd.Parameters.AddWithValue(
                    "@sp_status",
                    "USP_UpdateBackupZip");


                    cmd.Parameters.AddWithValue(
                    "@LogId",
                    logId);


                    cmd.Parameters.AddWithValue(
                    "@ZipFileName",
                    Path.GetFileName(zipFile));


                    cmd.Parameters.AddWithValue(
                    "@ZipFilePath",
                    zipFile);


                    cmd.Parameters.AddWithValue(
                    "@ZipFileSizeMB",
                    size);


                    con.Open();

                    cmd.ExecuteNonQuery();

                }

            }

        }
        private static void UpdateScheduleTime(
    int scheduleId,
    string frequency,
    TimeSpan backupTime,string dbname)
        {
            DateTime nextRun;

            if (frequency == "Daily")
            {
                nextRun =
                    DateTime.Today
                    .AddDays(1)
                    .Add(backupTime);
            }
            else if (frequency == "Weekly")
            {
                nextRun =
                    DateTime.Today
                    .AddDays(7)
                    .Add(backupTime);
            }
            else if (frequency == "Monthly")
            {
                nextRun =
                    DateTime.Today
                    .AddMonths(1)
                    .Add(backupTime);
            }
            else
            {
                nextRun =
                    DateTime.Now.AddDays(1);
            }


            using (SqlConnection con =
                new SqlConnection(SqlHelper.ConStr))
            {
                SqlCommand cmd =
                new SqlCommand(
                @"UPDATE tbl_BackupScheduler
          SET 
          LastRunTime = GETDATE(),
          NextRunTime = @NextRunTime
          WHERE Id=@Id", con);


                cmd.Parameters.AddWithValue(
                    "@NextRunTime",
                    nextRun);

                cmd.Parameters.AddWithValue(
                    "@Id",
                    scheduleId);


                con.Open();

                cmd.ExecuteNonQuery();
            }

            Logger.Write(
    "Database : " + dbname
    + " | NextRunTime : "
    + nextRun.ToString("dd-MM-yyyy HH:mm:ss"));
        }


        private static void DeleteOldBackupFiles()
        {
            try
            {
                using (SqlConnection con =
                    new SqlConnection(SqlHelper.ConStr))
                {
                    con.Open();

                    SqlCommand cmd =
                        new SqlCommand(@"
                SELECT
                    Id,
                    ZipFilePath
                FROM tbl_BackupLog
                WHERE
                    IsDeleted = 0
                    AND BackupStartTime <
                        DATEADD(DAY,-(
                            SELECT DefaultRetentionDays
                            FROM tbl_SystemSettings
                        ),GETDATE())", con);

                    SqlDataReader dr =
                        cmd.ExecuteReader();

                    DataTable dt = new DataTable();
                    dt.Load(dr);

                    foreach (DataRow row in dt.Rows)
                    {
                        long logId =
                            Convert.ToInt64(row["Id"]);

                        string zipPath =
                            row["ZipFilePath"].ToString();

                        try
                        {
                            if (File.Exists(zipPath))
                            {
                                File.Delete(zipPath);

                                Console.WriteLine(
                                    "Deleted : " +
                                    Path.GetFileName(zipPath));
                            }

                            SqlCommand cmdUpdate =
                                new SqlCommand(@"
                        UPDATE tbl_BackupLog
                        SET
                            IsDeleted = 1,
                            DeletedDate = GETDATE()
                        WHERE Id = @Id", con);

                            cmdUpdate.Parameters.AddWithValue(
                                "@Id",
                                logId);

                            cmdUpdate.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                "Delete Error : "
                                + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Retention Error : "
                    + ex.Message);
            }
        }


        //--------------------Zip file copy from cloud to home pce----------------
        
        private static void CopyToAllDestinations(long logId, string zipFile)
        {
            DataTable dtDest = GetBackupDestinations();

            foreach (DataRow dr in dtDest.Rows)
            {
                string destFolder =
                    dr["Destination_Path"].ToString();
                 
                CopyToDestination(
                    logId,
                    zipFile,
                    destFolder);
            }
        }

        private static void ConnectAllDestinations()
        {
            DataTable dtDest = GetBackupDestinations();

            foreach (DataRow dr in dtDest.Rows)
            {
                string destFolder = dr["Destination_Path"].ToString();
                string userName = dr["UserName"].ToString();
                string password = dr["Password"].ToString();

                try
                {
                    Process p = new Process();
                    p.StartInfo.FileName = "cmd.exe";
                    p.StartInfo.Arguments =
                        $"/c net use \"{destFolder}\" {password} /user:{userName}";
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.Start();
                    p.WaitForExit();

                    //Logger.Write("CONNECTED : " + destFolder);
                }
                catch (Exception ex)
                {
                    Logger.Write("CONNECT ERROR : " + ex.Message);
                }
            }
        }
        private static void DisconnectAllDestinations()
        {
            DataTable dtDest = GetBackupDestinations();

            foreach (DataRow dr in dtDest.Rows)
            {
                string destFolder = dr["Destination_Path"].ToString();

                try
                {
                    Process p = new Process();
                    p.StartInfo.FileName = "cmd.exe";
                    p.StartInfo.Arguments =
                        $"/c net use \"{destFolder}\" /delete /y";
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.Start();
                    p.WaitForExit();

                   // Logger.Write("DISCONNECTED : " + destFolder);
                }
                catch (Exception ex)
                {
                    Logger.Write("DISCONNECT ERROR : " + ex.Message);
                }
            }
        }




        private static void CopyToDestination(
     long logId,
     string zipFile,
     string destFolder)
        {
            try
            {
             

                if (!Directory.Exists(destFolder))
                {
                    Logger.Write(
                        $"COPY FAILED | Destination Offline | Destination={destFolder} | Time={DateTime.Now:dd-MM-yyyy HH:mm:ss}");

                    UpdateCloudStatus(
                        logId,
                        "Failed",
                        "Destination Offline");

                    return;
                }

                string destFile = Path.Combine(
                    destFolder,
                    Path.GetFileName(zipFile));

                if (!File.Exists(destFile))
                {
                    File.Copy(zipFile, destFile, true);

                    Logger.Write(
                        $"COPY SUCCESS | File={Path.GetFileName(zipFile)} | Destination={destFolder} | Time={DateTime.Now:dd-MM-yyyy HH:mm:ss}");

                    UpdateCloudStatus(
                        logId,
                        "Success",
                        "Copied Successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.Write(
                    $"COPY ERROR | File={Path.GetFileName(zipFile)} | Destination={destFolder} | Error={ex.Message} | Time={DateTime.Now:dd-MM-yyyy HH:mm:ss}");

                UpdateCloudStatus(
                    logId,
                    "Failed",
                    ex.Message);
            }
        }

        private static void UpdateCloudStatus(
     long logId,
     string status,
     string remark)
        {
            string qry = @"
    UPDATE tbl_BackupLog
    SET
        Cloud_Copy_Status=@Status,
        Cloud_Copy_DateTime=GETDATE(),
        Cloud_Copy_Remark=@Remark
    WHERE Id=@Id";

            SqlParameter[] prm =
            {
        new SqlParameter("@Id", logId),
        new SqlParameter("@Status", status),
        new SqlParameter("@Remark", remark)
    };

            SqlHelper.ExecuteNonQuery(
                SqlHelper.ConStr,
                CommandType.Text,
                qry,
                prm);
        }
        private static bool _syncRunning = false;

        
        private static DataTable GetBackupDestinations()
        {
            DataTable dt = new DataTable();

            using (SqlConnection con =
                new SqlConnection(SqlHelper.ConStr))
            {
                string qry = @"
        SELECT *
        FROM tbl_BackupDestination
        WHERE IsActive=1";

                SqlDataAdapter da =
                    new SqlDataAdapter(qry, con);

                da.Fill(dt);
            }

            return dt;
        }
        private static void SyncToAllDestinations()
        {
            int totalRecovered = 0;
            if (IsSyncRunning)
            {
                Logger.Write("Sync Already Running. Skipped.");
                return;
            }

            try
            {
                IsSyncRunning = true;

                DataTable dtDest = GetBackupDestinations();

                foreach (DataRow dr in dtDest.Rows)
                {
                    string destFolder =
                        dr["Destination_Path"].ToString();

                    string userName =
                        dr["UserName"].ToString();

                    string password =
                        dr["Password"].ToString();

                    totalRecovered += SyncToPath(
     destFolder,
     userName,
     password);
                }
            }
            finally
            {
                IsSyncRunning = false;
            }
        }
        private static int SyncToPath(
    string destFolder,
    string UserName,
    string Password)
        {
            try
            {
                string sourceFolder = @"C:\DBBackup";

                int recoveredCount = 0;

                foreach (string file in Directory.GetFiles(sourceFolder, "*.zip"))
                {
                    string destFile = Path.Combine(
                        destFolder,
                        Path.GetFileName(file));

                    if (!File.Exists(destFile))
                    {
                        File.Copy(file, destFile, true);

                        recoveredCount++;

                        Logger.Write(
                            "RECOVERY COPY SUCCESS : " +
                            Path.GetFileName(file));
                    }
                }

                if (recoveredCount > 0)
                {
                    Logger.Write(
                        "RECOVERY SYNC COMPLETED. FILES COPIED : "
                        + recoveredCount);
                }

                return recoveredCount;
            }
            catch (Exception ex)
            {
                Logger.Write("Sync Error : " + ex.Message);
                return 0;
            }
        }
    }
}