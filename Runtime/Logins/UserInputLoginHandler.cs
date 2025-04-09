using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    /// <summary>
    /// The UserInputLoginHandler will wait for a users input. Which will be passed as a string
    ///   array. LocalPlayers that are being waited on for input will be counted as logging in.
    /// </summary>
    public abstract class UserInputLoginHandler : LoginHandler 
    {
        protected List<LocalPlayer> userInput = new();
        /// <summary>
        /// Accept a list of input strings from a LocalPlayer.
        /// </summary>
        /// <param name="from">The LocalPlayer that submitted the input strings.</param>
        /// <param name="input">The input strings, can be username, password, token...</param>
        public abstract Task AcceptInput(LocalPlayer from, LoginInput input);
        public override bool IsLoggingIn(LocalPlayer localPlayer) => userInput.Contains(localPlayer);

        /// <summary>
        /// The login input for a UserInputLoginHandler, created by the LocalPlayer during the login
        ///   process and then passed back to the login handler 
        /// </summary>
        public struct LoginInput {
            public string[] input;
            public bool registering;

            public LoginInput(string[] input, bool registering) 
            {
                this.input = input;
                this.registering = registering;
            }
        }
    }
}