using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using EMullen.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        private string[] TabTitles => new string[] {"Settings", "Network", "Databases"};
        private string[] TabTitlesRuntime => new string[] {"Runtime", "Settings", "Network", "Databases"};
        // Pick tab titles base on the PlayerDataRegistry singleton being instantiated.
        private string[] ActiveTabTites => PlayerDataRegistry.Instance == null ? TabTitles : TabTitlesRuntime;

        // Settings
        private SerializedProperty sp_loginHandler;
        private SerializedProperty sp_logSettings;
        private SerializedProperty sp_logSettingsPlayerData;

        // Networked
        private SerializedProperty sp_joinBroadcastTimeout;

        // Permissions
        private SerializedProperty sp_visibilityMetadata;
        private SerializedProperty sp_mutabilityMetadata;

        // Database
        private SerializedProperty sp_enableDatabase;
        private SerializedProperty sp_databaseMetadatas;

        private void OnEnable() 
        {
            sp_loginHandler = serializedObject.FindProperty("_loginHandler");
            sp_logSettings = serializedObject.FindProperty("logSettings");
            sp_logSettingsPlayerData = serializedObject.FindProperty("logSettingsPlayerData");
            
            sp_joinBroadcastTimeout = serializedObject.FindProperty("joinBroadcastTimeout");

            sp_visibilityMetadata = serializedObject.FindProperty("visibilityMetadata");
            sp_mutabilityMetadata = serializedObject.FindProperty("mutabilityMetadata");

            sp_enableDatabase = serializedObject.FindProperty("enableDatabase");
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
                case "Databases":
                    DrawDatabases();
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
            EditorGUILayout.PropertyField(sp_loginHandler, new GUIContent("Login handler"));
            CustomEditorUtils.CreateNote("Assigns the player's uids.");

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
            GUILayout.Label("[Type Name] [Handler]");

            EditorGUILayout.PropertyField(sp_visibilityMetadata, new GUIContent("Visibility permissions"));
            CustomEditorUtils.CreateNote("Handler \"SERVER_ONLY\" behaves the same as \"OWNER_AND_SERVER\" for visibility.");
            EditorGUILayout.PropertyField(sp_mutabilityMetadata, new GUIContent("Mutability permissions"));
        }

        private void DrawDatabases() 
        {
            EditorGUILayout.PropertyField(sp_enableDatabase, new GUIContent("Authentication required"), true);

            if(!sp_enableDatabase.boolValue) {
                CustomEditorUtils.CreateNote("Databases disabled.");
                return;
            }

            CustomEditorUtils.CreateNote("For file system databases, address is the relative directory.");
            CustomEditorUtils.CreateNote("For SQL databases, address is <endpoint url>[@table name]");

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
}