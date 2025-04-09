using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EMullen.Core;
using EMullen.Core.PlayerMgmt;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    // Player Data Registry Database
    public partial class PlayerDataRegistry : MonoBehaviour 
    {
        [SerializeField]
        private bool enableDatabase;

        private Dictionary<Type, PlayerDatabase> databases;

        // SQL Database type
        [SerializeField]
        private string authAddress;
        [SerializeField]
        private List<PlayerDatabaseMetadata> databaseMetadatas;

        private void DatabaseAwake() 
        {
            if(!enableDatabase)
                return;

            databases = new();
            databaseMetadatas.ForEach(md => {
                Type t = GetTypeByName(md.typeName);
                if(t == null) {
                    Debug.LogError($"Failed to initialize PlayerDatabase: The type name \"{md.typeName}\" couldn't be resolved.");
                    return;
                }
                
                PlayerDatabase database;
                string addr = md.address;

                if(md.databaseType == PlayerDatabase.DatabaseType.SQL) {
                    string[] addr_arr = addr.Split("@");
                    if(addr_arr.Length > 2) {
                        Debug.LogError($"Can't load address \"{addr}\" for SQL, split is expecting a single @ symbol to separate endpoint url and table name.");
                        return;
                    }

                    database = new SQLPlayerDatabase(t, addr_arr[0], addr_arr.Length > 1 ? addr_arr[1] : null);
                } else if(md.databaseType == PlayerDatabase.DatabaseType.FILE_SYSTEM) {
                    database = new FSPlayerDatabase(t);
                } else
                    throw new NotImplementedException($"Can't handle database type: {md.databaseType}");

                databases.Add(t, database);
            });

        }

        private PlayerDatabase GetDatabase(Type type) => databases[type];

        /// <summary>
        /// Load the PlayerData database types for each loaded PlayerDatabase.
        /// Called when the player is added to the registry.
        /// </summary>
        private void LoadDatabaseEntries(PlayerData data) 
        {   
            if(!enableDatabase)
                return;

            BLog.Log($"Loading database entries for \"{data.GetUID()}\"", LogSettings, 4);
            databases.Values.ToList().ForEach(async database => {
                bool inDatabase = await database.Contains(data.GetUID());
                BLog.Log($"  In database {database.Type.Name}: {inDatabase}", LogSettings, 4);
                if(!inDatabase)
                    return;

                PlayerDatabaseDataClass databaseData;
                try {
                    databaseData = await database.Get(data.GetUID());
                } catch(DatabaseException exception) {
                    Debug.LogError(exception.Message);
                    return;
                }

                data.SetData(databaseData, database.Type);
                BLog.Log($"  Set data type {databaseData.GetType().Name}", LogSettings, 4);
            });
        }

        /// <summary>
        /// Save the PlayerData database types for each loaded PlayerDatabase.
        /// Called when the player is removed from the registry.
        /// </summary>
        public void SaveDatabaseEntries(PlayerData data) 
        {
            if(!enableDatabase)
                return;

            databases.Values.ToList().ForEach((Action<PlayerDatabase>)(async database => {
                if(!data.HasData(database.Type))
                    return;

                await database.Set((PlayerDatabaseDataClass) data.GetData(database.Type));
            }));
        }

        /// <summary>
        /// Check if the UID exists in any of the loaded PlayerDatabases.
        /// </summary>
        /// <returns>Contains status.</returns>
        public async Task<bool> DatabasesHaveUID(string uid) 
        {
            return await Task.WhenAny(databases.Values.Select(db => db.Contains(uid))).Result;
        }

    }

    [Serializable]
    public struct PlayerDatabaseMetadata 
    {
        public PlayerDatabase.DatabaseType databaseType;        
        [SubclassSelector(typeof(PlayerDatabaseDataClass))]
        public string typeName;
        public string address;
    }
}