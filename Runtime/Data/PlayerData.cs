using System;
using System.Collections.Generic;
using System.Linq;
using EMullen.Core;
using FishNet.CodeGenerating;
using FishNet.Serializing;
using Newtonsoft.Json;
using UnityEngine;

namespace EMullen.PlayerMgmt {
    /// <summary>
    /// This is data relating to the in game player.
    /// </summary>
    [UseGlobalCustomSerializer]
    [Serializable]
    public class PlayerData : ISerializationCallbackReceiver {

        private readonly Dictionary<Type, PlayerDataClass> data;
        
        [SerializeField]
        private List<PlayerDataClass> serializedClasses;

        public List<Type> Types => data.Keys.ToList();
        public List<string> TypeNames => Types.Select(type => type.Name).ToList();
        public List<PlayerDataClass> Datas => data.Values.ToList();

        [JsonConstructor]
        public PlayerData() {
            data = new();
        }

        public PlayerData(int playerIndex) 
        {
            data = new();
            SetDataNoUpdate(new IdentifierData(playerIndex));
        }

        public PlayerData(IdentifierData identifierData) {
            data = new();
            SetDataNoUpdate(identifierData);
        }

#region Data Management
        public bool HasData<T>() where T : PlayerDataClass => data.ContainsKey(typeof(T));
        public T GetData<T>() where T : PlayerDataClass
        {
            if(!HasData<T>()) {
                UnityEngine.Debug.LogError($"Failed to retrieve data of type \"{typeof(T)}\" Returned default values. Use PlayerData#HasData<{typeof(T)}> to ensure this player has the data before retrieving it.");
                return default(T);
            }
            return (T) data[typeof(T)];
        }

        public void SetData<T>(T data) where T : PlayerDataClass
        {
            SetDataNoUpdate(data);
            PlayerDataRegistry.Instance.UpdatePlayerData(this);
        }

        /// <summary>
        /// Sets the players data without making an update callback to the PlayerDataRegistry,
        ///   not recommended for use as the PlayerDataRegistry should always have the latest
        ///   information. Used for initialization.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        private void SetDataNoUpdate<T>(T data) where T : PlayerDataClass 
        {
            if(HasData<T>()) {
                this.data[typeof(T)] = data;
            } else {
                this.data.Add(typeof(T), data);
            }
        }

        /// <summary>
        /// Set data from an object type instead of a PlayerDataClass.
        /// Used interally for deserialization where we can infer the type is a PlayerDataClass.
        /// </summary>
        internal void SetData(object data) 
        {
            BLog.Log($"Setting anonymous data type \"{data.GetType()}\"", PlayerDataRegistry.Instance.LogSettingsPlayerData, 4);
            if(data is not PlayerDataClass) {
                Debug.LogError("Can't SetData anonymously since it is not an instance of PlayerDataClass");
                return;
            }
            if(this.data.ContainsKey(data.GetType())) {
                this.data[data.GetType()] = (PlayerDataClass)data;
            } else {
                this.data.Add(data.GetType(), (PlayerDataClass)data);
            }
        }

        public void ClearData<T>() where T : PlayerDataClass 
        {
            if(!HasData<T>()) {
                UnityEngine.Debug.LogError($"Can't clear data of type \"{typeof(T)}\"");
                return;
            }
            data.Remove(typeof(T));
            PlayerDataRegistry.Instance.UpdatePlayerData(this);
        }
#endregion

#region Serializers
        public List<PlayerDataClass> PlayerDataClassList => data.Values.ToList();
        public void DeserializePlayerDataClassList(List<PlayerDataClass> playerDataClassList) 
        {
            foreach(PlayerDataClass cls in playerDataClassList) {
                data.Add(cls.GetType(), cls);
            }
        }

        public void OnBeforeSerialize()
        {
            serializedClasses.Clear();
            serializedClasses.Concat(PlayerDataClassList);         
        }

        public void OnAfterDeserialize()
        {
            DeserializePlayerDataClassList(serializedClasses);
        }

        
#endregion

    }
    
    /// <summary>
    /// This class exists to enforce child classes to be Serializable, that way we can network
    ///   the information if necessary.
    /// </summary>
    [Serializable]
    public abstract class PlayerDataClass {}
    
    public static class FishnetPlayerDataSerializer 
    {

        public static JsonSerializerSettings settings = new()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore/*,
            Formatting = Formatting.Indented // Optional: for pretty printing the JSON */
        };

        public static void WritePlayerData(this Writer writer, PlayerData value) 
        {
            int count = value.Datas.Count;
            writer.Write<int>(count);
            BLog.Log($"Serializing PlayerData packet with {count} class(es).", PlayerDataRegistry.Instance.LogSettingsPlayerData, 4);
            foreach(PlayerDataClass pdc in value.Datas) {
                writer.WriteString(pdc.GetType().ToString());
                string json = JsonConvert.SerializeObject(pdc, pdc.GetType(), settings);
                BLog.Log($"Serialized type \"{pdc.GetType()}\" into:", PlayerDataRegistry.Instance.LogSettingsPlayerData, 4);
                BLog.Log(json, PlayerDataRegistry.Instance.LogSettingsPlayerData, 4);
                writer.WriteString(json);
            }
        }

        public static PlayerData ReadPlayerData(this Reader reader) 
        {
            PlayerData pd = new();

            int count = reader.ReadInt32();
            BLog.Log($"Deserializing PlayerData packet with {count} class(es).", PlayerDataRegistry.Instance.LogSettingsPlayerData, 4);
            while(count > 0) {
                string typeString = reader.ReadString();
                Type type = ResolveType(typeString);
                string json = reader.ReadString();
                if(type == null) {
                    Debug.LogError($"Failed to get type from type string \"{typeString}\". Can't deserialize json:");
                    Debug.LogError(json);
                    continue;
                }
                object deserialized = JsonConvert.DeserializeObject(json, type);
                pd.SetData(deserialized);
                BLog.Log($"Deserializing type \"{typeString}\" from json:", PlayerDataRegistry.Instance.LogSettingsPlayerData, 4);
                BLog.Log(json, PlayerDataRegistry.Instance.LogSettingsPlayerData, 4);
                count--;
            }

            return pd;
        }

        private static Type ResolveType(string typeString) 
        {
            Type type = Type.GetType(typeString);
            if (type == null) 
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) 
                {
                    type = assembly.GetType(typeString);
                    if (type != null)
                        break;
                }
            }

            if (type == null)
                UnityEngine.Debug.LogError($"Failed to resolve type: {typeString}");

            return type;
        }

    }
}
