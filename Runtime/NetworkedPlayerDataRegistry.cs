using UnityEngine;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet;
using System;
using FishNet.Connection;
using System.Linq;
using EMullen.Core;

namespace EMullen.PlayerMgmt 
{
    public class NetworkedPlayerDataRegistry : NetworkBehaviour
    {
        public static NetworkedPlayerDataRegistry Instance { get; private set; }
        public static bool Instantiated => Instance != null;
        
        internal readonly SyncDictionary<string, PlayerData> PlayerDatas = new();

        private void Awake() 
        {
            if(Instance != null) {
                Debug.LogWarning($"The PlayerDataNetworkedRegistry is a singleton and is already instantiated, destroying owner GameObject \"{gameObject.name}\"");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void Add(PlayerData playerData) => Add(LocalConnection, playerData);

        public void Add(NetworkConnection connection, PlayerData playerData)
        {
            if(!IsServerInitialized) {
                ServerRpcAdd(connection, playerData);
                return;
            }

            if(PlayerDatas.ContainsKey(playerData.GetUID())) {
                Debug.LogError($"Can't add PlayerData with uid \"{playerData.GetUID()}\" it is already in registry.");
                return;
            }

            // Collect all siblings in the PlayerData registries IDs
            List<string> siblingUIDs = PlayerDatas.Values.Where(pd => pd.GetData<NetworkIdentifierData>().clientID == connection.ClientId).Select(pd => pd.GetUID()).ToList();
            siblingUIDs.Add(playerData.GetUID());

            PlayerDatas.Add(playerData.GetUID(), playerData);

            NetworkIdentifierData newNetID = new() {
                clientID = connection.ClientId,
                connectionsUIDs = siblingUIDs
            };

            siblingUIDs.ForEach(uid => PlayerDatas[uid].SetData(newNetID));
        }

        public void AddAll(NetworkConnection connection, List<PlayerData> playerData) => playerData.ForEach(pd => Add(connection, pd));

        [ServerRpc(RequireOwnership = false)]
        private void ServerRpcAdd(NetworkConnection connection, PlayerData playerData) => Add(connection, playerData);

        public void Remove(PlayerData playerData)
        {
            if(!IsServerInitialized) {
                ServerRpcRemove(playerData);
                return;
            }

            if(!PlayerDatas.ContainsKey(playerData.GetUID())) {
                Debug.LogError($"Can't add PlayerData with uid \"{playerData.GetUID()}\" it is already in registry.");
                return;
            }

            List<string> siblingUIDs = playerData.GetData<NetworkIdentifierData>().connectionsUIDs;
            siblingUIDs.Remove(playerData.GetUID());

            NetworkIdentifierData newIDData = new() {
                clientID = playerData.GetData<NetworkIdentifierData>().clientID,
                connectionsUIDs = siblingUIDs
            };

            playerData.ClearData<NetworkIdentifierData>();

            PlayerDatas.Remove(playerData.GetUID());

            siblingUIDs.ForEach(uid => PlayerDatas[uid].SetData(newIDData));
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRpcRemove(PlayerData playerData) => Remove(playerData);

        public void RemoveAll(NetworkConnection connection) 
        {
            if(!IsServerInitialized) {
                ServerRpcRemoveAll(connection);
                return;
            }
            List<PlayerData> playerDatasToRemove = new(PlayerDatas.Values.Where(pd => pd.GetData<NetworkIdentifierData>().clientID == connection.ClientId));
            playerDatasToRemove.ForEach(playerData => Remove(playerData));
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRpcRemoveAll(NetworkConnection connection) => RemoveAll(connection);

        public bool Contains(string uid) => PlayerDatas.ContainsKey(uid);
        public bool Contains(PlayerData playerData) => Contains(playerData.GetUID());

        internal void UpdatePlayerData(PlayerData playerData, string uid = null)
        {
            if(!IsServerInitialized) {
                ServerRpcUpdateData(playerData, uid);
                return;
            }

            uid ??= playerData.GetUID();

            if(PlayerDatas.ContainsKey(uid)) {
                PlayerDatas[uid] = playerData;
            } else {
                PlayerDatas.Add(uid, playerData);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRpcUpdateData(PlayerData data, string uid = null) => UpdatePlayerData(data, uid);

    }

}