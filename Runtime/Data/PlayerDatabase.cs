using System;
using System.Data;
using System.Data.SqlClient;
using EMullen.PlayerMgmt;
using UnityEngine;

namespace EMullen.Core.PlayerMgmt 
{
    public class PlayerDatabase<T> where T : PlayerDataClass {

        private string connectionString;

        public PlayerDatabase(string connectionString) 
        {
            this.connectionString = connectionString;
        }

        public T Get(string uid) 
        {
            using SqlConnection conn = new SqlConnection(connectionString);

            try {
                conn.Open();
            } catch(Exception e) {
                Debug.LogError("Failed to open connection: " + e.Message);
                return null;
            }

            string query = "SELECT * FROM table";

            SqlCommand cmd = new(query, conn);
            using SqlDataReader reader = cmd.ExecuteReader();

            while(reader.Read()) {
                Debug.Log(string.Join(", ", reader));
            }

            return null;
        }

    }
}