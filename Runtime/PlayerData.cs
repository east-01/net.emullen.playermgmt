using System;
using System.Collections.Generic;
using System.Linq;
using EMullen.Core;
using FishNet.CodeGenerating;
using FishNet.Serializing;

#if UNITY_2022_3_OR_NEWER
using Unity.Plastic.Newtonsoft.Json;
#else
using Newtonsoft.Json;
#endif

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
        public List<string> TypeNames => Types.Select(type => type.FullName).ToList();
        public List<PlayerDataClass> Datas => data.Values.ToList();

        [JsonConstructor]
        public PlayerData() {
            data = new();
        }

        public PlayerData(int playerIndex) 
        {
            data = new();
            SetData(new IdentifierData(playerIndex), false);
        }

        public PlayerData(IdentifierData identifierData) {
            data = new();
            SetData(identifierData, false);
        }

#region Data Management
        /// <summary>
        /// Internal HasData check, allows for checking if the PlayerData has any type, but will
        ///   throw an exception if the provided type isn't a subclass of PlayerDataClass.
        /// </summary>
        /// <param name="type">The type to check if contained in PlayerData.</param>
        /// <returns>Contains status.</returns>
        /// <exception cref="InvalidOperationException">Provided type isn't a subclass of
        ///    PlayerDataClass</exception>
        internal bool HasData(Type type) 
        {
            // Ensure type is a subclass of PlayerDataClass
            if(!typeof(PlayerDataClass).IsAssignableFrom(type))
                throw new InvalidOperationException("Provided type isn't a subclass of PlayerDataClass.");
        
            return data.ContainsKey(type);
        }

        /// <summary>
        /// Check if the PlayerData has a specific data type, where the data type is a subclass
        ///   of PlayerDataClass.
        /// </summary>
        /// <typeparam name="T">The type to check if contained in PlayerData.</typeparam>
        /// <returns>Contains status.</returns>
        public bool HasData<T>() where T : PlayerDataClass => HasData(typeof(T));

        /// <summary>
        /// Internal GetData call, retrieve the data of any type from the PlayerData.
        /// </summary>
        /// <param name="type">The type to get from PlayerData.</param>
        /// <returns>The PlayerDataClass of type contained in PlayerDataClass.</returns>
        /// <exception cref="InvalidOperationException">Provided type isn't a subclass of
        ///    PlayerDataClass, PlayerData doesn't contain type.</exception>
        public PlayerDataClass GetData(Type type) 
        {
            // Ensure type is a subclass of PlayerDataClass
            if(!typeof(PlayerDataClass).IsAssignableFrom(type))
                throw new InvalidOperationException("Provided type isn't a subclass of PlayerDataClass.");

            // Ensure we have the target PlayerData type
            if(!HasData(type))
                throw new InvalidOperationException($"Failed to retrieve data of type \"{type.Name}\" Returned default values. Use PlayerData#HasData<{type.Name}> to ensure this player has the data before retrieving it.");

            return data[type];
        }

        /// <summary>
        /// Get the PlayerData for a specific data type. 
        /// </summary>
        /// <typeparam name="T">The type to get from PlayerData.</typeparam>
        /// <returns>The PlayerDataClass of type contained in PlayerDataClass.</returns>
        /// <exception cref="InvalidOperationException">Provided type isn't a subclass of
        ///    PlayerDataClass, PlayerData doesn't contain type.</exception>
        public T GetData<T>() where T : PlayerDataClass => (T) GetData(typeof(T));

        /// <summary>
        /// Internal SetData call, allows setting of any object type, as long as the type is a
        ///   subclass of PlayerDataClass.
        /// </summary>
        /// <param name="data">The object data to be set (must be subdata of PlayerDataClass)</param>
        /// <param name="type">The type of the data</param>
        /// <param name="update">Update the PlayerData in the registry.</param>
        /// <exception cref="InvalidOperationException">The provided type is not a subclass of 
        ///   PlayerDataClass.</exception>
        internal void SetData(object data, Type type, bool update = true) 
        {
            // Ensure type is a subclass of PlayerDataClass
            if(!typeof(PlayerDataClass).IsAssignableFrom(type))
                throw new InvalidOperationException($"Provided type ({type.Name}) isn't a subclass of PlayerDataClass.");

            // Overwrite or add incoming data to the data dictionary.
            if(HasData(type))
                this.data[type] = (PlayerDataClass) data;
            else
                this.data.Add(type, (PlayerDataClass) data);
            
            // Update if necessary
            if(update)
                PlayerDataRegistry.Instance.UpdatePlayerData(this, type);
        }

        /// <summary>
        /// SetData for a specific data type, with an added update call to allow blocking of
        ///   automatic updates.
        /// </summary>
        /// <typeparam name="T">The data type to set.</typeparam>
        /// <param name="data">The data object to set in the PlayerData.</param>
        /// <param name="update">Update the PlayerData in the registry.</param>
        internal void SetData<T>(T data, bool update = true) where T : PlayerDataClass => SetData(data, typeof(T), update);

        /// <summary>
        /// SetData for a specific data type, with an added update call to allow blocking of
        ///   automatic updates.
        /// </summary>
        /// <typeparam name="T">The data type to set.</typeparam>
        /// <param name="data">The data object to set in the PlayerData.</param>
        public void SetData<T>(T data) where T : PlayerDataClass => SetData(data, typeof(T));

        /// <summary>
        /// Intern ClearData call allowing you to clear the data for any specific type.
        /// </summary>
        /// <param name="type">The type to clear</param>
        /// <param name="update">Perform an UpdatePlayerData call in the registry after updating.</param>
        internal void ClearData(Type type, bool update = true) 
        {
            if(!HasData(type)) {
                Debug.LogError($"Can't clear data of type \"{type.Name}\" the PlayerData doesn't have it.");
                return;
            }

            data.Remove(type);

            if(update)
                PlayerDataRegistry.Instance.UpdatePlayerData(this, type);
        }

        /// <summary>
        /// Remove the data for a specific data type.
        /// </summary>
        /// <typeparam name="T">The data type to clear.</typeparam>
        public void ClearData<T>() where T : PlayerDataClass => ClearData(typeof(T));
#endregion

    public PlayerData Clone()
    {
        PlayerData clone = new();
        data.Values.ToList().ForEach(playerDataClass => {
            string pdcJson = JsonConvert.SerializeObject(playerDataClass);
            object clonedClass = JsonConvert.DeserializeObject(pdcJson, playerDataClass.GetType());
            clone.SetData(clonedClass, clonedClass.GetType(), false);            
        });
        return clone;
    }

    // public PlayerData Clone()
    // {
    //     using (MemoryStream stream = new MemoryStream())
    //     {
    //         BinaryFormatter formatter = new BinaryFormatter();
    //         formatter.Serialize(stream, this);
    //         stream.Position = 0;
    //         return (PlayerData)formatter.Deserialize(stream);
    //     }
    // }
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
                pd.SetData(deserialized, deserialized.GetType(), false);
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
