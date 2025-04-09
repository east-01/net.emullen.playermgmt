using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EMullen.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    /// <summary>
    /// The AuthServerLoginHandler is responsible for logging in local players to a web auth server.
    /// 
    /// Cannot handle multiple concurrent logins!
    /// </summary>
    public class AuthServerLoginHandler : UserInputLoginHandler
    {
        
        [SerializeField]
        private string authAddress;

        /// <summary>
        /// The player that is currently being logged in, cannot handle multi logins.
        /// </summary>
        private LocalPlayer player;

        private JObject refreshLoginResult;

        private List<LocalPlayer> awaitingResponse = new();

        public override string Status => "";
        /// <summary>
        /// Set this to true to save the users logins.
        /// </summary>
        public bool SaveLogin = false;

        private PlayerAuthenticator authenticator;

        private void Awake()
        {
            authenticator = new PlayerAuthenticator(authAddress);
        }

        private void Update() 
        {

        }

#region LoginHandler impl
        protected override Task LoginStart(LocalPlayer login, bool register) 
        {
            if(player != null)
                throw new InvalidOperationException($"Already logging in player {player.Input.playerIndex}. The AuthServerLoginHandler cannot handle multiple logins!");

            if(authenticator == null)
                throw new InvalidOperationException($"Cannot login player with AuthServerLoginHandler, the PlayerDataRegistry instance doesn't have an authenticator.");

            player = login;

            // Should we try to refresh login
            bool shouldRefresh = player.Input.playerIndex == 0 && PlayerPrefs.HasKey("refreshToken") && !string.IsNullOrEmpty(PlayerPrefs.GetString("refreshToken"));
            if(shouldRefresh) {
                string refreshToken = PlayerPrefs.GetString("refreshToken", null);
                RefreshLogin(refreshToken);
                // Clear refresh token since they're only allowed to be used once.
                PlayerPrefs.SetString("refreshToken", null);
                PlayerPrefs.Save();
            }

            return Task.CompletedTask;
        }

        public override async Task AcceptInput(LocalPlayer from, UserInputLoginHandler.LoginInput input)
        {
            string[] inputArgs = input.input;

            if(inputArgs.Length != 2)
                throw new ArgumentException("AuthServerLH#AcceptInput: len(input) != 2. The AuthServerLoginHandler expects a user and a password.");

            if(authenticator == null)
                throw new InvalidOperationException("Can't AcceptInput as AutherServerLoginHandler, PlayerDataRegistry#Authenticator is null!");

            string user = inputArgs[0];
            string pass = inputArgs[1];

            try {
                if(input.registering) {
                    await authenticator.Register(user, pass);
                }

                awaitingResponse.Add(from);
                JObject loginResult = await authenticator.LogIn(user, pass);
                awaitingResponse.Remove(from);

                await HandleLoginResult(from, loginResult);

            } catch(AuthenticationException exception) {
                Debug.LogError($"Failed to handle user: {exception.Message}");
            }
        }

        public override bool IsLoggingIn(LocalPlayer localPlayer) => player == localPlayer;
#endregion

#region Auth tasks
        public async Task HandleLoginResult(LocalPlayer player, JObject loginResult) 
        {
            if(loginResult == null || !loginResult.ContainsKey("uid") || !loginResult.ContainsKey("token") || !loginResult.ContainsKey("refreshToken"))
                throw new InvalidOperationException("Attempted to HandleLoginResult with " + (loginResult == null ? "null loginResult" : loginResult.ToString()));

            string uid = loginResult.GetValue("uid").Value<string>();
            string token = loginResult.GetValue("token").Value<string>();
            string refreshToken = loginResult.GetValue("refreshToken").Value<string>();

            PlayerPrefs.SetString("refreshToken", null);
            if(SaveLogin && player.Input.playerIndex == 0) {
                PlayerPrefs.SetString("refreshToken", refreshToken);
                BLog.Log("Saved refresh token locally.");
            }
            PlayerPrefs.Save();

            PlayerData pd = await LoginEnd(player, uid);
            
            if(token != null) {
                DatabaseTokenData dbTokenData = new(token);
                pd.SetData(dbTokenData, typeof(DatabaseTokenData), false);
            }
        }

        // Async method call to use the authentication library
        private async void RefreshLogin(string refreshToken)
        {
            try {
                // Make refresh login attempt, set refreshStatus to true if successful
                refreshLoginResult = await authenticator.RefreshLogIn(refreshToken);
                await HandleLoginResult(this.player, refreshLoginResult);
            } catch(AuthenticationException authException) {
                Debug.LogError("Exception during RefreshLogIn: " + authException.Message);
            } catch (Exception ex) {
                Debug.LogError("Exception during RefreshLogIn: " + ex.Message);
            }
        }
    }
#endregion
}