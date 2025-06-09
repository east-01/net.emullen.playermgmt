using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EMullen.PlayerMgmt
{
    /// <summary>
    /// The PlayerAuthenticator is used to register, log in, and verify users.
    /// Operations make UnityWebRequests to the Node.js authenticator server, via the WebRequests
    ///   class.
    /// </summary>
    public class PlayerAuthenticator
    {
    
        /// <summary>
        /// Address of the authentication Node.js server.
        /// </summary>
        private readonly string authServerAddr;

        public PlayerAuthenticator(string authServerAddr) 
        {
            this.authServerAddr = authServerAddr;
        }

        /// <summary>
        /// Register a user on the authentication server. Will send an auth JSON body to the
        ///   authServerAddr/register endpoint, and throws an AuthenticationException if it fails
        ///   during this process.
        /// </summary>
        /// <param name="username">The username to register</param>
        /// <param name="password">The password to register</param>
        /// <exception cref="AuthenticationException">If the user fails to register, i.e. the user
        ///   already exists</exception>
        public async Task Register(string username, string password) 
        {
            string reqBody = WebRequests.CreateAuthJSON(username, password).ToString();
            
            try {
                // Make the post request, if it goes through without error that means we've
                //  successfully registered.
                await WebRequests.WebPostString($"{authServerAddr}/register", reqBody);
            } catch(UnityWebRequestException exception) {
                // Throw authentication exception with details from the WebRequestException
                throw new AuthenticationException(exception.responseCode == 400 ? exception.text : "A backend server error occurred.", exception);
            }
        }

        /// <summary>
        /// Log in as a user on the authentication server. Will send an auth JSON body to the
        ///   authServerAddr/login endpoint, and throws an AuthenticationException if it fails
        ///   during this process.
        /// </summary>
        /// <param name="username">The username to log in with</param>
        /// <param name="password">The password to log in with</param>
        /// <returns>The returned JSON from the server, containing the users UID and a token to
        ///   access SQL server data.</returns>
        /// <exception cref="AuthenticationException">If the log in information is invalid, i.e.
        ///   username doesn't exist or password is incorrect.</exception>
        public async Task<JObject> LogIn(string username, string password) 
        {
            // Create the request body and json string
            string reqBody = WebRequests.CreateAuthJSON(username, password).ToString();
            string json;
            
            try {
                // Make the post request, it will return a json string to be parsed into a JObject
                json = await WebRequests.WebPostString($"{authServerAddr}/login", reqBody);
            } catch(UnityWebRequestException exception) {
                // Throw authentication exception with details from the WebRequestException
                throw new AuthenticationException(exception.responseCode == 400 ? exception.text : "A backend server error occurred.", exception);
            }

            JObject parsedJSON;

            try {
                // Parse the provided string json into a JObject 
                parsedJSON = JObject.Parse(json);
            } catch(JsonReaderException exception) {
                Debug.LogError(exception.Message);
                return null;
            }

            return parsedJSON;
        }

        public async Task<JObject> RefreshLogIn(string token) 
        {
            string reqBody = WebRequests.CreateRefreshJSON(token).ToString();
            string json;

            try {
                json = await WebRequests.WebPostString($"{authServerAddr}/refresh-token", reqBody);
            } catch(UnityWebRequestException exception) {
                // Throw authentication exception with details from the WebRequestException
                throw new AuthenticationException(exception.responseCode == 400 ? exception.text : "A backend server error occurred.", exception);
            }

            JObject parsedJSON;

            try {
                // Parse the provided string json into a JObject 
                parsedJSON = JObject.Parse(json);
            } catch(JsonReaderException exception) {
                Debug.LogError(exception.Message);
                return null;
            }

            return parsedJSON;
        }
    }

    /// <summary>
    /// An exception relating to all authentication errors, holds the culprit WebRequestException
    ///   and additional message from the authentication server.
    /// </summary>
    [Serializable]
    public class AuthenticationException : Exception
    {
        public UnityWebRequestException WebException => InnerException != null ? (UnityWebRequestException) InnerException : null;

        public AuthenticationException() : base() {}
        public AuthenticationException(string message) : base(message) {}
        public AuthenticationException(string message, Exception innerException) : base(message, innerException) {}
        protected AuthenticationException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }
}
