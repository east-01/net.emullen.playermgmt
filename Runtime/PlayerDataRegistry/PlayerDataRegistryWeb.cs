using System;
using System.Collections.Generic;
using System.Linq;
using EMullen.Core;
using EMullen.Core.PlayerMgmt;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    // Player Data Registry Web
    public partial class PlayerDataRegistry : MonoBehaviour 
    {
        [SerializeField]
        private bool authenticationRequired;
        public bool AuthenticationRequired => authenticationRequired;

        [SerializeField]
        private string authAddress;
        [SerializeField]
        private List<PlayerDatabaseMetadata> databaseMetadatas;

        public PlayerAuthenticator Authenticator { get; private set; }
        private Dictionary<Type, PlayerDatabase> databases;

        private void WebAwake() 
        {
            if(!authenticationRequired) {
                BLog.Log("Started PlayerDataRegistry without requiring authentication, web based features will be disabled.");
                return;
            }

            if(authAddress != null && authAddress.Length > 0) {
                Authenticator = new PlayerAuthenticator(authAddress);
                BLog.Log("Initialized player authenticator", LogSettings, 1);
            }

            databases = new();
            databaseMetadatas.ForEach(md => {
                Type t = GetTypeByName(md.typeName);
                if(t == null) {
                    Debug.LogError($"Failed to initialize PlayerDatabase: The type name \"{md.typeName}\" couldn't be resolved.");
                    return;
                }

                PlayerDatabase newDB = new(t, md.databaseURL);
                databases.Add(t, newDB);
            });
        }

        private PlayerDatabase GetDatabase(Type type) => databases[type];

        /// <summary>
        /// Load the PlayerData database types for each loaded PlayerDatabase.
        /// Called when the player is added to the registry.
        /// </summary>
        private void LoadDatabaseEntries(PlayerData data) 
        {   
            if(!authenticationRequired)
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
            if(!authenticationRequired)
                return;

            databases.Values.ToList().ForEach((Action<PlayerDatabase>)(async database => {
                if(!data.HasData(database.Type))
                    return;

                await database.Set((PlayerDatabaseDataClass) data.GetData(database.Type));
            }));
        }
    }

    [Serializable]
    public struct PlayerDatabaseMetadata 
    {
        public string databaseURL;
        [SubclassSelector(typeof(PlayerDatabaseDataClass))]
        public string typeName;
    }
}