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
    public class PlayerDataRegistryOld : MonoBehaviour
    {        

#if FISHNET
        

        private NetworkManager networkManager;

#region Events
        public delegate void NetworkRegistryStatusChanged();
        public event NetworkRegistryStatusChanged NetworkRegistryStatusChangedEvent;
#endregion
#endif

        private void Awake() 
        {
            

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
}