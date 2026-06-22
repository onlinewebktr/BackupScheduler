using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace BackupScheduler
{
    class EmailHelper
    {
        /* SendFailureEmail();
          SendDailySummaryEmail();
         SendDailySummaryIfRequired();*/

        public static void SendMail(
    string subject,
    string body)
        {
            string smtpHost = "";
            int smtpPort = 587;
            string smtpEmail = "";
            string smtpPassword = "";
            string adminEmail = "";

            using (SqlConnection con =
                new SqlConnection(SqlHelper.ConStr))
            {
                con.Open();

                SqlCommand cmd =
                    new SqlCommand(
                    @"SELECT TOP 1
              SMTPHost,
              SMTPPort,
              SMTPEmail,
              SMTPPassword,
              AdminEmail
              FROM tbl_SystemSettings", con);

                SqlDataReader dr =
                    cmd.ExecuteReader();

                if (dr.Read())
                {
                    smtpHost = dr["SMTPHost"].ToString();
                    smtpPort = Convert.ToInt32(dr["SMTPPort"]);
                    smtpEmail = dr["SMTPEmail"].ToString();
                    smtpPassword = dr["SMTPPassword"].ToString();
                    adminEmail = dr["AdminEmail"].ToString();
                }

                dr.Close();
            }

            MailMessage mail =
                new MailMessage();

            mail.From =
                new MailAddress(smtpEmail);

            mail.To.Add(adminEmail);

            mail.Subject = subject;

            mail.Body = body;

            mail.IsBodyHtml = true;


            SmtpClient smtp =
                new SmtpClient(
                smtpHost,
                smtpPort);

            smtp.EnableSsl = true;

            smtp.Credentials =
                new NetworkCredential(
                smtpEmail,
                smtpPassword);

            smtp.Send(mail);
        }
    }
}
