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
        /// Add the LocalPlayer to the registry.
        /// Optional parameters are there for when authentication is required, prompt the player to
        ///   log in using PlayerDataRegistry#PlayerAuthenticator and pass the returned credentials
        ///   here.
        /// </summary>
        /// <param name="uid">Optional parameter used when authentication is required, the UID is 
        ///   retrieved from the login callback.</param>
        /// <param name="authToken">Optional parameter used when authentication is required, the 
        ///   authToken is retrieved from the login callback.</param>
        public void AddToPlayerDataRegistry(string uid = null, string authToken = null) 
        {
            PlayerDataRegistry registry = PlayerDataRegistry.Instance;

            // Ensure that we have proper credentials if they're required
            if(registry.AuthenticationRequired && (uid == null || authToken == null))
                throw new InvalidOperationException("Failed to add the LocalPlayer to the PlayerDataRegistry, authentication is required but no credentials were provided.");

            // Populate UID field if necessary
            uid ??= IdentifierData.GenerateUID();

            // Create IdentifierData and PlayerData objects for this LocalPlayer
            IdentifierData identifierData = new(uid, Input.playerIndex);
            PlayerData data = new(identifierData);

            if(registry.Contains(uid))
                throw new InvalidOperationException("Failed to add new player: Player is already registered");

            registry.Add(data);
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