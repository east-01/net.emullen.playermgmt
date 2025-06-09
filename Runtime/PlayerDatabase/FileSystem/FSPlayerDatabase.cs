using System.Threading.Tasks;
using EMullen.PlayerMgmt;
using UnityEngine;
using System;
using System.Runtime.Serialization;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EMullen.Core.PlayerMgmt 
{
    /// <summary>
    /// Make connections to a PlayerData database containing type T. Requests to/from the datab
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FSPlayerDatabase : PlayerDatabase {

        public static string GetPath(string name) => Path.Combine(Application.persistentDataPath, "SaveData", name);
        public static string GetPlayersPath(string basePath, string username) => Path.Combine(basePath, username);

        public string BasePath => GetPath(name);
        public string GetPlayersPath(string username) => GetPlayersPath(BasePath, username);

        public FSPlayerDatabase(Type type, string name = "default") : base(type, name) {}

        /// <summary>
        /// Get the data for a specified uid.
        /// </summary>
        /// <param name="uid">The UID identifier for the data.</param>
        /// <returns>The PlayerDatabaseDataClass associated with the uid if it exists, null if not.</returns>
        public override Task<PlayerDatabaseDataClass> Get(string uid) 
        {
            string json = LoadData(GetPlayersPath(uid));

            if(json == null || json == "") {
                Debug.LogError("Json is null");
                return null;
            }

            PlayerDatabaseDataClass toReturn;

            try {
                toReturn = (PlayerDatabaseDataClass) JsonConvert.DeserializeObject(json, Type);
            } catch(JsonReaderException exception) {
                Debug.LogError(exception.Message);
                return null;
            }

            return Task.FromResult(toReturn);
        }

        /// <summary>
        /// Check if the data belonging to the associated UID is in the database.
        /// </summary>
        /// <returns>Is the UID in database</returns>
        public override Task<bool> Contains(string uid) => Task.FromResult(File.Exists(GetPlayersPath(uid)));

        /// <summary>
        /// Set the data in the table.
        /// If the UID already exists in the table, the existing data will be overwritten.
        /// </summary>
        /// <param name="data">The associated data to be inserted into the table</param>
        /// <returns>Success status.</returns>
        public override Task<bool> Set(PlayerDatabaseDataClass data) 
        {
            if(!data.GetType().Equals(Type))
                throw new InvalidOperationException($"Can't set data to database, provided data's type ({data.GetType().Name}) doesn't match the database type ({Type.Name})");

            if(data == null)
                throw new InvalidOperationException("Can't set data to database, provided data is null.");

            string serializedObject = JsonConvert.SerializeObject(data);

            JObject dataObject = WebRequests.LowerCaseKeys(JObject.Parse(serializedObject));
            if(!dataObject.ContainsKey("uid"))
                throw new InvalidOperationException($"Can't set data to database, provided data doesn't have a uid key.\nOffending data:\n{serializedObject}");

            string uid = dataObject.GetValue("uid").Value<string>();

            if(uid == null || uid == "")
                throw new InvalidOperationException($"Can't set data to database, provided data doesn't have a uid key, or key is empty.\nOffending data:\n{serializedObject}");

            string path = GetPlayersPath(uid);

            SaveData(serializedObject, path);

            BLog.Highlight("Saved data to " + path);

            return Task.FromResult(true);

        }

        // Save the JSON data as a binary file
        public void SaveData(string jsonData, string filePath)
        {            
            // Extract the directory part of the file path, ensure it exists
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            // Convert JSON string to byte array
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

            // Write the byte array to the binary file
            using BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create));

            writer.Write(jsonBytes.Length);  // Write length of JSON data
            writer.Write(jsonBytes);         // Write the actual JSON data
        }

        // Load JSON data from a binary file
        public string LoadData(string filePath)
        {
            if(!File.Exists(filePath)) {
                Debug.LogError("File does not exist!");
                return null;
            }

            using BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open));

            int dataLength = reader.ReadInt32();  // Read the length of the JSON data
            byte[] jsonBytes = reader.ReadBytes(dataLength);  // Read the JSON bytes

            // Convert byte array to JSON string
            return Encoding.UTF8.GetString(jsonBytes);
        }
    }
}