using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupScheduler
{
    class SqlHelper
    {
        public static string ConStr
        {
            get
            {
                return ConfigurationManager
                    .ConnectionStrings["con"]
                    .ConnectionString;
            }
        }

        internal static void ExecuteNonQuery(
    string conStr,
    CommandType commandType,
    string qry,
    SqlParameter[] prm)
        {
            using (SqlConnection con = new SqlConnection(conStr))
            {
                using (SqlCommand cmd = new SqlCommand(qry, con))
                {
                    cmd.CommandType = commandType;

                    if (prm != null)
                    {
                        cmd.Parameters.AddRange(prm);
                    }

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

       
    }
}
