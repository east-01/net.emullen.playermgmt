using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace EMullen.PlayerMgmt.Samples 
{
    public class PlayerMgmtTestController : MonoBehaviour
    {

        [SerializeField]
        private TMP_Text selectedPlayerText;
        [SerializeField]
        private TMP_Text dataReadoutText;
        [SerializeField]
        private TMP_Text joiningEnabledText;

        private int selectedPlayerIndex;
        public PlayerData SelectedPlayer { get { 
            CheckSelectionBounds();
            return PlayerDataRegistry.Instance.GetAllData()[selectedPlayerIndex];
        } }

        private void Start() 
        {
            InvokeRepeating(nameof(UpdateValues), 0f, 1f);
        }

        public void UpdateValues() 
        {
            if(PlayerManager.Instance.PlayerCount == 0)
                return;

            string selectedPlayerTextStr = "No player selected";
            string dataReadoutTextStr = "No data";

            if(SelectedPlayer != null) {
                selectedPlayerTextStr = SelectedPlayer.GetData<IdentifierData>().uid;
                dataReadoutTextStr = MakeDataReadout(SelectedPlayer);
            }

            selectedPlayerText.text = selectedPlayerTextStr;
            dataReadoutText.text = dataReadoutTextStr;
            joiningEnabledText.text = "Joining enabled: " + PlayerManager.Instance.PlayerInputManager.joiningEnabled;
        }

        public string MakeDataReadout(PlayerData data) 
        {
            List<string> dataStrings = new();
            data.Datas.ForEach(data => dataStrings.Add("Type: " + data.GetType().Name + "\n  " + data.ToString()));
            return "Types: " + string.Join(", ", data.TypeNames) + "\n\n" + string.Join("\n", dataStrings);
        }

#region UI Button callbacks
        public void ChangeSelection(int direction) 
        {
            selectedPlayerIndex += direction;
            CheckSelectionBounds();
            UpdateValues();
        }

        /// <summary>
        /// Make sure the selected player index is in the bounds of the PlayerData registry
        /// </summary>
        private void CheckSelectionBounds() 
        {
            int len = PlayerDataRegistry.Instance.GetAllData().Length;
            if(selectedPlayerIndex >= len)
                selectedPlayerIndex = 0;
            if(selectedPlayerIndex < 0)
                selectedPlayerIndex = len-1;
        }

        public void ToggleJoinEnabled() 
        {
            PlayerInputManager pim = PlayerManager.Instance.PlayerInputManager;
            if(pim.joiningEnabled)
                pim.DisableJoining();
            else
                pim.EnableJoining();

            UpdateValues();
        }

        public void InputCommand(string str) 
        {
            if(str.Length == 0)
                return;

            string[] args = str.Split(" ");
            string command = args[0].ToLower();
            string[] argsNoCMD = new string[args.Length-1];
            for(int i = 1; i < args.Length; i++) {
                argsNoCMD[i-1] = args[i];
            }
            switch(command) {
                case "setname":
                    InputCommand_SetName(argsNoCMD);
                    break;
                default:
                    Debug.LogError($"Didn't recognize command \"{command}\"");
                    break;
            }

            UpdateValues();
        }

        public void InputCommand_SetName(string[] args) 
        {
            if(SelectedPlayer == null) {
                Debug.LogError("Can't set name because selected player is null");
                return;
            }
            if(args.Length == 0) {
                Debug.LogError("Must provide a name");
                return;
            }

            NameData nd = SelectedPlayer.HasData<NameData>() ? SelectedPlayer.GetData<NameData>() : new();
            nd.Name = string.Join(" ", args);
            SelectedPlayer.SetData(nd);
            Debug.Log($"Set name to \"{nd.Name}\"");
        }
    }
#endregion

}
