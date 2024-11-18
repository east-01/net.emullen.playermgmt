using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EMullen.Core;
#if FISHNET
using FishNet;
using FishNet.Transporting;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
#endif

namespace EMullen.PlayerMgmt 
{
    [RequireComponent(typeof(PlayerManager))]
    /// <summary>
    /// The PlayerDataRegistry stores all PlayerData for the local instance. If using a networked
    ///   setup, the data will sync with the server.<br\>
    /// <br\>
    /// There are several phases to joining the networked registry, see their detailed explanations
    ///   in their enum summaries.
    /// </summary>
    public class PlayerDataRegistry : MonoBehaviour
    {

        public static PlayerDataRegistry Instance { get; private set;}

        [SerializeField]
        private GameObject NetworkedPlayerDataRegistryPrefab;
        
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

#if FISHNET
        private NetworkedPDRPhase networkPhase;
        /// <summary>
        /// The NetworkedPDRPhase represents what part of the connection process we're in to the
        ///   networked player data registry. See NetworkedPDRPhase for details on each phase.
        /// </summary>
        public NetworkedPDRPhase NetworkPhase { 
            get {
                return networkPhase;
            }
            private set {
                NetworkedPDRPhase prev = networkPhase;
                networkPhase = value;
                NetworkPhaseChangedEvent?.Invoke(prev, networkPhase);
            }
        }
        private bool CheckUsingNetworkedRegistry() => 
            PlayerManager.Instance.LocalPlayers != null &&
            NetworkedPlayerDataRegistry.Instantiated && 
            PlayerManager.Instance.LocalPlayers.Where(lp => lp != null).ToList().All(lp => NetworkedPlayerDataRegistry.Instance.PlayerDatas.ContainsKey(lp.UID));
        public bool UsingNetworkedRegistry => NetworkPhase == NetworkedPDRPhase.IN_USE;

        [SerializeField]
        private float joinMessageTimeout = 10;
        /// <summary>
        /// The time since we last sent NetworkedPlayerDataRegistry#JoinRegistry message, set from
        ///   the JoinRegistry method itself.
        /// </summary>
        internal float lastJoinTime = -1;

        public delegate void NetworkPhaseChangedHandler(NetworkedPDRPhase prev, NetworkedPDRPhase current);
        public event NetworkPhaseChangedHandler NetworkPhaseChangedEvent;

        private NetworkManager networkManager;

#region Events
        public delegate void NetworkRegistryStatusChanged();
        public event NetworkRegistryStatusChanged NetworkRegistryStatusChangedEvent;
#endregion
#endif

        private void Awake() 
        {
            if(Instance != null) {
                Destroy(gameObject);
                Debug.Log($"Destroyed newly spawned PlayerDataRegistry since singleton Instance already exists.");
                return;
            } else {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }

#if FISHNET
            networkManager = null;

            SubscribeToNetworkEvents();

            NetworkPhaseChangedEvent += NetworkedPDRPhaseChangeEvent;
            NetworkRegistryStatusChangedEvent += NetworkRegistryStatusChangedEventMethod;
#endif

            PlayerDatas = new();
        }

        private void OnDestroy() 
        {
#if FISHNET
            NetworkPhaseChangedEvent -= NetworkedPDRPhaseChangeEvent;
            NetworkRegistryStatusChangedEvent -= NetworkRegistryStatusChangedEventMethod;
#endif

            if(networkManager != null) {
                networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
                networkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
                networkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
            }
        }

        private void Update() 
        {    
#if FISHNET
            if(networkManager == null)
                SubscribeToNetworkEvents();
            UpdatePhase();
#endif
        }

#region Data management
        public void Add(PlayerData newData) 
        {
#if FISHNET
            if(UsingNetworkedRegistry) {
                NetworkedPlayerDataRegistry.Instance.Add(newData);
                return;
            }
#endif
            if(!newData.HasData<IdentifierData>()) {
                Debug.LogError("Can't register new PlayerData. It doesn't have any IdentifierData.");
                return;
            }
            string uid = newData.GetData<IdentifierData>().uid;
            if(PlayerDatas.ContainsKey(uid)) {
                Debug.LogError("Can't register new PlayerData: The uid provided is already registered with this PlayerDataRegistry.");
                return;
            }
            PlayerDatas.Add(uid, newData);
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

#if FISHNET
#region Networked registry
#region Network events
        private void SubscribeToNetworkEvents()
        {
            if(NetworkManager.Instances.Count == 0) 
                return;
            
            networkManager = InstanceFinder.NetworkManager;
            networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
            networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;

            BLog.Log("Subscribed to network events.", LogSettings, 1);
        }

        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if(args.ConnectionState == LocalConnectionState.Started) {
                GameObject instantiated = Instantiate(NetworkedPlayerDataRegistryPrefab);
                InstanceFinder.ServerManager.Spawn(instantiated.GetComponent<NetworkObject>());
            }
        }

        private void ServerManager_OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if(args.ConnectionState == RemoteConnectionState.Stopped)
                NetworkedPlayerDataRegistry.Instance.RemoveAll(connection);
        }

        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args) 
        {
            if(args.ConnectionState == LocalConnectionState.Stopping) {
                NetworkPhase = NetworkedPDRPhase.DISCONNECTING; 
            }
        }
#endregion

        private void SynchronizeWithNetworkedPDR() => PlayerDatas = NetworkedPlayerDataRegistry.Instance.PlayerDatas.ToDictionary(kp => kp.Key, kp => kp.Value);

        private void UpdatePhase() 
        {
            switch(NetworkPhase) {
                case NetworkedPDRPhase.DISABLED:
                    if(PlayerManager.Instance.LocalPlayers != null && NetworkedPlayerDataRegistry.Instance != null)
                        NetworkPhase = NetworkedPDRPhase.JOINING;
                    break;
                case NetworkedPDRPhase.JOINING:
                    if(lastJoinTime == -1 || (Time.time - lastJoinTime > joinMessageTimeout))
                        JoinNetworkedRegistry();

                    if(CheckUsingNetworkedRegistry())
                        NetworkPhase = NetworkedPDRPhase.IN_USE;
                    break;
                case NetworkedPDRPhase.IN_USE:
                    break;
                case NetworkedPDRPhase.DISCONNECTING:
                    if(PlayerDatas.Values.All(pd => !pd.HasData<NetworkIdentifierData>()))
                        NetworkPhase = NetworkedPDRPhase.DISABLED;
                    break;
            }
        }

        private void NetworkedPDRPhaseChangeEvent(NetworkedPDRPhase prev, NetworkedPDRPhase curr) 
        {
            BLog.Log($"NetworkedPDR phase changed from {prev} to {curr}.", logSettings, 2);
            if(prev == NetworkedPDRPhase.IN_USE || curr == NetworkedPDRPhase.IN_USE) {
                NetworkRegistryStatusChangedEvent?.Invoke();
                BLog.Log($"Using networked registry: {curr == NetworkedPDRPhase.IN_USE}", LogSettings, 2);
            }
        }

        private void NetworkRegistryStatusChangedEventMethod() 
        {
            if(UsingNetworkedRegistry) {
                NetworkedPlayerDataRegistry.Instance.PlayerDatas.OnChange += NetworkedPlayerDataRegistryChanged;

                SynchronizeWithNetworkedPDR();
            } else {
                NetworkedPlayerDataRegistry.Instance.PlayerDatas.OnChange -= NetworkedPlayerDataRegistryChanged;      

                // Get the remotes player data dictionary, and select the key/value pairs where the
                //   key is in the localUID list and convert that to a dictionary.
                IEnumerable<string> localUIDList = PlayerManager.Instance.LocalPlayers.Where(lp => lp != null).ToList().Select(lp => lp.UID);
                Dictionary<string, PlayerData> remoteDatas = PlayerDatas.Where(kp => localUIDList.Contains(kp.Key)).ToDictionary(kp => kp.Key, kp => kp.Value);
                remoteDatas.Values.ToList().ForEach(pd => pd.ClearData<NetworkIdentifierData>());
                PlayerDatas = remoteDatas;
                BLog.Log($"Transferred remote registry back to local. ({PlayerDatas.Count} value(s))", logSettings, 3);
            }
        }

        private void NetworkedPlayerDataRegistryChanged(SyncDictionaryOperation op, string key, PlayerData value, bool asServer) => SynchronizeWithNetworkedPDR();

        private void JoinNetworkedRegistry() 
        {
            lastJoinTime = Time.time;
            NetworkedPlayerDataRegistry.Instance.AddAll(NetworkedPlayerDataRegistry.Instance.LocalConnection, PlayerDatas.Values.ToList());
        }  
#endregion
#endif
    }

#if FISHNET
    /// <summary>
    /// Each phase is activated a different way and represents a different function.
    /// </summary>
    public enum NetworkedPDRPhase 
    {
        /// <summary><br/>
        /// Set from:<br/>
        /// - Initialization<br/>
        /// - DISCONNECTING phase when we the data is done copying<br/>
        /// Runs when:<br/>
        /// - The PlayerDataRegistry is using its local data, not the data from the NPDR<br/>
        /// </summary>
        DISABLED, 
        
        /// <summary>
        /// Set from:<br/>
        /// - DISABLED phase when the NetworkedPlayerDataRegistry is instantiated and the local<br/>
        ///   player's uids are not in the networked registry.<br/>
        /// Runs when:<br/>
        /// - The PlayerDataRegistry has sent NetworkedPlayerDataRegistry#JoinRegistry<br/>
        /// </summary>
        JOINING, 
        
        /// <summary>
        /// Set from:<br/>
        /// - JOINING phase once the NetworkedPlayerDataRegistry has all of the local player's uids.<br/>
        /// Runs when:<br/>
        /// - The NetworkedPlayerDataRegistry is in use, all data will be synchronized over the <br/>
        ///   network.<br/>
        /// </summary>
        IN_USE, 
        
        /// <summary>
        /// Set from:<br/>
        /// - IN_USE once the client is losing connection<br/>
        /// Runs when:<br/>
        /// - The client is copying the data from the networked registry and placing it in local 
        ///   registry<br/>
        /// </summary>
        DISCONNECTING
    }
#endif
}