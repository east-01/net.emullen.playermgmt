using System.Linq;
using FishNet;
using FishNet.Broadcast;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    /// Networked Player Data Registry
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

        private void NetworkedAwake() 
        {

        }

        private void NetworkedUpdate() 
        {
            
        }

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

    public struct PDRSyncBroadcast : IBroadcast 
    {
        
    }
}