using System;
using System.Collections.Generic;
using System.Reflection;
using EMullen.Core;
using EMullen.Core.PlayerMgmt;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    // Player Data Registry Web
    public partial class PlayerDataRegistry : MonoBehaviour 
    {
        [SerializeField]
        private string authAddress;
        [SerializeField]
        private List<PlayerDatabaseMetadata> databaseMetadatas;

        public PlayerAuthenticator Authenticator { get; private set; }
        private Dictionary<Type, PlayerDatabase> databases;

        private void WebAwake() 
        {
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

        private Type GetTypeByName(string typeName)
        {
            // Search in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                var type = assembly.GetType(typeName);
                if (type != null && typeof(PlayerDatabaseDataClass).IsAssignableFrom(type)) {
                    return type;
                }
            }
            return null;
        }
    }

    [Serializable]
    public struct PlayerDatabaseMetadata 
    {
        public string databaseURL;
        public Assembly typeAssembly;
        public string typeName;
    }
}