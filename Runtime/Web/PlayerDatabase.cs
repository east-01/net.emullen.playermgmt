using System.Threading.Tasks;
using EMullen.PlayerMgmt;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.Serialization;

namespace EMullen.Core.PlayerMgmt 
{
    /// <summary>
    /// Make connections to a PlayerData database containing type T. Requests to/from the datab
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PlayerDatabase {

        public readonly Type Type;
        private readonly string sqlServerAddr;
        private readonly string tableName;

        public PlayerDatabase(Type type, string sqlServerAddr, string tableName = null) 
        {
            if(!typeof(PlayerDatabaseDataClass).IsAssignableFrom(type))
                throw new InvalidOperationException($"Can't initialize PlayerDatabase, the provided type \"{type.Name}\" is not an instance of PlayerDatabaseDataClass");

            this.Type = type;
            this.sqlServerAddr = sqlServerAddr;
            this.tableName = tableName ?? type.Name.ToLower();
        }

        /// <summary>
        /// Get the data for a specified uid.
        /// </summary>
        /// <param name="uid">The UID identifier for the data.</param>
        /// <returns>The PlayerDatabaseDataClass associated with the uid if it exists, null if not.</returns>
        public async Task<PlayerDatabaseDataClass> Get(string uid) 
        {
            string token = RetrieveToken(uid);
            if(token == null)
                throw new InvalidOperationException($"Failed to check contains status, couldn't retrieve the database token belonging to uid \"{uid}\"");

            string reqBody = WebRequests.CreateIDJSON(token, tableName, uid).ToString();
            string json;

            try {
                json = await WebRequests.WebPostString($"{sqlServerAddr}/fetch", reqBody);
            } catch(UnityWebRequestException exception) {
                // Throw database exception with details from the WebRequestException
                throw new DatabaseException(exception.responseCode == 400 ? exception.text : "A backend server error occurred.", exception);
            }

            PlayerDatabaseDataClass toReturn;

            try {
                toReturn = (PlayerDatabaseDataClass) JsonConvert.DeserializeObject(json, Type);
            } catch(JsonReaderException exception) {
                Debug.LogError(exception.Message);
                return null;
            }

            return toReturn;
        }

        /// <summary>
        /// Check if the data belonging to the associated UID is in the database.
        /// </summary>
        /// <returns>Is the UID in database</returns>
        public async Task<bool> Contains(string uid) 
        {
            string token = RetrieveToken(uid);
            if(token == null)
                throw new InvalidOperationException($"Failed to check contains status, couldn't retrieve the database token belonging to uid \"{uid}\"");

            string reqBody = WebRequests.CreateIDJSON(token, tableName, uid).ToString();
            string json;

            try {
                json = await WebRequests.WebPostString($"{sqlServerAddr}/contains", reqBody);
            } catch(UnityWebRequestException exception) {
                // Throw database exception with details from the WebRequestException
                throw new DatabaseException(exception.responseCode == 400 ? exception.text : "A backend server error occurred.", exception);
            }

            JObject parsedJSON;

            try {
                // Parse the provided string json into a JObject 
                parsedJSON = JObject.Parse(json);
            } catch(JsonReaderException exception) {
                Debug.LogError(exception.Message);
                return false;
            }

            if(!parsedJSON.ContainsKey("contains")) {
                Debug.LogError("Failed to check contains status, returned JSON doesn't have a contains key.");
                return false;
            }

            return parsedJSON.GetValue("contains").Value<bool>();
        }

        /// <summary>
        /// Set the data in the table.
        /// If the UID already exists in the table, the existing data will be overwritten.
        /// </summary>
        /// <param name="data">The associated data to be inserted into the table</param>
        /// <returns>Success status.</returns>
        public async Task<bool> Set(PlayerDatabaseDataClass data) 
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
            string token = RetrieveToken(uid);
            if(token == null)
                throw new InvalidOperationException($"Failed to get data, couldn't retrieve the database token belonging to uid \"{uid}\"");

            string reqBody = WebRequests.CreateInsertJSON(token, tableName, serializedObject).ToString();
            string json;

            try {
                json = await WebRequests.WebPostString($"{sqlServerAddr}/insert", reqBody);
            } catch(UnityWebRequestException exception) {
                // Throw database exception with details from the WebRequestException
                throw new DatabaseException(exception.responseCode == 400 ? exception.text : "A backend server error occurred.", exception);
            }

            return json != null;
        }

        /// <summary>
        /// Retrieve the database token belonging to the specific uid contained in the locally
        ///   instantited PlayerDatabaseRegistry
        /// </summary>
        /// <param name="uid">The token that belongs to the uid</param>
        /// <returns>The string token if it exists, null otherwise.</returns>
        private string RetrieveToken(string uid) 
        {
            if(PlayerDataRegistry.Instance == null) {
                Debug.LogWarning("Failed to retrieve token: PlayerDataRegistry instance is null.");
                return null;
            }
            
            if(!PlayerDataRegistry.Instance.Contains(uid))
                return null;
            
            PlayerData data = PlayerDataRegistry.Instance.GetPlayerData(uid);

            if(!data.HasData<DatabaseTokenData>())
                return null;

            return data.GetData<DatabaseTokenData>().token;
        }
    }

    /// <summary>
    /// The PlayerDatabaseDataClass is an extension of the PlayerDataClass that enforces a string
    ///   UID to be the first value. The UID is required to be first as it's the identifier for
    ///   the data in each table.
    /// </summary>
    public abstract class PlayerDatabaseDataClass : PlayerDataClass 
    {
        public abstract string UID { get; }
    }

    /// <summary>
    /// An exception relating to all database errors, holds the culprit WebRequestException
    ///   and additional message from the database server.
    /// </summary>
    [Serializable]
    public class DatabaseException : Exception
    {
        public UnityWebRequestException WebException => InnerException != null ? (UnityWebRequestException) InnerException : null;

        public DatabaseException() : base() {}
        public DatabaseException(string message) : base(message) {}
        public DatabaseException(string message, Exception innerException) : base(message, innerException) {}
        protected DatabaseException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }
}