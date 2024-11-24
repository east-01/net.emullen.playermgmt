using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EMullen.Core;
using EMullen.PlayerMgmt;
using Newtonsoft.Json;

namespace EMullen.PlayerMgmt.Editor 
{
    [CustomEditor(typeof(PlayerDataRegistry), true)]
    public class PlayerDataRegistryEditor : UnityEditor.Editor 
    {

        /* Editor properties */
        private Vector2 scrollPos;
        private int selectedTabIndex;
        private string SelectedTab 
        { get { 
            // Ensure we don't get an out of bounds exception when checking
            selectedTabIndex %= ActiveTabTites.Length;
            return ActiveTabTites[selectedTabIndex]; 
        } }
        private string[] TabTitles => new string[] {"Settings", "Network", "Web"};
        private string[] TabTitlesRuntime => new string[] {"Runtime", "Settings", "Network", "Web"};
        // Pick tab titles base on the PlayerDataRegistry singleton being instantiated.
        private string[] ActiveTabTites => PlayerDataRegistry.Instance == null ? TabTitles : TabTitlesRuntime;

        // Settings
        private SerializedProperty sp_logSettings;
        private SerializedProperty sp_logSettingsPlayerData;

        // Networked
        private SerializedProperty sp_joinBroadcastTimeout;

        // Permissions
        private SerializedProperty sp_visibilityMetadata;
        private SerializedProperty sp_mutabilityMetadata;

        // Web
        private SerializedProperty sp_authenticationRequired;
        private SerializedProperty sp_authAddress;
        private SerializedProperty sp_databaseMetadatas;

        private void OnEnable() 
        {
            sp_logSettings = serializedObject.FindProperty("logSettings");
            sp_logSettingsPlayerData = serializedObject.FindProperty("logSettingsPlayerData");

            sp_joinBroadcastTimeout = serializedObject.FindProperty("joinBroadcastTimeout");

            sp_visibilityMetadata = serializedObject.FindProperty("visibilityMetadata");
            sp_mutabilityMetadata = serializedObject.FindProperty("mutabilityMetadata");

            sp_authenticationRequired = serializedObject.FindProperty("authenticationRequired");
            sp_authAddress = serializedObject.FindProperty("authAddress");
            sp_databaseMetadatas = serializedObject.FindProperty("databaseMetadatas");
        }

        public override void OnInspectorGUI() 
        {
            // Ensure we don't get an out of bounds exception when checking
            selectedTabIndex %= ActiveTabTites.Length;
            selectedTabIndex = GUILayout.Toolbar(selectedTabIndex, ActiveTabTites);

            GUILayout.Space(5);

            switch(SelectedTab) {
                case "Runtime":
                    DrawRuntime();
                    break;
                case "Settings":
                    DrawSettings();
                    break;
                case "Network":
                    DrawNetwork();
                    break;
                case "Web":
                    DrawWeb();
                    break;
            }

            serializedObject.ApplyModifiedProperties();            
        }

        private int selectedPlayerDataIndex;
        private void DrawRuntime() 
        {
            PlayerDataRegistry pdr = PlayerDataRegistry.Instance;
#if FISHNET
            GUILayout.Label($"Network phase: {pdr.NetworkPhase}");
            if(pdr.Networked)
                GUILayout.Label("Is Networked ✓");
#endif

            if(pdr.GetAllData().Length == 0) {
                GUILayout.Label("PlayerDataRegistry is empty");
                return;
            }

            GUILayout.Label("PlayerData in registry:");
            string[] buttonLabels = pdr.GetAllData().Select(pd => pd.GetUID()).ToArray();
            for (int i = 0; i < buttonLabels.Length; i++) {
                GUI.backgroundColor = i == selectedPlayerDataIndex ? new Color(0.747f, 0.768f, 1f) : Color.gray;
                if (GUILayout.Button(buttonLabels[i]))
                    selectedPlayerDataIndex = i; // Update the selected button index
            }

            PlayerData pd = pdr.GetPlayerData(buttonLabels[selectedPlayerDataIndex]);
            foreach(Type type in pd.Types) {
                GUILayout.Label($"  {type.Name}:");
                string prefix = "  ";
                string json = JsonConvert.SerializeObject(pd.GetData(type), Formatting.Indented);
                json = json.Replace("\n", $"\n{prefix}");
                CustomEditorUtils.CreateNote($"  {json}");
            }
        }

        private void DrawSettings() 
        {
            EditorGUILayout.PropertyField(sp_logSettings, new GUIContent("Registry log settings"));
            EditorGUILayout.PropertyField(sp_logSettingsPlayerData, new GUIContent("PlayerData log settings"));
        }

        private void DrawNetwork() 
        {
            // Network
            CustomEditorUtils.CreateHeader("Network");
            EditorGUILayout.PropertyField(sp_joinBroadcastTimeout, new GUIContent("Join broadcast timeout"));
            CustomEditorUtils.CreateNote("How long, in seconds, after sending a join broadcast to resend (if not already Networked).");


            // Permissions
            CustomEditorUtils.CreateHeader("Permissions");
            CustomEditorUtils.CreateNote("Check mark represents the PlayerDataRegistry resolving the type name to a true Type correctly.\n");
            GUILayout.Label("[Type Name] [Handler]");

            EditorGUILayout.PropertyField(sp_visibilityMetadata, new GUIContent("Visibility permissions"));
            EditorGUILayout.PropertyField(sp_mutabilityMetadata, new GUIContent("Mutability permissions"));
        }

        private void DrawWeb() 
        {
            EditorGUILayout.PropertyField(sp_authenticationRequired, new GUIContent("Authentication required"), true);

            if(!sp_authenticationRequired.boolValue) {
                CustomEditorUtils.CreateNote("Authentication is not required. Enable it to access authentication and database features.");
                return;
            }

            EditorGUILayout.PropertyField(sp_authAddress, new GUIContent("Auth address"));
            CustomEditorUtils.CreateNote("Address of the authentication server.");

            EditorGUILayout.PropertyField(sp_databaseMetadatas);
        }
    }

    [CustomPropertyDrawer(typeof(TypeNameHandlerPair))]
    public class TypeNameHandlerPairDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Start property drawing
            EditorGUI.BeginProperty(position, label, property);

            // Calculate rects
            float fieldWidth = position.width / 2;

            Rect typeNameRect = new Rect(position.x, position.y, fieldWidth, position.height);
            Rect handlerRect = new Rect(position.x + 5 + fieldWidth, position.y, fieldWidth, position.height);

            EditorGUI.PropertyField(typeNameRect, property.FindPropertyRelative("typeName"), GUIContent.none);
            EditorGUI.PropertyField(handlerRect, property.FindPropertyRelative("handler"), GUIContent.none);

            // End property drawing
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Ensure single line height
            return EditorGUIUtility.singleLineHeight;
        }
    }

    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        private static Dictionary<Type, Type[]> subclassCache = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attribute = (SubclassSelectorAttribute)base.attribute;
            var baseType = attribute.BaseType;

            // Retrieve subclasses from cache or compute them if not cached
            if (!subclassCache.TryGetValue(baseType, out var subclasses))
            {
                subclasses = AppDomain.CurrentDomain.GetAssemblies()
                                .SelectMany(assembly => assembly.GetTypes())
                                .Where(type => type.IsSubclassOf(baseType) && !type.IsAbstract)
                                .ToArray();
                subclassCache[baseType] = subclasses;
            }

            // Get the current selected subclass name
            string currentSubclass = property.stringValue;

            // Create a list of names for the dropdown
            // Sort names by standard Name, for same name variables
            List<string> subclassNames = subclasses.Select(t => t.FullName).ToList();
            subclassNames.Sort();
            string[] subclassNamesArr = subclassNames.ToArray();
            string[] showSubClassNames = (string[]) subclassNamesArr.Clone();
            for(int i = 0; i < showSubClassNames.Length-1; i++) {
                string[] curr = showSubClassNames[i].Split('.');
                string[] next = showSubClassNames[i+1].Split('.');

                List<string> builder = new();
                for(int j = curr.Length-1; j >= 0; j--) {
                    builder.Add(curr[j]);
                    if(j > next.Length-1 || curr[j] != next[j])
                        break;
                }
                showSubClassNames[i] = string.Join(".", builder);               
            }
            int currentIndex = Array.IndexOf(subclassNamesArr, currentSubclass);

            List<string> strippedNames = subclassNames.Select(fullName => fullName.Split(".")[^1]).ToList();
            for(int i = 0; i < strippedNames.Count; i++) {
                string currName = strippedNames[i];
                for(int j = 0; j < strippedNames.Count; j++) {
                    if(j == i)
                        continue;
                    
                    string targName = strippedNames[j];
                    if(currName == targName) {
                        
                    }
                }
            }

            // Draw the dropdown
            int newIndex = EditorGUI.Popup(position, label.text, currentIndex, showSubClassNames);

            // If a new subclass is selected, update the string value
            if (newIndex != currentIndex)
            {
                property.stringValue = subclassNames[newIndex];
            }
        }
    }
}