using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using EMullen.Core.PlayerMgmt;
using EMullen.PlayerMgmt;
using EMullen.Core;
using System.Collections;
using System;
using EMullen.PlayerMgmt.Samples;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EMullen.PlayerMgmt.Tests 
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
            if(PlayerDataRegistry.Instance.GetAllData().Length == 0)
                return null;
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
                case "db":
                    InputCommand_DB(argsNoCMD);
                    break;
                case "register":
                    InputCommand_Register(argsNoCMD);
                    break;
                case "login":
                    InputCommand_LogIn(argsNoCMD);
                    break;
                case "setscore":                
                    InputCommand_SetScore(argsNoCMD);
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

        public async void InputCommand_DB(string[] args) 
        {
            string sqlServerAddr = "http://emullen.net:8921";
            string database = "testdb";

            SQLPlayerDatabase db = new(typeof(SimplePlayerData), sqlServerAddr, database);
            if(args[0] == "out") {
                SimplePlayerData spd = (SimplePlayerData) await db.Get("abcd");
                
                if(spd == null) {
                    Debug.LogError("Failed to retrieve simple player data.");
                    return;
                }

                Debug.Log("Score: " + spd.Score);
            } else if(args[0] == "in") {
                SimplePlayerData spd = new("abcd", 100.123f);
                bool status = await db.Set(spd);
                Debug.Log("Status: " + status);
            }
        }

        public async void InputCommand_Register(string[] args) 
        {
            if(args.Length != 2) {
                Debug.LogError("Incorrect amount of arguments");
                return;
            }

            string authServerAddr = "https://emullen.net/auth";
            PlayerAuthenticator auth = new(authServerAddr);
            
            try {
                await auth.Register(args[0], args[1]);
            } catch(AuthenticationException exception) {
                Debug.LogError("Failed to register: " + exception.Message);
                return;
            }

            Debug.Log("Successfully registered user.");
        }

        public async void InputCommand_LogIn(string[] args) 
        {
            if(args.Length != 2) {
                Debug.LogError("Incorrect amount of arguments");
                return;
            }

            string authServerAddr = "https://emullen.net/auth";
            PlayerAuthenticator auth = new(authServerAddr);
            JObject obj;

            try {
                obj = await auth.LogIn(args[0], args[1]);
            } catch(AuthenticationException exception) {
                Debug.LogError("Failed to log in: " + exception.Message);
                return;
            } catch(Exception exception) {
                Debug.LogError(exception.Message);
                return;
            }

            Debug.Log("Logged in: " + obj.ToString());
        }

        public void InputCommand_SetScore(string[] args) 
        {
            if(args.Length != 1) {
                Debug.LogError("Incorrect amount of arguments");
                return;
            }

            SimplePlayerData spd;
            if(SelectedPlayer.HasData<SimplePlayerData>())
                spd = SelectedPlayer.GetData<SimplePlayerData>();
            else
                spd = new(SelectedPlayer.GetUID(), 0);

            spd.Score = float.Parse(args[0]);
            SelectedPlayer.SetData(spd);

        }
    }
#endregion

}

public class SimplePlayerData : PlayerDatabaseDataClass 
{
    private string uid;
    public override string UID => uid;
    public float Score;

    public SimplePlayerData(string uid, float score) {
        this.uid = uid;
        this.Score = score;
    }

    public override string ToString() => $"Score: {Score}";
}