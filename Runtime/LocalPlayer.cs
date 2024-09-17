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
    }
}