using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace EMullen.PlayerMgmt 
{
    /// <summary>
    /// A library of various web requests, all methods are asynchronous.
    /// </summary>
    public class WebRequests 
    {
#region Web calls
        /// <summary>
        /// Perform a web POST request via the provided url, takes the result and returns it in the
        ///   form of a string.
        /// The body of the POST request is converted into a byte array via Encoding#UTF8#GetBytes,
        ///   it is expected to be a json value.
        /// </summary>
        /// <param name="url">The URL of the POST request.</param>
        /// <param name="body">The body of the POST request.</param>
        /// <returns>The string form of the result.</returns>
        public static async Task<string> WebPostString(string url, string body)
        {
            // Convert string body into byte array for uploadhandler.
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            // Create and configure the web request, using UnityWebRequest#POST causes issues.
            using UnityWebRequest webRequest = new(url);
            webRequest.method = UnityWebRequest.kHttpVerbPOST;
            webRequest.disposeUploadHandlerOnDispose = true;
            webRequest.disposeDownloadHandlerOnDispose = true;

            using UploadHandlerRaw uploadHandlerRaw = new(bodyRaw);
            using DownloadHandlerBuffer downloadHandler = new();

            webRequest.uploadHandler = uploadHandlerRaw;
            webRequest.downloadHandler = downloadHandler;
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // Perform the web request and wait for it to finish
            var asyncOp = webRequest.SendWebRequest();
            while(!asyncOp.isDone)
                await Task.Yield();

            bool hasError = webRequest.result == UnityWebRequest.Result.ConnectionError ||
                            webRequest.result == UnityWebRequest.Result.ProtocolError;
    
            string result = webRequest.downloadHandler.text;

            if(webRequest.result == UnityWebRequest.Result.ConnectionError ||
               webRequest.result == UnityWebRequest.Result.ProtocolError) {
                throw new UnityWebRequestException(webRequest.result, webRequest.responseCode, webRequest.error, result);
            }

            return result;
        }
#endregion

#region Request JSON objects
        public static JObject CreateAuthJSON(string username, string password) 
        {
            return new() {
                {"username", username},
                {"password", password}
            };
        }

        public static JObject CreateRefreshJSON(string token) 
        {
            return new() {
                {"refreshToken", token}
            };
        }

        public static JObject CreateSQLJSON(string token, string tableName) 
        {
            return new() {
                {"token", token},
                {"table", tableName}
            };
        }

        public static JObject CreateIDJSON(string token, string tableName, string uid) 
        {
            JObject jsonBase = CreateSQLJSON(token, tableName);
            jsonBase.Add("uid", uid);
            return jsonBase;
        }

        public static JObject CreateInsertJSON(string token, string tableName, string jsonData) 
        {
            JObject jsonBase = CreateSQLJSON(token, tableName);
            JObject parsedJson = LowerCaseKeys(JObject.Parse(jsonData));
            jsonBase.Add("data", parsedJson);
            return jsonBase;
        }

        public static JObject LowerCaseKeys(JObject input) 
        {
            if(input == null) {
                Debug.LogError("Can't convert input to lower case keys, input is null.");
                return null;
            }
            JObject output = new JObject();
            foreach(JProperty property in input.Properties()) {
                output.Add(property.Name.ToLower(), property.Value);
            }
            return output;
        }
#endregion
    }

    [Serializable]
    public class UnityWebRequestException : Exception 
    {
        public readonly UnityWebRequest.Result result;
        public readonly long responseCode;
        public readonly string error;
        public readonly string text;

        public UnityWebRequestException(UnityWebRequest.Result result, long responseCode, string error, string text) {
            this.result = result;
            this.responseCode = responseCode;
            this.error = error;
            this.text = text;
        }
    }
}