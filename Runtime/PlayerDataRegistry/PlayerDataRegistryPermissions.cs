using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EMullen.Core;
using EMullen.Core.Editor;
using EMullen.Core.PlayerMgmt;
using FishNet.Connection;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    // Player Data Registry Permissions - Handle permissions for data going over network
    public partial class PlayerDataRegistry : MonoBehaviour 
    {

        [SerializeField]
        private List<TypeNameHandlerPair> visibilityMetadata;
        private Dictionary<Type, Handler> visibility;

        [SerializeField]
        private List<TypeNameHandlerPair> mutabilityMetadata;
        private Dictionary<Type, Handler> mutability;

        private void PermissionsAwake() 
        {
            // Load a list of type names
            Dictionary<Type, Handler> LoadHandlers(List<TypeNameHandlerPair> dataTypeNames) {            
                Dictionary<Type, Handler> outDict = new();
                dataTypeNames.ForEach(handlerPair => {                    
                    // Resolve the type from its name and ensure it loaded properly
                    Type type = GetTypeByName(handlerPair.typeName);
                    if(type == null) {
                        Debug.LogError($"Failed to resolve data type from name \"{handlerPair.typeName}\"");
                        return;
                    }

                    // Check if the loaded type is a base type, this wouldn't make sense as
                    //   functionality comes from derived classes.
                    if(type == typeof(PlayerDataClass) || type == typeof(PlayerDatabaseDataClass))
                        Debug.LogWarning("Loaded a type that resolved to the base PlayerDataClass/PlayerDatabaseDataClass, was this intended?");

                    // Ensure type is valid for PlayerData
                    if(!typeof(PlayerDataClass).IsAssignableFrom(type)) {
                        Debug.LogError($"Type \"{type.Name}\" isn't a subclass of PlayerDataClass, it will not be loaded into permissions.");
                        return;
                    }

                    // If the type isn't already in the output dictionary, add it.
                    if(!outDict.ContainsKey(type))
                        outDict.Add(type, handlerPair.handler);
                });
                return outDict;
            }

            // Load types from their string names into actual types we can compare against
            visibility = LoadHandlers(visibilityMetadata);
            mutability = LoadHandlers(mutabilityMetadata);
        }

        /// <summary>
        /// Check if the PlayerData data type can be shown to the target NetworkConnection.
        /// All calls to this method assume the PlayerDataRegistry is networked.
        /// </summary>
        /// <param name="data">The data to check</param>
        /// <param name="type">The data type to check</param>
        /// <param name="target">The target connection that wants to see the </param>
        /// <returns>True if the data type can be shown</returns>
        internal bool CanShow(PlayerData data, Type type, NetworkConnection target) 
        {
            Handler handler;
            RequireVisibilityHandler rvh = type.GetCustomAttribute<RequireVisibilityHandler>();
            if(rvh != null) {
                handler = rvh.requiredHandler;
            } else if(visibility.ContainsKey(type)) {
                handler = visibility[type];
            } else {
                handler = Handler.EVERYONE;
            }
            return SatisfiesHandlerCase(data, handler, target);
        }

        /// <summary>
        /// Check if the PlayerData's type can be modified by the modifier NetworkConnection.
        /// All calls to this method assume the PlayerDataRegistry is networked.
        /// </summary>
        /// <param name="data">The data that will be modified</param>
        /// <param name="type">The type that is being modified</param>
        /// <param name="modifier">The NetworkConnection who wants to modify the data.</param>
        /// <returns>True if the data type can be modified</returns>
        internal bool CanModify(PlayerData data, Type type, NetworkConnection modifier) 
        {
            Handler handler = mutability.ContainsKey(type) ? mutability[type] : Handler.OWNER_AND_SERVER;
            return SatisfiesHandlerCase(data, handler, modifier);
        }

        private bool SatisfiesHandlerCase(PlayerData data, Handler handler, NetworkConnection conn) 
        {
            if(data == null)
                throw new InvalidOperationException("Can't determine if we can satisfy handler case, data is null.");

            if(!data.HasData<IdentifierData>())
                throw new InvalidOperationException("Can't determine if we can satisfy handler case, PlayerData doesn't have IdentifierData.");

            if(!data.HasData<NetworkIdentifierData>())
                throw new InvalidOperationException("Can't determine if we can satisfy handler case, PlayerData doesn't have NetworkIdentifierData.");

            switch(handler) {
                case Handler.EVERYONE:
                    return true;
                case Handler.OWNER_AND_SERVER:
                    return data.GetData<NetworkIdentifierData>().clientID == conn.ClientId;
                case Handler.SERVER_ONLY:
                    return false;
                default:
                    Debug.LogError($"Failed to cover handler case \"{handler}\"");
                    return false;
            }
        }
    }

    [Serializable]
    public enum Handler {
        EVERYONE, OWNER_AND_SERVER, SERVER_ONLY
    }

    [Serializable]
    public struct TypeNameHandlerPair 
    {
        [SubclassSelector(typeof(PlayerDataClass))]
        public string typeName;
        public Handler handler;
    }
    
    public class RequireVisibilityHandler : Attribute 
    {
        public Handler requiredHandler;
        public RequireVisibilityHandler(Handler requiredHandler) 
        {
            this.requiredHandler = requiredHandler;
        }
    }

    public class RequireMutabilityHandler : Attribute 
    {
        public Handler requiredHandler;
        public RequireMutabilityHandler(Handler requiredHandler) 
        {
            this.requiredHandler = requiredHandler;
        }
    }
}