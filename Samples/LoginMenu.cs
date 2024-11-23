using System;
using EMullen.Core;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor.PackageManager;
using UnityEngine;

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
        private TMP_Text registerText; // The text that prompts the switching between registering/logging in
        [SerializeField]
        private TMP_Text registerButtonText; // The text on the button related to above

        private bool loginMode = true;
        private float statusClearTime;

        private new void Awake() 
        {
            base.Awake();
            PlayerManager.Instance.LocalPlayerRequiresLoginEvent += OnLocalPlayerRequiresLoginEvent;

            passInput.onSubmit.AddListener(OnPassInputSubmit);
        }

        private new void OnDestroy() 
        {
            base.OnDestroy();
            PlayerManager.Instance.LocalPlayerRequiresLoginEvent -= OnLocalPlayerRequiresLoginEvent;
        }

        private void OnLocalPlayerRequiresLoginEvent(LocalPlayer lp)
        {
            Open(lp);
        }

        protected override void Opened() 
        {
            base.Opened();
            statusClearTime = Time.time;
        }

        private void Update() 
        {
            if(Time.time > statusClearTime)
                statusText.text = "";
        }

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
        public async void Submit() 
        {
            if(FocusedPlayer == null) {
                Debug.LogError("Can't submit log in screen, focused player is null.");
                return;
            }

            PlayerAuthenticator auth = PlayerDataRegistry.Instance.Authenticator;
            if(auth == null) {
                Debug.LogError("Can't submit log in screen, authenticator is null");
                return;
            }

            try {
                if(loginMode) {
                    JObject loginResult = await auth.LogIn(userInput.text, passInput.text);
                    string uid = loginResult.GetValue("uid").Value<string>();
                    string token = loginResult.GetValue("token").Value<string>();
                    FocusedPlayer.AddToPlayerDataRegistry(uid, token);
                    Close();
                } else {
                    await auth.Register(userInput.text, passInput.text);
                    ShowStatusText("Successfully registered.");                    
                    ToggleMode();
                }
            } catch(AuthenticationException exception) {
                Debug.LogError($"Failed to {(loginMode ? "log in" : "register")} user: {exception.message}");
                ShowStatusText(exception.message, true);
            }
        }        

        /// <summary>
        /// Callback from button to toggle between register/login mode
        /// </summary>
        public void ToggleMode() 
        {
            loginMode = !loginMode;   
        
            titleText.text = loginMode ? "Log In" : "Register";
            registerText.text = loginMode ? "Don't have an account?" : "Already have an account?";
            registerButtonText.text = loginMode ? "Register" : "Log In";
        }

        public void ShowStatusText(string message, bool isError = false) 
        {
            statusText.color = isError ? Color.red : Color.green;
            statusText.text = message;

            statusClearTime = Time.time + statusShowTime;
        }

    }
}