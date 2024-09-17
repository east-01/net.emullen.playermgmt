using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Server;

namespace EMullen.PlayerMgmt 
{
    /// <summary>
    /// This identifier data relates the PlayerData to a network connection that it belongs to,
    ///   it also provides the list of all IdentifierData uids belonging to that connection.
    ///   
    /// This data also acts as a lock to keep us in the networked registry. If the PlayerData has
    ///   NetworkIdentifierData that means the data is networked.
    /// </summary>
    public class NetworkIdentifierData : PlayerDataClass 
    {
        /// <summary>
        /// The unique id for the clients connection, retrieved from the joining clients
        ///   LocalConnection#clientID
        /// </summary>
        public int clientID;
        public List<string> connectionsUIDs;

        public NetworkIdentifierData() {}

        public override string ToString()
        {
            string isLocal = "";
            if(PlayerManager.Instance != null && PlayerManager.Instance.LocalPlayers != null) {
                List<string> localUIDs = PlayerManager.Instance.LocalPlayers.Where(lp => lp != null).Select(lp => lp.UID).ToList();
                if(localUIDs.ToHashSet().SetEquals(connectionsUIDs.ToHashSet()))
                    isLocal = "(LOCAL) ";
            }

            return $"{isLocal}id: {clientID}\n  Connections: {string.Join(", ", connectionsUIDs)}";
        }
    }
}