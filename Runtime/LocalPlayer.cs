using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EMullen.PlayerMgmt 
{
    public class LocalPlayer 
    {
        public PlayerInput Input { get; private set; }
        public string UID { get; private set; }

        public LocalPlayer(PlayerInput input) 
        {
            Input = input;
        }

        /// <summary>
        /// Add the LocalPlayer to the registry. Called by the LoginHandler of the PlayerDataRegistry.
        /// </summary>
        /// <param name="uid">Optional parameter used when authentication is required, the UID is 
        ///   retrieved from the login callback.</param>
        /// <param name="authToken">Optional parameter used when authentication is required, the 
        ///   authToken is retrieved from the login callback.</param>
        /// <returns>Resulting PlayerData that was added to the registry.</returns>
        public PlayerData AddToPlayerDataRegistry(string uid) 
        {
            PlayerDataRegistry registry = PlayerDataRegistry.Instance;

            if(registry.Contains(uid))
                throw new InvalidOperationException("Failed to add new player: Player is already registered");

            // Create IdentifierData and PlayerData objects for this LocalPlayer
            IdentifierData identifierData = new(uid, Input.playerIndex);
            PlayerData data = new(identifierData);

            registry.Add(data);

            UID = uid;
            return data;
        }

        public PlayerData GetPlayerData() 
        {
            if(UID == null) {
                Debug.LogError("Can't get player data, LocalPlayer doesn't have a UID.");
                return null;
            }
            if(!HasPlayerData()) {
                Debug.LogError("Can't get player data, LocalPlayer doesn't have any");
                return null;
            }
            return PlayerDataRegistry.Instance.GetPlayerData(UID);
        }

        public bool HasPlayerData() 
        {
            return UID != null && PlayerDataRegistry.Instance != null && PlayerDataRegistry.Instance.Contains(UID);
        }
    }
}