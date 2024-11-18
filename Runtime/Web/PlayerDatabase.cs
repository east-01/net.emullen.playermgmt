using System.Threading.Tasks;
using EMullen.PlayerMgmt;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EMullen.Core.PlayerMgmt 
{
    /// <summary>
    /// Make connections to a PlayerData database containing type T. Requests to/from the datab
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PlayerDatabase<T> where T : PlayerDatabaseDataClass {

        private readonly string sqlServerAddr;
        private readonly string tableName;

        public PlayerDatabase(string sqlServerAddr, string tableName = null) 
        {
            this.sqlServerAddr = sqlServerAddr;
            this.tableName = tableName ?? typeof(T).Name.ToLower();
        }

        /// <summary>
        /// Get the data for a specified uid.
        /// </summary>
        /// <param name="uid">The UID identifier for the data.</param>
        /// <returns>The PlayerDatabaseDataClass associated with the uid if it exists, null if not.</returns>
        public async Task<T> Get(string uid) 
        {
            string reqBody = WebRequests.CreateIDJSON(tableName, uid).ToString();
            string json = await WebRequests.WebPostString($"{sqlServerAddr}/fetch", reqBody);

            if(json == null) {
                Debug.LogError("The returned JSON was null.");
                return null;
            }

            Debug.Log(json);

            T toReturn = JsonConvert.DeserializeObject<T>(json);

            if(toReturn == null)
                Debug.LogError($"Failed to deserialize object of type {typeof(T).Name} with returned web data:\n{json}");

            return toReturn;
        }

        /// <summary>
        /// Check if the data belonging to the associated UID is in the database.
        /// </summary>
        /// <returns>Is the UID in database</returns>
        public async Task<bool> Contains(string uid) 
        {
            string reqBody = WebRequests.CreateIDJSON(tableName, uid).ToString();
            string json = await WebRequests.WebPostString($"{sqlServerAddr}/contains", reqBody);

            if(json == null) {
                Debug.LogError("The returned JSON was null.");
                return false;
            }

            JObject obj = new JObject(json);
            if(!obj.ContainsKey("contains")) {
                Debug.LogError("Returned JSON doesn't have contains key in it.");
                return false;
            }

            return obj.GetValue("contains").Value<bool>();
        }

        /// <summary>
        /// Set the data in the table.
        /// If the UID already exists in the table, the existing data will be overwritten.
        /// </summary>
        /// <param name="data">The associated data to be inserted into the table</param>
        /// <returns>Success status.</returns>
        public async Task<bool> Set(T data) 
        {
            if(data == null) {
                Debug.LogError("Can't set data to database, provided data is null.");
                return false;
            }

            string serializedObject = JsonConvert.SerializeObject(data);
            string reqBody = WebRequests.CreateInsertJSON(tableName, serializedObject).ToString();
            string result = await WebRequests.WebPostString($"{sqlServerAddr}/insert", reqBody);

            Debug.Log(result);
            return result != null;
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
}