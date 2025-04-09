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
    public abstract class PlayerDatabase 
    {

        public readonly Type Type;
        protected readonly string name;

        public PlayerDatabase(Type type, string name = null) 
        {
            if(!typeof(PlayerDatabaseDataClass).IsAssignableFrom(type))
                throw new InvalidOperationException($"Can't initialize PlayerDatabase, the provided type \"{type.Name}\" is not an instance of PlayerDatabaseDataClass");

            this.Type = type;
            this.name = name ?? type.Name.ToLower();
        }

        /// <summary>
        /// Get the data for a specified uid.
        /// </summary>
        /// <param name="uid">The UID identifier for the data.</param>
        /// <returns>The PlayerDatabaseDataClass associated with the uid if it exists, null if not.</returns>
        public abstract Task<PlayerDatabaseDataClass> Get(string uid);

        /// <summary>
        /// Check if the data belonging to the associated UID is in the database.
        /// </summary>
        /// <returns>Is the UID in database</returns>
        public abstract Task<bool> Contains(string uid);

        /// <summary>
        /// Set the data in the table.
        /// If the UID already exists in the table, the existing data will be overwritten.
        /// </summary>
        /// <param name="data">The associated data to be inserted into the table</param>
        /// <returns>Success status.</returns>
        public abstract Task<bool> Set(PlayerDatabaseDataClass data);
    
        public enum DatabaseType { FILE_SYSTEM, SQL }
    }

}