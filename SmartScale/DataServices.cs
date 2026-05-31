using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace SmartScale
{
    public class DataServices
    {
        public void SaveDataToDatabase(string data)
        {
            string connectionString = "server=your_server;user=your_username;database=your_database;password=your_password;";
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    string query = "INSERT INTO tblLogs (data) VALUES (@data);";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@data", data);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                   Debug.WriteLine(ex.Message);
                }
            }
        }
    }
}
