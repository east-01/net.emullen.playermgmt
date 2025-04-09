
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    /// <summary>
    /// The LoginHandler is responsible for retrieving a LocalPlayer's UID, set up to handle
    ///   asynchronous logins.
    /// A LocalPlayer enters the LoginHandler through the Login callback. LoginHandler#Login will
    ///   be called by the PlayerManager once the LocalPlayer is registered.
    /// </summary>
    public abstract class LoginHandler : MonoBehaviour 
    {
        public abstract string Status { get; }


        public delegate void LoginStartedHandler(LocalPlayer localPlayer, bool registering);
        public event LoginStartedHandler LoginStartedEvent;

        public delegate void LoginEndedHandler(LocalPlayer localPlayer, bool registered, PlayerData resultData);
        /// <summary>
        /// Login end event for when the player gets their player data.
        /// </summary>
        public event LoginEndedHandler LoginEndedEvent;

        /// <summary>
        /// Start the log in process for the LocalPlayer, the registering flag is there to signal 
        ///   to each PlayerDatabase that this player wants to create new UIDs. This is important
        ///   to block multipleplayers using the same UID.
        /// Wraps the child classes LoginImpl call, this is so the base Login implementation
        ///   consistently performs the registering steps.
        /// </summary>
        /// <param name="localPlayer"></param>
        /// <param name="registering"></param>
        public void Login(LocalPlayer localPlayer, bool registering) 
        {
            LoginStartedEvent?.Invoke(localPlayer, registering);
            LoginStart(localPlayer, registering);
        }
        /// <summary>
        /// The login implementation, implemented by child classes to actually perform the logging
        ///   in portion.
        /// </summary>
        /// <param name="localPlayer"></param>
        /// <param name="registering"></param>
        protected abstract Task LoginStart(LocalPlayer localPlayer, bool registering);
        /// <summary>
        /// Complete the log in process for the LocalPlayer. Called by child classes to pass the
        ///   new uid back and registerign status.
        /// </summary>
        /// <param name="localPlayer"></param>
        /// <param name="uid"></param>
        /// <param name="registering"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        protected async Task<PlayerData> LoginEnd(LocalPlayer localPlayer, string uid, bool registering = false) 
        {
            if(registering && !await PlayerDataRegistry.Instance.DatabasesHaveUID(uid))
                throw new InvalidOperationException("Can't register player, UID already exists in a loaded database. ");

            PlayerData resultData = localPlayer.AddToPlayerDataRegistry(uid);

            LoginEndedEvent?.Invoke(localPlayer, registering, resultData);

            return resultData;
        }
        public abstract bool IsLoggingIn(LocalPlayer localPlayer);
    }
}