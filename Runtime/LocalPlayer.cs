using UnityEngine;
using UnityEngine.InputSystem;

namespace EMullen.PlayerMgmt 
{
    public class LocalPlayer 
    {
        public PlayerInput Input { get; private set; }
        public IdentifierData IdentifierData { get; private set; }
        public string UID { get; private set; }

        public LocalPlayer(PlayerInput input, IdentifierData identifierData) 
        {
            Input = input;
            IdentifierData = identifierData;
            UID = identifierData.uid;
        }

        public PlayerData GetPlayerData() 
        {
            if(!HasPlayerData()) {
                Debug.LogError("Can't get player data, LocalPlayer doesn't have any");
                return null;
            }
            return PlayerDataRegistry.Instance.GetPlayerData(UID);
        }

        public bool HasPlayerData() 
        {
            return PlayerDataRegistry.Instance != null && PlayerDataRegistry.Instance.Contains(UID);
        }
    }
}