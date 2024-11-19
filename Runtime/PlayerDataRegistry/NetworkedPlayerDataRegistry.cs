using System.Collections.Generic;
using System.Linq;
using Codice.CM.SEIDInfo;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    // Networked Player Data Registry
    public partial class PlayerDataRegistry : MonoBehaviour
    {

        private NetworkedPDRPhase networkPhase;
        /// <summary>
        /// The NetworkedPDRPhase represents what part of the connection process we're in to the
        ///   networked player data registry. See NetworkedPDRPhase for details on each phase.
        /// </summary>
        public NetworkedPDRPhase NetworkPhase { 
            get => networkPhase;
            private set {
                NetworkedPDRPhase prev = networkPhase;
                networkPhase = value;
                NetworkPhaseChangedEvent?.Invoke(prev, networkPhase);
            }
        }

        /// <summary>
        /// Is this PlayerDataRegistry synchronized with the network.
        /// </summary>
        public bool Networked => NetworkPhase == NetworkedPDRPhase.IN_USE;
        /// <summary>
        /// Is the PlayerDataRegistry transitioning between synchonized/unsynchronized.
        /// </summary>
        public bool Transitioning => NetworkPhase == NetworkedPDRPhase.JOINING || NetworkPhase == NetworkedPDRPhase.DISCONNECTING;

        [SerializeField]
        private float joinMessageTimeout = 10;
        /// <summary>
        /// The time since we last sent the PDRSyncBroadcast to the server, set from the joining
        ///   phases client section in UpdatePhase.
        /// </summary>
        internal float lastBroadcastTime = -1;

        public delegate void NetworkPhaseChangedHandler(NetworkedPDRPhase prev, NetworkedPDRPhase current);
        public event NetworkPhaseChangedHandler NetworkPhaseChangedEvent;

        private void NetworkedAwake() 
        {

        }

        private void NetworkOnEnable() 
        {
            InstanceFinder.ServerManager.RegisterBroadcast<PDRSyncBroadcast>(OnPDRSyncBroadcastFromClient);
            InstanceFinder.ServerManager.RegisterBroadcast<PlayerDataUpdateBroadcast>(OnPlayerDataUpdateBroadcastFromClient);

            InstanceFinder.ClientManager.RegisterBroadcast<PDRSyncBroadcast>(OnPDRSyncBroadcastFromServer);
            InstanceFinder.ClientManager.RegisterBroadcast<PlayerDataUpdateBroadcast>(OnPlayerDataUpdateBroadcastFromServer);
        }

        private void NetworkOnDisable() 
        {
            InstanceFinder.ServerManager.UnregisterBroadcast<PDRSyncBroadcast>(OnPDRSyncBroadcastFromClient);
            InstanceFinder.ServerManager.UnregisterBroadcast<PlayerDataUpdateBroadcast>(OnPlayerDataUpdateBroadcastFromClient);

            InstanceFinder.ClientManager.UnregisterBroadcast<PDRSyncBroadcast>(OnPDRSyncBroadcastFromServer);
            InstanceFinder.ClientManager.UnregisterBroadcast<PlayerDataUpdateBroadcast>(OnPlayerDataUpdateBroadcastFromServer);
        }

        private void NetworkedUpdate() 
        {
            UpdatePhase();
        }

#region Phase management
        private void UpdatePhase() 
        {
            // TODO: We'll probably have to check if the NetworkManager exists or something like
            //   that to ensure we don't run into NREs when checking the DISABLED phase.
            bool isNetworkActive = InstanceFinder.IsServerStarted || InstanceFinder.IsClientOnlyStarted;
            
            switch(NetworkPhase) {
                // In the disabled phase we look for when either the server is started or the
                //   the client is only started, this means we should be joining the networked
                //   player data registry.
                case NetworkedPDRPhase.DISABLED:
                    if(PlayerManager.Instance.LocalPlayers != null && isNetworkActive)
                        NetworkPhase = NetworkedPDRPhase.JOINING;
                    break;

                // In the joining phase there are two options:
                // For the server: we're just adding NetworkIdentifierData to all local PlayerDatas.
                // For the client: we send a PDRSyncBroadcast to send our local data and wait to
                //   recieve a response PDRSyncBroadcast from the server containing our new data
                //   with NetworkIdentifierDatas added.
                case NetworkedPDRPhase.JOINING:
                    if(InstanceFinder.IsServerStarted) {
                        // Get local siblingUIDs and create NetworkIdentifierData
                        List<string> siblingUIDs = PlayerDatas.Values.Select(pd => pd.GetUID()).ToList();
                        NetworkIdentifierData newNID = new(-1, siblingUIDs);

                        // Apply new NetworkIdentifierData
                        PlayerDatas.Values.ToList().ForEach(pd => {
                            pd.SetData(newNID);
                            UpdatePlayerData(pd, pd.GetUID());
                        });

                        // Move on to next phase since we don't have to wait for any response.
                        NetworkPhase = NetworkedPDRPhase.IN_USE;

                    } else if(InstanceFinder.IsClientOnlyStarted) {
                        if(lastBroadcastTime == -1 || (Time.time - lastBroadcastTime > joinMessageTimeout)) {
                            // Create broadcast and send to server
                            PDRSyncBroadcast broadcast = new(PlayerDatas);
                            InstanceFinder.ClientManager.Broadcast(broadcast);
                            lastBroadcastTime = Time.time;
                        }

                        // Get all local players, check if they're in the registry and if they have NetworkIdentifierData
                        List<LocalPlayer> localPlayers = PlayerManager.Instance.LocalPlayers.Where(lp => lp != null).ToList();
                        bool allLPInRegistry = localPlayers.All(lp => PlayerDatas.ContainsKey(lp.UID));
                        bool allLPHasNID = localPlayers.All(lp => PlayerDatas[lp.UID].HasData<NetworkIdentifierData>());

                        // If the above conditions are met that means we've synced with the server's registry successfully.
                        if(allLPInRegistry && allLPHasNID)
                            NetworkPhase = NetworkedPDRPhase.IN_USE;

                    } else 
                        Debug.LogWarning("Attempting to join but don't know how to handle server/client setup.");
                    
                    break;

                // In the in use phase we're keeping track of when the network becomes inactive so
                //   we can transition to the disconnecting phase when necessary.
                case NetworkedPDRPhase.IN_USE:
                    // If the network is no longer active, transition to disconnecting phase
                    if(!isNetworkActive)
                        NetworkPhase = NetworkedPDRPhase.DISCONNECTING;
                    break;

                // In the disconnecting phase we regenerate local PlayerDatas dictionary from the 
                //   existing (synchronized with server) one while removing NetworkIdentifierDatas.
                case NetworkedPDRPhase.DISCONNECTING:
                    // Create new dictionary to replace the existing one
                    Dictionary<string, PlayerData> newPlayerDatas = new();

                    List<string> localUIDs = PlayerManager.Instance.LocalPlayers.Where(lp => lp != null).Select(lp => lp.UID).ToList();
                    localUIDs.ForEach(uid => {
                        // Get the PlayerData and clear its NetworkIdentifier data
                        PlayerData pd = PlayerDatas[uid];
                        pd.ClearData<NetworkIdentifierData>();

                        newPlayerDatas.Add(uid, pd);
                    });

                    // Overwrite existing PlayerDatas and set phase to disabled
                    PlayerDatas = newPlayerDatas;
                    NetworkPhase = NetworkedPDRPhase.DISABLED;
                    break;
            }
        }
#endregion

#region Broadcast callbacks
        /// <summary>The server side callback- the server recieves broadcast from client.</summary>
        private void OnPDRSyncBroadcastFromClient(NetworkConnection connection, PDRSyncBroadcast broadcast, Channel channel) 
        {
            
        }

        /// <summary>The server side callback- the server recieves broadcast from client.</summary>
        private void OnPlayerDataUpdateBroadcastFromClient(NetworkConnection connection, PlayerDataUpdateBroadcast broadcast, Channel channel) 
        {

        }

        /// <summary> The client side callback- the client recieves broadcast from server. </summary>
        private void OnPDRSyncBroadcastFromServer(PDRSyncBroadcast broadcast, Channel channel) 
        {
            PlayerDatas = broadcast.PlayerDatas;
        }

        /// <summary> The client side callback- the client recieves broadcast from server. </summary>
        private void OnPlayerDataUpdateBroadcastFromServer(PlayerDataUpdateBroadcast broadcast, Channel channel) 
        {
            
        }
#endregion

    }

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

    public readonly struct PDRSyncBroadcast : IBroadcast 
    {
        public readonly Dictionary<string, PlayerData> PlayerDatas;

        public PDRSyncBroadcast(Dictionary<string, PlayerData> PlayerDatas) 
        {
            this.PlayerDatas = PlayerDatas;
        }
    }

    public readonly struct PlayerDataUpdateBroadcast : IBroadcast 
    {
        public readonly string updatedUID;
        public readonly PlayerDataClass updatedPlayerDataClass;
    }
}