using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms;
using EMullen.Core;
using FishNet;
using System.Linq;

namespace EMullen.PlayerMgmt {
    /// <summary>
    /// The player object manager is mainly responsible for LOCAL player input management.
    /// We don't want to network this because, if we did, we'd be doing splitscreen for all
    ///   players on server when splitscreen should only be for players on the same machine.
    /// </summary>
    [RequireComponent(typeof(PlayerInputManager))]
    [RequireComponent(typeof(PlayerDataRegistry))]
    public class PlayerManager : MonoBehaviour
    {

        public static PlayerManager Instance { get; private set; }

#region Editor fields
        [SerializeField]
        private GameObject inputPromptCanvas;
        [SerializeField]
        private GameObject inputPromptPanel;
        [SerializeField]
        private GameObject deviceMissingPanel;
        [SerializeField]
        private GameObject loginPanel;

        [SerializeField]
        private List<string> sceneBlacklistControlSchemeSwitch;
        [SerializeField]
        private List<string> sceneBlacklistInputPrompt;
#endregion

#region Runtime fields
        public PlayerInputManager PlayerInputManager { get; private set; }
        private List<PlayerInput> playersMissingDevices = new();

        public LocalPlayer[] LocalPlayers { get; private set; }

        public int PlayerCount => PlayerInputManager.playerCount;
        public bool CanPlayerOneAutoSwitch => PlayerCount == 1 && !sceneBlacklistControlSchemeSwitch.Contains(SceneManager.GetActiveScene().name);
#endregion

#region Events
        public delegate void LocalPlayerJoinedHandler(LocalPlayer lp);
        public event LocalPlayerJoinedHandler LocalPlayerJoinedEvent;

        public delegate void LocalPlayerLeftHandler(LocalPlayer lp);
        public event LocalPlayerLeftHandler LocalPlayerLeftEvent;

        public delegate void LocalPlayerRequiresLogin(LocalPlayer lp);
        public event LocalPlayerRequiresLogin LocalPlayerRequiresLoginEvent;
#endregion

        private void Awake()
        {
            if(Instance != null) {
                Destroy(gameObject);
                Debug.Log($"Destroyed newly spawned PlayerManager since singleton Instance already exists.");
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            PlayerInputManager = GetComponent<PlayerInputManager>();
            string warningPrefix = $"PlayerInputManager on GameObject \"{PlayerInputManager.gameObject.name}\" warning: ";
            if(PlayerInputManager.maxPlayerCount == -1)
                Debug.LogWarning($"{warningPrefix}maxPlayerCount isn't defined. The PlayerManager will register the first 100 players.");
            if(PlayerInputManager.playerPrefab.GetComponent<PlayerInput>() == null)
                Debug.LogWarning($"{warningPrefix}playerPrefab doesn't have a PlayerInput component");

            LocalPlayers = new LocalPlayer[PlayerInputManager.maxPlayerCount == -1 ? 100 : PlayerInputManager.maxPlayerCount];
        }

        private void OnEnable()
        {
            PlayerInputManager.onPlayerJoined += AddPlayer;
            PlayerInputManager.onPlayerLeft += RemovePlayer;
        }

        private void OnDisable()
        {
            PlayerInputManager.onPlayerJoined -= AddPlayer;
            PlayerInputManager.onPlayerLeft -= RemovePlayer;		
        }

        private void Update() 
        {
            // We always need an input for player 1
            if(PlayerCount == 0 && !InputPromptActive) {
                PromptForInput();
            } else if(PlayerCount > 0 && InputPromptActive) {
                ClearInputPrompt();
            }

            if(LocalPlayers.Length > 0 && LocalPlayers[0] != null) {
                // BLog.Highlight($"P1 can auto switch: {CanPlayerOneAutoSwitch}");
                LocalPlayers[0].Input.neverAutoSwitchControlSchemes = !CanPlayerOneAutoSwitch;
            }
        }

#region Add/Remove player
        public void AddPlayer(PlayerInput input) 
        {
            input.gameObject.transform.SetParent(transform);

            input.onDeviceLost += PlayerInput_DeviceLost;
            input.onDeviceRegained += PlayerInput_DeviceRegained;

            LocalPlayer newlp = new(input);
            LocalPlayers[input.playerIndex] = newlp;       

            if(!PlayerDataRegistry.Instance.AuthenticationRequired)
                newlp.AddToPlayerDataRegistry();
            else
                LocalPlayerRequiresLoginEvent?.Invoke(newlp);

            LocalPlayerJoinedEvent?.Invoke(newlp);     
        }

        /// <summary>
        /// Remove a player from the PlayerObjectManager
        /// </summary>
        /// <param name="obj"></param>
        public void RemovePlayer(PlayerInput input) 
        {
            LocalPlayer lp = LocalPlayers[input.playerIndex];
            if(lp == null) {
                Debug.LogError($"Can't remove LocalPlayer for player index {input.playerIndex}, no LocalPlayer exists at that index.");
                return;
            }

            input.onDeviceLost -= PlayerInput_DeviceLost;
            input.onDeviceRegained -= PlayerInput_DeviceRegained;

            if(lp.HasPlayerData())
                PlayerDataRegistry.Instance.Remove(PlayerDataRegistry.Instance.GetPlayerData(lp.UID));

            LocalPlayerLeftEvent?.Invoke(LocalPlayers[input.playerIndex]);

            LocalPlayers[input.playerIndex] = null;
            Destroy(input.gameObject);

        }
        /// <summary>
        /// Event call for removing player, shortcuts to the original RemovePlayer call 
        ///   using FindPlayerObject(PlayerInput).
        /// </summary>
        /// <param name="input">The PlayerInput to remove, more specifically, the PlayerObject 
        ///   that owns that PlayerInput</param>
        public void RemovePlayer(LocalPlayer lp) => RemovePlayer(lp.Input);
#endregion  

#region PlayerDataRegistry interactions
        
#endregion

#region Input prompts
        /// <summary>
        /// Prompt the user for input so that we have a player one.
        /// We don't want to show the input prompt canvas because this is the only
        ///   place where there isn't a player one right when the scene opens.
        /// </summary>
        public void PromptForInput() 
        { 
            if(!sceneBlacklistInputPrompt.Contains(SceneManager.GetActiveScene().name))
                inputPromptPanel.SetActive(true); 
            PlayerInputManager.EnableJoining();
        }

        public void ClearInputPrompt() 
        { 
            inputPromptPanel.SetActive(false); 
            PlayerInputManager.DisableJoining();
        }

        public bool InputPromptActive { get { 
            // Weird logic here because we don't want to show the input prompt on the title screen. Explained in PromptForInput
            if(sceneBlacklistInputPrompt.Contains(SceneManager.GetActiveScene().name))
                return PlayerInputManager.joiningEnabled;
            else
                return inputPromptPanel.activeSelf; 
        } }

        private void PlayerInput_DeviceLost(PlayerInput input)
        {
            BLog.Highlight("Device lost for " + input.playerIndex);
            if(!playersMissingDevices.Contains(input))
                playersMissingDevices.Add(input);

            UpdateMissingDevicesPrompt();
        }
        
        private void PlayerInput_DeviceRegained(PlayerInput input)
        {
            if(playersMissingDevices.Contains(input))
                playersMissingDevices.Remove(input);

            UpdateMissingDevicesPrompt();
        }

        public void UpdateMissingDevicesPrompt() 
        {
            string nameList = "";
            if(playersMissingDevices.Count > 0) {
                playersMissingDevices.ForEach(playerInput => nameList += $"{PlayerDataRegistry.Instance.GetPlayerData(LocalPlayers[playerInput.playerIndex].UID)}, ");
                nameList = nameList[..^2];
            }

            deviceMissingPanel.GetComponent<TMP_Text>().text = $"Missing input: {nameList}";
            deviceMissingPanel.SetActive(playersMissingDevices.Count > 0);
        }
#endregion

    }
}