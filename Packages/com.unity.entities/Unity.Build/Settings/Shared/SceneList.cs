using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Build
{
    public class SceneList : IBuildSettingsComponent
    {
        public bool                  BuildCurrentScene;
        //@TODO: This needs to be a GUID we should probably move Hash128 to be in Unity.Collections to make this happen?
        public readonly List<string> Scenes = new List<string>();

        public string[] GetScenePathsForBuild()
        {
            if (BuildCurrentScene)
            {
                // Build a list of the root scenes
                var rootScenes = new List<string>();
                for (int i = 0; i != EditorSceneManager.sceneCount; i++)
                {
                    var scene = EditorSceneManager.GetSceneAt(i);
                    if (scene.isSubScene)
                        continue;
                    if (!scene.isLoaded)
                        continue;
                    if (EditorSceneManager.IsPreviewScene(scene))
                        continue;
                    if (string.IsNullOrEmpty(scene.path))
                        continue;

                    rootScenes.Add(scene.path);
                }

                return rootScenes.ToArray();
            }
            else
            {
                return Scenes.ToArray();
            }
        }


        public string Name => "Scenes";

        private int currentPickerWindow;

        public bool OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            BuildCurrentScene = EditorGUILayout.Toggle("Build Current Scene", BuildCurrentScene);
            if (EditorGUI.EndChangeCheck())
                return true;

            using (var disabled = new EditorGUI.DisabledScope(BuildCurrentScene))
            {
                // Placeholder GUI until we'll have generic inspector
                for (int i = 0; i < Scenes.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    EditorGUI.BeginChangeCheck();
                    var asset = EditorGUILayout.ObjectField(AssetDatabase.LoadAssetAtPath<SceneAsset>(Scenes[i]), typeof(SceneAsset), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Scenes[i] = AssetDatabase.GetAssetPath(asset);
                        return true;
                    }

                    if (GUILayout.Button("Remove"))
                    {
                        Scenes.RemoveAt(i);
                        return true;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                currentPickerWindow = EditorGUIUtility.GetControlID(FocusType.Passive) + 100;

                if (GUILayout.Button("Add scene"))
                    EditorGUIUtility.ShowObjectPicker<SceneAsset>(null, false, "", currentPickerWindow);

                if (Event.current.commandName == "ObjectSelectorClosed" && EditorGUIUtility.GetObjectPickerControlID() == currentPickerWindow)
                {
                    var scene = EditorGUIUtility.GetObjectPickerObject();
                    var path = AssetDatabase.GetAssetPath(scene);
                    if (scene != null && !Scenes.Contains(path))
                        Scenes.Add(path);
                    currentPickerWindow = -1;
                    // Note: EditorGUI.BeginChangeCheck works incorrectly with object selector
                    return true;
                }
                
                return false;
            }

        }
    }
}
