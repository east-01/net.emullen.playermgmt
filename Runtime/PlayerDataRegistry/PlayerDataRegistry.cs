using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using EMullen.Core;
using EMullen.Core.PlayerMgmt;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    [RequireComponent(typeof(PlayerManager))]
    /// <summary>
    /// The PlayerDataRegistry stores all PlayerData for the local instance. If using
    ///   FishNetworking, the data will sync with the server.<br\>
    /// <br/>
    /// Networked:<br/>
    /// The networking backend for the PlayerDataRegistry. Uses broadcasts to synchronize with the
    ///   server's PlayerDataRegistry.<br/>
    /// Has multiple phases for joining/leaving so that data doesn't get lost during
    /// synchronization.<br/>
    /// <br/>
    /// Web:<br/>
    /// The web backend for the PlayerDataRegistry. Holds references to a PlayerAuthenticator and
    ///   as many PlayerDatabases as necessary.<br/>
    /// </summary>
    public partial class PlayerDataRegistry : MonoBehaviour 
    {

        /// <summary>
        /// Singleton instance for the PlayerDataRegistry
        /// </summary>
        public static PlayerDataRegistry Instance { get; private set; }

        [SerializeField]
        private BLogChannel logSettings;
        public BLogChannel LogSettings => logSettings;
        [SerializeField]
        private BLogChannel logSettingsPlayerData;
        public BLogChannel LogSettingsPlayerData => logSettingsPlayerData;

        /// <summary>
        /// All tracked PlayerData objects, mapped using their IdentifierData#uid value.
        /// </summary>
        internal Dictionary<string, PlayerData> PlayerDatas { get; private set; }

        [SerializeField]
        private LoginHandler _loginHandler;
        /// <summary>
        /// The LoginHandler the PlayerDataRegistry is currently using.
        /// </summary>
        public LoginHandler LoginHandler {
            get => _loginHandler;
            set {
                LoginHandler oldValue = _loginHandler;
                _loginHandler = value;
                LoginHandlerChangeEvent?.Invoke(oldValue, _loginHandler);
            }
        }

        public delegate void PlayerDataUpdatedHandler(PlayerData playerData, PlayerDataClass newData);
        /// <summary>
        /// This event is called when PlayerData updateds, the data that was changed is passed for
        ///   comparison purposes.
        /// </summary>
        public event PlayerDataUpdatedHandler PlayerDataUpdatedEvent;
        public delegate void LoginHandlerChangeHandler(LoginHandler oldHandler, LoginHandler newHandler);
        /// <summary>
        /// The login handler changed event dictates when the login handler has changed, this
        ///   allows for classes who are subscribed to the events to update.
        /// </summary>
        public event LoginHandlerChangeHandler LoginHandlerChangeEvent;

        private void Awake() 
        {
            // Set up singleton instance.
            if(Instance != null) {
                Destroy(gameObject);
                Debug.Log($"Destroyed newly spawned PlayerDataRegistry since singleton Instance already exists.");
                return;
            }

            if(LoginHandler == null) {
                Debug.LogWarning("No LoginHandler on the PlayerDataRegistry, setting to RandomLoginHandler.");
                LoginHandler = gameObject.AddComponent<RandomLoginHandler>();
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            NetworkedAwake();
            DatabaseAwake();
            PermissionsAwake();

            PlayerDatas = new();
        }

        /// On application quit call to remove all local players
        private void OnApplicationQuit() 
        {
            if(Networked)
                return;

            List<PlayerData> saveable = PlayerDatas.Values.ToList();
            saveable.ForEach(pd => Remove(pd));
        }

        private void OnEnable() 
        {
            NetworkOnEnable();
        }

        private void OnDisable() 
        {
            NetworkOnDisable();
        }

        private void Update() 
        {
            NetworkedUpdate();
        }

#region Data management
        /// <summary>
        /// Add PlayerData to the registry.
        /// </summary>
        /// <param name="data">The data to be added</param>
        /// <param name="batchOperation">Shows that this is part of a group of PlayerDatas being
        ///    added, used in HandleNetworkAdd.</param>
        /// <exception cref="RegistryOperationException">Thrown when the data doesn't have
        ///    Identifier data or the registry already contains the data. </exception>
        public void Add(PlayerData data, bool batchOperation = false) 
        {
            // Ensure the PlayerData has its uid
            if(!data.HasData<IdentifierData>())
                throw new RegistryOperationException("Can't register new PlayerData. It doesn't have any IdentifierData.");

            // Ensure the PlayerData isn't already in this PlayerDataRegistry
            if(PlayerDatas.ContainsKey(data.GetUID()))
                throw new RegistryOperationException("Can't register new PlayerData: The uid provided is already registered with this PlayerDataRegistry.");

            BLog.Log($"Adding data with UID \"{data.GetUID()}\" to the data registry.", LogSettings, 2);

            if(!Networked) { 
                PlayerDatas.Add(data.GetUID(), data); // If the registry isn't networked add the new data normally
            } else {
                HandleNetworkAdd(data, batchOperation); // Pass add behaviour to be handled by networked registry.
            }            

            LoadDatabaseEntries(data);
        }

        /// <summary>
        /// Remove a PlayerData from the registry.
        /// </summary>
        /// <param name="data">The data to be removed</param>
        /// <param name="batchOperation">Shows that this is part of a group of PlayerDatas being
        ///    removed, used in HandleNetworkRemove.</param>
        /// <exception cref="RegistryOperationException">Thrown when the data doesn't have
        ///    Identifier data or the registry doesn't contain the data. </exception>
        public void Remove(PlayerData data, bool batchOperation = false)
        {
            // Ensure the PlayerData has its uid
            if(!data.HasData<IdentifierData>())
                throw new RegistryOperationException("Can't remove player data it does not have IdentifierData.");

            // Ensure the PlayerData is in the registry
            if(!PlayerDatas.ContainsKey(data.GetUID()))
                throw new RegistryOperationException($"Can't remove player \"{data.GetUID()}\" because it is not in the PlayerDataRegistry");

            BLog.Log($"Removing data with UID \"{data.GetUID()}\" from the data registry.", LogSettings, 2);

            SaveDatabaseEntries(data);

            if(!Networked) { 
                PlayerDatas.Remove(data.GetUID()); // If the registry isn't networked remove the data normally
            } else {
                HandleNetworkRemove(data, batchOperation); // Pass remove behaviour to be handled by networked registry.
            }
        }

        public bool Contains(string uid) => PlayerDatas.ContainsKey(uid);

        /// <summary>
        /// Update the player data to the new version provided in the registry.
        /// </summary>
        /// <param name="data">The data to be updated with</param>
        /// <param name="overrideNetworkUpdate">Don't handle this update on the networking side,
        ///    only update the data locally. Used in the PlayerDataUpdateBroadcast callback.</param>
        /// <exception cref="RegistryOperationException">Thrown if the data doesn't have a UID or 
        ///    the data isn't in the registry</exception>
        public void UpdatePlayerData(PlayerData data, Type updatedType, bool overrideNetworkUpdate = false) 
        {
            // Ensure the PlayerData has its uid
            if(!data.HasData<IdentifierData>())
                throw new RegistryOperationException("Can't update player data it does not have IdentifierData.");

            if(!Contains(data.GetUID()))
                throw new RegistryOperationException("Can't update player data it's not in the registry");

            if(!Networked || overrideNetworkUpdate) { 
                PlayerDatas[data.GetUID()] = data; // If the registry isn't networked update the data normally
            } else {
                HandleNetworkUpdate(data, updatedType); // Pass update behaviour to be handled by networked registry.
            }

            // Invoke update event
            PlayerDataUpdatedEvent?.Invoke(data, data.GetData(updatedType));
        }

        public PlayerData GetPlayerData(string uid) 
        {
            if(!PlayerDatas.ContainsKey(uid)) {
                Debug.LogError("Cant get PlayerData, it is not registered with this PlayerDataRegistry.");
                return null;
            }
            return PlayerDatas[uid];
        }

        public PlayerData[] GetAllData() 
        {
            if(PlayerDatas.Values.Count == 0)
                return new PlayerData[0];

            return PlayerDatas.Values.ToArray();
        }
#endregion

        public static Type GetTypeByName(string typeName)
        {
            if(typeName == null || typeName.Length == 0)
                return null;

            // Search in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {                
                var type = assembly.GetType(typeName);
                if (type != null) {
                    if(typeof(PlayerDataClass).IsAssignableFrom(type))
                        return type;
                    else
                        Debug.LogWarning($"Trying to resolve type name \"{typeName}\" and found type \"{type.FullName}\" but it is not a child class of PlayerDataClass");
                }
            }
            return null;
        }

        public static Type GetLoginHandlerTypeByName(string typeName)
        {
            if(typeName == null || typeName.Length == 0)
                return null;

            // Search in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {                
                var type = assembly.GetType(typeName);
                if (type != null) {
                    if(typeof(LoginHandler).IsAssignableFrom(type))
                        return type;
                    else
                        Debug.LogWarning($"Trying to resolve type name \"{typeName}\" and found type \"{type.FullName}\" but it is not a child class of LoginHandler");
                }
            }
            return null;
        }
    }

    [Serializable]
    public class RegistryOperationException : Exception
    {
        public RegistryOperationException() : base() {}
        public RegistryOperationException(string message) : base(message) {}
        public RegistryOperationException(string message, Exception innerException) : base(message, innerException) {}
        protected RegistryOperationException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }
}