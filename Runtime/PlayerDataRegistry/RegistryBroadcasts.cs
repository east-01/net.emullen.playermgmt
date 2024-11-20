using System;
using System.Collections.Generic;
using FishNet.Broadcast;

namespace EMullen.PlayerMgmt 
{
    /// <summary>
    /// A broadcast to perform an operation on the PlayerDataRegistry when it is already 
    ///   synchronized (i.e. add or remove)
    /// </summary>
    public readonly struct RegistryOperationBroadcast : IBroadcast 
    {
        public readonly PlayerData data;
        public readonly Operation operation;

        public RegistryOperationBroadcast(PlayerData data, Operation operation) 
        {
            this.data = data;
            this.operation = operation;
        }

        /// <summary>
        /// A simple enum to represent each PlayerDataRegistryOperation
        /// </summary>
        [Serializable]
        public enum Operation { ADD, REMOVE }
    }

    /// <summary>
    /// A broadcast sent from the server to clients to dictate what the clients PlayerDataRegistry
    ///   should contain.
    /// </summary>
    public readonly struct RegistrySyncBroadcast : IBroadcast 
    {
        public readonly Dictionary<string, PlayerData> playerDatas;
        public RegistrySyncBroadcast(Dictionary<string, PlayerData> playerDatas) 
        {
            this.playerDatas = playerDatas;
        }
    }

    /// <summary>
    /// A broadcast sent from clients to the server to show that the client wants to join the
    ///   networked registry.
    /// </summary>
    public readonly struct RegistryJoinBroadcast : IBroadcast 
    {
        public readonly List<PlayerData> datas;
        public RegistryJoinBroadcast(List<PlayerData> datas) 
        {
            this.datas = datas;
        }
    }

    /// <summary>
    /// A broadcast sent from all networked registry users when a piece of PlayerData is updated.
    /// </summary>
    public readonly struct PlayerDataUpdateBroadcast : IBroadcast 
    {
        public readonly PlayerData data;
        public PlayerDataUpdateBroadcast(PlayerData data) 
        {
            this.data = data;
        }
    }
}