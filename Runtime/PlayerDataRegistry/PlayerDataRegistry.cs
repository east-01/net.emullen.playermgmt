using System.Collections.Generic;
using EMullen.Core;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    [RequireComponent(typeof(PlayerManager))]
    /// <summary>
    /// The PlayerDataRegistry stores all PlayerData for the local instance. If using
    ///   FishNetworking, the data will sync with the server.<br\>
    ///   
    /// NetworkedPlayerDataRegistry:<br/>
    /// The networking backend for the PlayerDataRegistry. Uses broadcasts to synchronize with the
    ///   server's PlayerDataRegistry.<br/>
    /// Has multiple phases for joining/leaving so that data doesn't get lost during
    /// synchronization.
    /// </summary>
    public partial class PlayerDataRegistry : MonoBehaviour 
    {

        /// <summary>
        /// Singleton instance for the PlayerDataRegistry
        /// </summary>
        public static PlayerDataRegistry Instance { get; private set; }

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

        private void Awake() 
        {
            // Set up singleton instance.
            if(Instance != null) {
                Destroy(gameObject);
                Debug.Log($"Destroyed newly spawned PlayerDataRegistry since singleton Instance already exists.");
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            NetworkedAwake();

            PlayerDatas = new();
        }

        private void Update() 
        {
            NetworkedUpdate();
        }

    }
}