using UnityEngine;
using EMullen.Bootstrapper;
using System.Linq;
using System.Collections.Generic;
using EMullen.Core;

namespace EMullen.PlayerMgmt 
{
    public class PlayerMgmtBootstrapper : MonoBehaviour, IBootstrapComponent
    {

        [SerializeField]
        private bool requireInputToComplete;
        [SerializeField]
        private bool requireRegistry;

        public bool IsLoadingComplete()
        {
            // Loading isn't complete until the player manager exists
            if(PlayerManager.Instance == null)
                return false;

            List<LocalPlayer> localPlayers = PlayerManager.Instance.LocalPlayers.Where(lp => lp != null).ToList();
            if(requireRegistry) {
                int localPlayerWithUID = localPlayers.Where(lp => lp.UID != null).ToList().Count;
                // Highest constraint case, all local players must have a UID in the PlayerDataRegistry for bootstrap to be complete.
                return localPlayers.Count > 0 && localPlayerWithUID == localPlayers.Count;
            } else if(requireInputToComplete) {
                // Middle constraint case, a local player must exist for bootstrap to be complete
                return localPlayers.Count > 0;
            } else {
                return true;
            }
        }
    }
}