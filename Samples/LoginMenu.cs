using System;
using EMullen.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static EMullen.PlayerMgmt.LoginHandler;
#if UNITY_2022_3_OR_NEWER
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
#else
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

namespace EMullen.PlayerMgmt.Samples 
{
    [DefaultExecutionOrder(1)]
    public class LoginMenu : MenuController.MenuController 
    {
        
        [SerializeField]
        private TMP_Text titleText;
        [SerializeField]
        private float statusShowTime = 5;
        [SerializeField]
        private TMP_Text statusText;
        [SerializeField]
        private TMP_InputField userInput;
        [SerializeField]
        private TMP_InputField passInput;
        [SerializeField]
        private Toggle saveLoginToggle;
        [SerializeField]
        private TMP_Text registerText; // The text that prompts the switching between registering/logging in
        [SerializeField]
        private TMP_Text registerButtonText; // The text on the button related to above

        private bool loginMode = true;
        private float statusClearTime;

        private new void Awake() 
        {
            base.Awake();
            // PlayerManager.Instance.LocalPlayerJoinedEvent += LocalPlayerJoinedEvent;
            PlayerDataRegistry.Instance.LoginHandlerChangeEvent += LoginHandlerChangedEvent;

            // Initial subscribe to the LoginHandler's events
            if(PlayerDataRegistry.Instance.LoginHandler != null)
                LoginHandlerChangedEvent(null, PlayerDataRegistry.Instance.LoginHandler);

            passInput.onSubmit.AddListener(OnPassInputSubmit);
        }

        private new void OnDestroy() 
        {
            base.OnDestroy();
            // PlayerManager.Instance.LocalPlayerJoinedEvent -= LocalPlayerJoinedEvent;
        }

        protected override void Opened() 
        {
            base.Opened();
            statusClearTime = Time.time;

            if(!Input.GetKey(KeyCode.Escape)) {
                LoadInfoFromPlayerPrefs();
            }
        }

        private void Update() 
        {
            if(PlayerDataRegistry.Instance.LoginHandler is not UserInputLoginHandler)
                return;

            UserInputLoginHandler loginHandler = PlayerDataRegistry.Instance.LoginHandler as UserInputLoginHandler;

            // statusText.text = loginHandler.Status;

            // // Clear the status text, or show that we're refreshing the login
            // if(loginHandler) {
            //     SetStatusText("Logging in...");
                
            //     userInput.text = "";
            //     passInput.text = "";
            // } else if(Time.time > statusClearTime)
            //     statusText.text = "";

            
        }

        private void LoginStartedEvent(LocalPlayer lp, bool registering) 
        {
            LoginHandler loginHandler = PlayerDataRegistry.Instance.LoginHandler;
            if(loginHandler == null || loginHandler is not UserInputLoginHandler)
                return;

            bool isAuth = loginHandler is AuthServerLoginHandler;
            passInput.gameObject.SetActive(isAuth);

            Open(lp);
        }

        private void LoginEndedEvent(LocalPlayer lp, bool registered, PlayerData resultData) 
        {
            Close();
        }

        private void LoginHandlerChangedEvent(LoginHandler oldHandler, LoginHandler newHandler) 
        {
            if(oldHandler != null)
                LoginHandlerUnsubscribe(oldHandler);

            LoginHandlerSubscribe(newHandler);            
        }

        private void LoginHandlerSubscribe(LoginHandler toSubTo) 
        {
            toSubTo.LoginStartedEvent += LoginStartedEvent;
            toSubTo.LoginEndedEvent += LoginEndedEvent;
        }

        private void LoginHandlerUnsubscribe(LoginHandler toUnsubFrom) 
        {
            toUnsubFrom.LoginStartedEvent -= LoginStartedEvent;
            toUnsubFrom.LoginEndedEvent -= LoginEndedEvent;
        }

        private async void HandleLoginInput(UserInputLoginHandler.LoginInput input) 
        {
            LoginHandler loginHandler = PlayerDataRegistry.Instance.LoginHandler;
            if(loginHandler == null || loginHandler is not UserInputLoginHandler)
                return;

            UserInputLoginHandler userInputLoginHandler = loginHandler as UserInputLoginHandler;

            await userInputLoginHandler.AcceptInput(FocusedPlayer, input);
        }

#region UI Element Callbacks
        /// <summary>
        /// Callback from passInput input field when the user presses enter.
        /// </summary>
        private void OnPassInputSubmit(string submit) 
        {
            if(passInput.wasCanceled)
                return;

            Submit();
        }

        /// <summary>
        /// Callback from submit button/password onSubmit to submit the username and password
        /// </summary>
        public void Submit() 
        {
            if(FocusedPlayer == null) {
                Debug.LogError("Can't submit log in screen, focused player is null.");
                return;
            }

            string user = userInput.text;
            string pass = passInput.text;

            if(saveLoginToggle.isOn) {
                SaveInfoToPlayerPrefs();
            }

            UserInputLoginHandler.LoginInput input = new(new string[] {user, pass}, !loginMode);
            HandleLoginInput(input);
        }        

        /// <summary>
        /// Callback from button to toggle between register/login mode
        /// </summary>
        public void ToggleMode() => SetLoginMode(!loginMode);
#endregion

#region Player prefs
        public void SaveInfoToPlayerPrefs() 
        {
            PlayerPrefs.SetString("UserInput.Text", userInput.text);
            PlayerPrefs.SetString("PassInput.Text", passInput.text);
            PlayerPrefs.Save();
            BLog.Highlight($"Saved {userInput.text} and {passInput.text}");
        }

        public bool LoadInfoFromPlayerPrefs() 
        {
            string user = PlayerPrefs.GetString("UserInput.Text", "");
            BLog.Highlight($"Loaded {user}");
            if(user == "")
                return false;
            string pass = PlayerPrefs.GetString("PassInput.Text", "");
            BLog.Highlight($"Loaded {pass}");
            
            UserInputLoginHandler.LoginInput input = new(new string[] {user, pass}, false);
            HandleLoginInput(input);
            return true;
        }
#endregion

#region UI Controls
        private void SetLoginMode(bool loginMode) 
        {
            this.loginMode = loginMode;

            titleText.text = loginMode ? "Log In" : "Register";
            registerText.text = loginMode ? "Don't have an account?" : "Already have an account?";
            registerButtonText.text = loginMode ? "Register" : "Log In";
        }

        public void SetStatusText(string message, bool isError = false) 
        {
            statusText.color = isError ? Color.red : Color.green;
            statusText.text = message;
        }

        public void ShowStatusText(string message, bool isError = false) 
        {
            SetStatusText(message, isError);
            statusClearTime = Time.time + statusShowTime;
        }
#endregion

    }
}