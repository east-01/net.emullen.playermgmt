using UnityEngine;
using EMullen.Bootstrapper;
using System.Linq;

namespace EMullen.PlayerMgmt 
{
    public class PlayerMgmtBootstrapper : MonoBehaviour, IBootstrapComponent
    {

        [SerializeField]
        private bool requireInputToComplete;

        public bool IsLoadingComplete()
        {
            if(PlayerManager.Instance != null && requireInputToComplete)
                return PlayerManager.Instance.PlayerInputManager.playerCount > 0;
            else
                return PlayerManager.Instance != null;
        }
    }
}