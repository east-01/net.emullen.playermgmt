using System;
using System.Collections.Generic;
using System.Linq;
using EMullen.Core;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    // Player Data Registry Networked - Handles all interactions over the network
    public partial class PlayerDataRegistry : MonoBehaviour
    {

        /// <summary>
        /// The NetworkManager who's events we're subscribed to.
        /// </summary>
        private NetworkManager networkManager; 

        private NetworkedRegistryPhase networkPhase;
        /// <summary>
        /// The NetworkedPDRPhase represents what part of the connection process we're in to the
        ///   networked player data registry. See NetworkedPDRPhase for details on each phase.
        /// </summary>
        public NetworkedRegistryPhase NetworkPhase { 
            get => networkPhase;
            private set {
                NetworkedRegistryPhase prev = networkPhase;
                networkPhase = value;
                NetworkPhaseChangedEvent?.Invoke(prev, networkPhase);
                BLog.Log($"Updated network phase to {value}", LogSettings, 4);
            }
        }

        /// <summary>
        /// Is this PlayerDataRegistry synchronized with the network.
        /// </summary>
        public bool Networked => NetworkPhase == NetworkedRegistryPhase.IN_USE;
        /// <summary>
        /// Is the PlayerDataRegistry transitioning between synchonized/unsynchronized.
        /// </summary>
        public bool Transitioning => NetworkPhase == NetworkedRegistryPhase.JOINING || NetworkPhase == NetworkedRegistryPhase.DISCONNECTING;

        [SerializeField]
        private float joinBroadcastTimeout = 10;
        /// <summary>
        /// The time since we last sent the PDRSyncBroadcast to the server, set from the joining
        ///   phases client section in UpdatePhase.
        /// </summary>
        internal float lastBroadcastTime = -1;

#region Events
        public delegate void NetworkPhaseChangedHandler(NetworkedRegistryPhase prev, NetworkedRegistryPhase current);
        public event NetworkPhaseChangedHandler NetworkPhaseChangedEvent;
#endregion

#region Initializers
        private void NetworkedAwake() 
        {

        }

        private void NetworkOnEnable() 
        {
            InstanceFinder.ServerManager.RegisterBroadcast<RegistryJoinBroadcast>(OnRegistryJoinBroadcast);
            InstanceFinder.ServerManager.RegisterBroadcast<RegistryOperationBroadcast>(OnRegistryOperationBroadcast);
            InstanceFinder.ServerManager.RegisterBroadcast<PlayerDataUpdateBroadcast>(OnPlayerDataUpdateBroadcastFromClient);

            InstanceFinder.ClientManager.RegisterBroadcast<RegistrySyncBroadcast>(OnRegistrySyncBroadcastFromServer);
            InstanceFinder.ClientManager.RegisterBroadcast<PlayerDataUpdateBroadcast>(OnPlayerDataUpdateBroadcastFromServer);

            SubscribeToNetworkEvents();
        }

        private void NetworkOnDisable() 
        {
            if(InstanceFinder.ServerManager != null) {
                InstanceFinder.ServerManager.UnregisterBroadcast<RegistryJoinBroadcast>(OnRegistryJoinBroadcast);
                InstanceFinder.ServerManager.UnregisterBroadcast<RegistryOperationBroadcast>(OnRegistryOperationBroadcast);
                InstanceFinder.ServerManager.UnregisterBroadcast<PlayerDataUpdateBroadcast>(OnPlayerDataUpdateBroadcastFromClient);
            }

            if(InstanceFinder.ClientManager != null) {
                InstanceFinder.ClientManager.UnregisterBroadcast<RegistrySyncBroadcast>(OnRegistrySyncBroadcastFromServer);
                InstanceFinder.ClientManager.UnregisterBroadcast<PlayerDataUpdateBroadcast>(OnPlayerDataUpdateBroadcastFromServer);
            }

            UnsubscribeFromNetworkEvents();
        }
#endregion

        private void NetworkedUpdate() 
        {
            UpdatePhase();
            
            // Constantly attempt to subscribe to net events in case we weren't able to on the
            //   first try.
            SubscribeToNetworkEvents();
        }

#region Operation extensions
        /// <summary>
        /// Extension over the regular add that sets the PlayerDatas NetworkIdentifierData as it's
        ///   being added.
        /// </summary>
        /// <param name="connection">The connection that the PlayerData belongs to</param>
        /// <param name="data">The PlayerData being added</param>
        /// <param name="batchOperation">Is this a part of a batch operation</param>
        private void NetworkAdd(NetworkConnection connection, PlayerData data, bool batchOperation = false) 
        {
            // Create NetworkIdentifierData
            NetworkIdentifierData newNID = new(connection.ClientId);
        
            // Apply new NetworkIdentifierData and add to the registry
            data.SetData(newNID, false);
            Add(data, batchOperation);
        }

        /// <summary>
        /// Disconnect a connection from the network.
        /// </summary>
        /// <param name="connection"></param>
        private void NetworkRemove(NetworkConnection connection) 
        {
            // Collect PlayerDatas with connection ids matching the remote connection
            PlayerData[] matchingConnectionIDs = GetAllData().ToList().Where(pd => pd.GetData<NetworkIdentifierData>().clientID == connection.ClientId).ToArray();
            for(int i = 0; i < matchingConnectionIDs.Length; i++) {
                Remove(matchingConnectionIDs[i], i < matchingConnectionIDs.Length - 1);
            }

            // Create a new Dictionary to be sent in the sync broadcast
            Dictionary<string, PlayerData> newData = matchingConnectionIDs.ToDictionary(pd => pd.GetUID(), pd =>pd);
            RegistrySyncBroadcast broadcast = new(newData);
            InstanceFinder.ServerManager.Broadcast(connection, broadcast);

            BLog.Log($"Removing connection id {connection.ClientId} from network.", LogSettings, 1);
        }
#endregion

#region Operation visitors
        /// <summary>
        /// Called in PlayerDataRegistry#Add to handle the add operation
        /// </summary>
        /// <param name="data">The data that is being added</param>
        private void HandleNetworkAdd(PlayerData data, bool batchOperation = false) 
        {
            if(InstanceFinder.IsServerStarted) {
                PlayerDatas.Add(data.GetUID(), data);
                if(!batchOperation)
                    SendSynchronizeBroadcast(RegistrySyncBroadcast.Reason.PLAYER_LIST_CHANGED);
            } else if(InstanceFinder.IsClientOnlyStarted) {
                InstanceFinder.ClientManager.Broadcast(new RegistryOperationBroadcast(data, RegistryOperationBroadcast.Operation.ADD));
            } else
                Debug.LogWarning("Attempting to handle networked add but don't know how to handle server/client setup.");
        }

        /// <summary>
        /// Called in PlayerDataRegistry#Remove to handle the remove operation
        /// </summary>
        /// <param name="data">The data that is being removed</param>
        private void HandleNetworkRemove(PlayerData data, bool batchOperation = false) 
        {
            if(InstanceFinder.IsServerStarted) {
                PlayerDatas.Remove(data.GetUID());
                if(!batchOperation)
                    SendSynchronizeBroadcast(RegistrySyncBroadcast.Reason.PLAYER_LIST_CHANGED);
            } else if(InstanceFinder.IsClientOnlyStarted) {
                InstanceFinder.ClientManager.Broadcast(new RegistryOperationBroadcast(data, RegistryOperationBroadcast.Operation.REMOVE));
            } else
                Debug.LogWarning("Attempting to handle networked add but don't know how to handle server/client setup.");            
        }

        /// <summary>
        /// Called in PlayerDataRegistry#UpdatePlayerData to handle the update operation
        /// </summary>
        /// <param name="data"></param>
        private void HandleNetworkUpdate(PlayerData data, Type updatedType) 
        {
            PlayerDatas[data.GetUID()] = data;

            if(InstanceFinder.IsServerStarted) {
                SendUpdateBroadcast(data, updatedType);
            } else if(InstanceFinder.IsClientOnlyStarted) {
                InstanceFinder.ClientManager.Broadcast(new PlayerDataUpdateBroadcast(data, updatedType.FullName));
            } else
                Debug.LogWarning("Attempting to handle networked add but don't know how to handle server/client setup.");            
        }
#endregion

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
                case NetworkedRegistryPhase.DISABLED:
                    if(PlayerManager.Instance.LocalPlayers != null && isNetworkActive)
                        NetworkPhase = NetworkedRegistryPhase.JOINING;
                    break;

                // In the joining phase there are two options:
                // For the server: we're just adding NetworkIdentifierData to all local PlayerDatas.
                // For the client: we send a PDRSyncBroadcast to send our local data and wait to
                //   recieve a response PDRSyncBroadcast from the server containing our new data
                //   with NetworkIdentifierDatas added.
                case NetworkedRegistryPhase.JOINING:
                    if(InstanceFinder.IsServerStarted) {
                        // Get local siblingUIDs and create NetworkIdentifierData
                        List<string> siblingUIDs = PlayerDatas.Values.Select(pd => pd.GetUID()).ToList();
                        NetworkIdentifierData newNID = new(-1);

                        // Apply new NetworkIdentifierData
                        PlayerDatas.Values.ToList().ForEach(pd => {
                            pd.SetData(newNID);
                        });

                        // Move on to next phase since we don't have to wait for any response.
                        NetworkPhase = NetworkedRegistryPhase.IN_USE;

                    } else if(InstanceFinder.IsClientOnlyStarted) {
                        if(lastBroadcastTime == -1 || (Time.time - lastBroadcastTime > joinBroadcastTimeout)) {
                            // Create broadcast and send to server
                            RegistryJoinBroadcast broadcast = new(PlayerDatas.Values.ToList());
                            InstanceFinder.ClientManager.Broadcast(broadcast);
                            lastBroadcastTime = Time.time;
                        }

                        // Get all local players, check if they're in the registry and if they have NetworkIdentifierData
                        bool HasPlayerDatas = PlayerDatas != null;
                        List<LocalPlayer> localPlayers = PlayerManager.Instance.LocalPlayers.Where(lp => lp != null && lp.UID != null).ToList();
                        bool allLPInRegistry = HasPlayerDatas && localPlayers.All(lp => PlayerDatas.ContainsKey(lp.UID));
                        bool allLPHasNID = HasPlayerDatas && localPlayers.All(lp => PlayerDatas[lp.UID].HasData<NetworkIdentifierData>());

                        // If the above conditions are met that means we've synced with the server's registry successfully.
                        if(allLPInRegistry && allLPHasNID)
                            NetworkPhase = NetworkedRegistryPhase.IN_USE;

                    } else 
                        Debug.LogWarning("Attempting to join but don't know how to handle server/client setup.");
                    
                    break;

                // In the in use phase we're keeping track of when the network becomes inactive so
                //   we can transition to the disconnecting phase when necessary.
                case NetworkedRegistryPhase.IN_USE:
                    // If the network is no longer active, transition to disconnecting phase
                    if(!isNetworkActive)
                        NetworkPhase = NetworkedRegistryPhase.DISCONNECTING;
                    break;

                // In the disconnecting phase we regenerate local PlayerDatas dictionary from the 
                //   existing (synchronized with server) one while removing NetworkIdentifierDatas.
                case NetworkedRegistryPhase.DISCONNECTING:
                    // Create new dictionary to replace the existing one
                    Dictionary<string, PlayerData> newPlayerDatas = new();

                    List<string> localUIDs = PlayerManager.Instance.LocalPlayers.Where(lp => lp != null && lp.UID != null).Select(lp => lp.UID).ToList();
                    localUIDs.ForEach(uid => {
                        // Get the PlayerData and clear its NetworkIdentifier data
                        PlayerData pd = PlayerDatas[uid];
                        pd.ClearData<NetworkIdentifierData>();

                        newPlayerDatas.Add(uid, pd);
                    });

                    // Overwrite existing PlayerDatas and set phase to disabled
                    PlayerDatas = newPlayerDatas;
                    NetworkPhase = NetworkedRegistryPhase.DISABLED;
                    break;
            }
        }
#endregion

#region Server side broadcast callbacks
        private void OnRegistryJoinBroadcast(NetworkConnection connection, RegistryJoinBroadcast broadcast, Channel channel)
        {
            // Collect the data to add as an array
            PlayerData[] datasToAdd = broadcast.datas.ToArray();
            // Iterate through the array with an index so we know when we're on the last element,
            //   this allows us to conclude the batch operation and send a sync broadcast.
            for(int i = 0; i < datasToAdd.Length; i++) {
                NetworkAdd(connection, datasToAdd[i], i < datasToAdd.Length - 1);
            }
        }

        /// <summary>The server side callback- the server recieves broadcast from client.</summary>
        private void OnRegistryOperationBroadcast(NetworkConnection clientIn, RegistryOperationBroadcast broadcast, Channel channel) 
        {
            // Take in the registry operation and perform its related function
            if(broadcast.operation == RegistryOperationBroadcast.Operation.ADD) {
                NetworkAdd(clientIn, broadcast.data);
            } else if(broadcast.operation == RegistryOperationBroadcast.Operation.REMOVE) {
                Remove(broadcast.data);
            }
        }

        /// <summary>The server side callback- the server recieves broadcast from client.</summary>
        private void OnPlayerDataUpdateBroadcastFromClient(NetworkConnection connection, PlayerDataUpdateBroadcast broadcast, Channel channel) 
        {
            Type updatedType = GetTypeByName(broadcast.typeName);

            // Ensure we've read the updated type correctly
            if(updatedType == null)
                throw new InvalidOperationException("FATAL: Failed to resolve type name from broadcast, can't proceed in updating locally and to other clients.");

            // Check if the connection has permission to update this type
            if(!CanModify(broadcast.data, updatedType, connection)) {
                SendSynchronizeBroadcastToClient(connection, RegistrySyncBroadcast.Reason.UPDATE_PERMISSION_DENIED);
                Debug.LogWarning($"Connection ID {connection.ClientId} tried to modify data type \"{updatedType.Name}\" without permission");
                return;
            }

            // Update the player data locally
            UpdatePlayerData(broadcast.data, updatedType);
            // Send update broadcast to all clients
            SendUpdateBroadcast(broadcast.data, updatedType);
        }

#endregion

#region Client side broadcast callbacks
        private void OnRegistrySyncBroadcastFromServer(RegistrySyncBroadcast broadcast, Channel channel)
        {
            if(broadcast.reason == RegistrySyncBroadcast.Reason.UPDATE_PERMISSION_DENIED)
                Debug.LogError("You were warned from the server for updating a PlayerDataClass you weren't allowed to. Registry has been re-synced.");

            PlayerDatas = broadcast.playerDatas;
        }

        /// <summary> The client side callback- the client recieves broadcast from server. </summary>
        private void OnPlayerDataUpdateBroadcastFromServer(PlayerDataUpdateBroadcast broadcast, Channel channel) 
        {
            Type updatedType = GetTypeByName(broadcast.typeName);

            // Ensure we've read the updated type correctly
            if(updatedType == null)
                throw new InvalidOperationException($"FATAL: Failed to resolve type name \"{broadcast.typeName}\" from broadcast, can't proceed in updating locally.");

            UpdatePlayerData(broadcast.data, updatedType, true);
        }
#endregion

#region Outgoing server side broadcasts
        /// <summary>
        /// Given the current state of the PlayerDataRegistry on the server, send a
        ///   RegistrySyncBroadcast to all clients.
        /// </summary>
        private void SendSynchronizeBroadcast(RegistrySyncBroadcast.Reason reason = RegistrySyncBroadcast.Reason.NONE) 
        {
            List<NetworkConnection> clients = InstanceFinder.ServerManager.Clients.Values.ToList();
            clients.ForEach(clientOut => SendSynchronizeBroadcastToClient(clientOut, reason));
        }

        private void SendSynchronizeBroadcastToClient(NetworkConnection client, RegistrySyncBroadcast.Reason reason = RegistrySyncBroadcast.Reason.NONE) 
        {
            // Create a new registry dictionary tuned to this client, with data types it's allowed to see.
            Dictionary<string, PlayerData> dictToClient = new();
            PlayerDatas.Values.ToList().ForEach(origData => {
                PlayerData data = origData.Clone();
                foreach(Type type in data.Types) {
                    if(!CanShow(data, type, client))
                        data.ClearData(type, false);
                }
                dictToClient.Add(data.GetUID(), data);
            });

            RegistrySyncBroadcast broadcast = new(dictToClient, reason);
            InstanceFinder.ServerManager.Broadcast(client, broadcast);                    
        }

        private void SendUpdateBroadcast(PlayerData updatedData, Type updatedType) 
        {
            List<NetworkConnection> clients = InstanceFinder.ServerManager.Clients.Values.ToList();
            clients.ForEach(clientOut => {
                // Only send the update if the client can see the data, block others here
                if(!CanShow(updatedData, updatedType, clientOut))
                    return;

                PlayerDataUpdateBroadcast broadcast = new(updatedData, updatedType.FullName);
                InstanceFinder.ServerManager.Broadcast(clientOut, broadcast);                    
            });
        }
#endregion

#region FishNet event handling
        private void SubscribeToNetworkEvents()
        {
            // Ensure there is a NetworkManager instance to subscribe to
            if(NetworkManager.Instances.Count == 0) 
                return;
            
            // Check if we're already subscribed to network events
            if(networkManager != null)
                return;

            networkManager = InstanceFinder.NetworkManager;
            networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;

            BLog.Log("Subscribed to network events.", LogSettings, 1);
        }

        private void UnsubscribeFromNetworkEvents() 
        {
            if(networkManager == null)
                return;

            networkManager = InstanceFinder.NetworkManager;
            networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
            networkManager.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
        }

        private void ServerManager_OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if(args.ConnectionState == RemoteConnectionState.Stopped)
                NetworkRemove(connection);
        }

        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if(args.ConnectionState == LocalConnectionState.Stopping) {
                InstanceFinder.ServerManager.Clients.ToList().ForEach(client => NetworkRemove(client.Value));
            }
        }

#endregion

    }

    /// <summary>
    /// Each phase is activated a different way and represents a different function.
    /// </summary>
    public enum NetworkedRegistryPhase 
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
}