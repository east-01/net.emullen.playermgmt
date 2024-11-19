using System.Collections.Generic;
using System.Linq;
using EMullen.Core;
using FishNet;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    [RequireComponent(typeof(PlayerManager))]
    /// <summary>
    /// The PlayerDataRegistry stores all PlayerData for the local instance. If using
    ///   FishNetworking, the data will sync with the server.<br\>
    ///   
    /// NetworkedPlayerDataRegistry:<br/>
    /// The networking backend for the PlayerDataRegistry. Uses broadcasts to synchronize with the
    ///   server's PlayerDataRegistry.<br/>
    /// Has multiple phases for joining/leaving so that data doesn't get lost during
    /// synchronization.
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

        private void Awake() 
        {
            // Set up singleton instance.
            if(Instance != null) {
                Destroy(gameObject);
                Debug.Log($"Destroyed newly spawned PlayerDataRegistry since singleton Instance already exists.");
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            NetworkedAwake();

            PlayerDatas = new();
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
        public void Add(PlayerData newData) 
        {
            // Ensure the PlayerData has it's uid
            if(!newData.HasData<IdentifierData>()) {
                Debug.LogError("Can't register new PlayerData. It doesn't have any IdentifierData.");
                return;
            }

            // Ensure the PlayerData isn't already in this PlayerDataRegistry
            if(PlayerDatas.ContainsKey(newData.GetUID())) {
                Debug.LogError("Can't register new PlayerData: The uid provided is already registered with this PlayerDataRegistry.");
                return;
            }

            // Add the player to the PlayerDataRegistry
            PlayerDatas.Add(newData.GetUID(), newData);

            // Synchronize with server if this instance is networked
            if(Networked) {
                InstanceFinder.ClientManager.Broadcast(new PDRSyncBroadcast(PlayerDatas));
            }
        }

        public void Remove(PlayerData playerData)
        {
#if FISHNET
            if(UsingNetworkedRegistry) {
                NetworkedPlayerDataRegistry.Instance.Remove(playerData);
                return;
            }
#endif
            if(!playerData.HasData<IdentifierData>()) {
                Debug.LogError("Can't remove player data it does not have IdentifierData.");
                return;
            }
            string uid = playerData.GetData<IdentifierData>().uid;
            if(!PlayerDatas.ContainsKey(uid)) {
                Debug.LogError($"Can't remove player \"{uid}\" because it is not in the PlayerDataRegistry");
                return;
            }
            PlayerDatas.Remove(uid);
        }

        public bool Contains(string uid) => PlayerDatas.ContainsKey(uid);

        public void UpdatePlayerData(PlayerData playerData, string uid = null) 
        {
#if FISHNET
            if(UsingNetworkedRegistry) {                
                NetworkedPlayerDataRegistry.Instance.UpdatePlayerData(playerData, uid);
                return;
            }
#endif
            uid ??= playerData.GetUID();

            if(PlayerDatas.ContainsKey(uid)) {
                PlayerDatas[uid] = playerData;
            } else {
                PlayerDatas.Add(uid, playerData);
            }
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

    }
}