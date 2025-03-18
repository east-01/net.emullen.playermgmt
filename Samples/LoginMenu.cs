using System;
using EMullen.Core;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        private string refreshToken;
        private bool shouldRefresh;
        private bool waitingOnRefreshStatus;
        private JObject refreshLoginResult;
        private bool prevWaitingOnRefreshStatus;
        private bool refreshStatus;

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

        protected override void Opened() 
        {
            base.Opened();
            statusClearTime = Time.time;
        }

        private void Update() 
        {
            // Clear the status text, or show that we're refreshing the login
            if(waitingOnRefreshStatus) {
                SetStatusText("Logging in...");
                
                userInput.text = "";
                passInput.text = "";
            } else if(Time.time > statusClearTime)
                statusText.text = "";

            // Start of refresh cycle, shouldRefresh flag is set to true so we catch that here
            if(shouldRefresh) {
                // Reset refresh flag, this should only be set in OnLocalPlayerRequiresLoginEvent callback
                shouldRefresh = false;

                // Async method call to use the authentication library
                async void RefreshLogin(LocalPlayer lp)
                {
                    waitingOnRefreshStatus = true; // Show that we're waiting for the refresh
                    refreshStatus = false; // Initialize the refresh status to false

                    try {
                        // Make refresh login attempt, set refreshStatus to true if successful
                        refreshLoginResult = await PlayerDataRegistry.Instance.Authenticator.RefreshLogIn(refreshToken);
                        refreshStatus = refreshLoginResult != null;
                    } catch(AuthenticationException authException) {
                        Debug.LogError("Exception during RefreshLogIn: " + authException.Message);
                    } catch (Exception ex) {
                        Debug.LogError("Exception during RefreshLogIn: " + ex.Message);
                    }

                    waitingOnRefreshStatus = false; // Show that refresh is done
                }

                // Call aync method on main thread (unity complains if it's called in the event callback)
                RefreshLogin(FocusedPlayer);
            }

            // End of the refresh cycle, this was given back to us by the async RefreshLogin call
            //   from above, we can handle the login result on the main thread now that its here.
            if(refreshLoginResult != null) {
                HandleLoginResult(FocusedPlayer, refreshLoginResult);
                refreshLoginResult = null; // Reset login result
            }
        }

        private void OnLocalPlayerRequiresLoginEvent(LocalPlayer lp)
        {
            if(PlayerDataRegistry.Instance.Authenticator == null) {
                Debug.LogError("Can't handle LogIn event, authenticator is null");
                return;
            }

            Open(lp);

            // Should we try to refresh login
            shouldRefresh = lp.Input.playerIndex == 0 && PlayerPrefs.HasKey("refreshToken") && !string.IsNullOrEmpty(PlayerPrefs.GetString("refreshToken"));
            if(shouldRefresh) {
                refreshToken = PlayerPrefs.GetString("refreshToken", null);
                // Clear refresh token since they're only allowed to be used once.
                PlayerPrefs.SetString("refreshToken", null);
                PlayerPrefs.Save();
            }
        }

        public void HandleLoginResult(LocalPlayer player, JObject loginResult) 
        {
            if(loginResult == null || !loginResult.ContainsKey("uid") || !loginResult.ContainsKey("token") || !loginResult.ContainsKey("refreshToken"))
                throw new InvalidOperationException("Attempted to HandleLoginResult with " + (loginResult == null ? "null loginResult" : loginResult.ToString()));

            string uid = loginResult.GetValue("uid").Value<string>();
            string token = loginResult.GetValue("token").Value<string>();
            string refreshToken = loginResult.GetValue("refreshToken").Value<string>();

            BLog.Highlight("Save login toggle: " + saveLoginToggle.isOn);
            if(saveLoginToggle.isOn) {
                if(player.Input.playerIndex == 0) {
                    PlayerPrefs.SetString("refreshToken", refreshToken);
                    BLog.Log("Saved refresh token locally.");
                } else {
                    PlayerPrefs.SetString("refreshToken", null);
                }
            }
            PlayerPrefs.Save();

            FocusedPlayer.AddToPlayerDataRegistry(uid, token);
            Close();
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
        public async void Submit() 
        {
            if(FocusedPlayer == null) {
                Debug.LogError("Can't submit log in screen, focused player is null.");
                return;
            }

            if(waitingOnRefreshStatus)
                return;

            PlayerAuthenticator auth = PlayerDataRegistry.Instance.Authenticator;
            if(auth == null) {
                Debug.LogError("Can't submit log in screen, authenticator is null");
                return;
            }

            bool clearUser = false;
            bool clearPass = false;

            try {
                if(loginMode) {
                    JObject loginResult = await auth.LogIn(userInput.text, passInput.text);
                    HandleLoginResult(FocusedPlayer, loginResult);
                    clearUser = true;
                    clearPass = true;
                } else {
                    await auth.Register(userInput.text, passInput.text);
                    ShowStatusText("Successfully registered. Please log in.");                    
                    ToggleMode();
                }
            } catch(AuthenticationException exception) {
                Debug.LogError($"Failed to {(loginMode ? "log in" : "register")} user: {exception.Message}");
                ShowStatusText(exception.Message, true);
                clearUser = !loginMode;
                clearPass = true;
            }

            if(clearUser)
                userInput.text = "";
            
            if(clearPass)
                passInput.text = "";

        }        

        /// <summary>
        /// Callback from button to toggle between register/login mode
        /// </summary>
        public void ToggleMode() => SetLoginMode(!loginMode);
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