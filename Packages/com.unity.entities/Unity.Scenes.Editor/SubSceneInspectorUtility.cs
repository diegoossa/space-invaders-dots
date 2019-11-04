using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.Windows;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor
{
    internal static class SubSceneInspectorUtility
    {
        public static Transform GetUncleanHierarchyObject(SubScene[] subscenes)
        {
            foreach (var scene in subscenes)
            {
                var res = GetUncleanHierarchyObject(scene.transform);
                if (res != null)
                    return res;
            }
    
            return null;
        }
    
        public static Transform GetUncleanHierarchyObject(Transform child)
        {
            while (child)
            {
                if (child.localPosition != Vector3.zero)
                    return child;
                if (child.localRotation != Quaternion.identity)
                    return child;
                if (child.localScale!= Vector3.one)
                    return child;
                
                child = child.parent;
            }
    
            return null;
        }
        
        public static bool HasChildren(SubScene[] scenes)
        {
            foreach (var scene in scenes)
            {
                if (scene.transform.childCount != 0)
                    return true;
            }
    
            return false;
        }
    
        public static void CloseSceneWithoutSaving(params SubScene[] scenes)
        {
            foreach(var scene in scenes)
                EditorSceneManager.CloseScene(scene.EditingScene, true);
        }
    
        public struct LoadableScene
        {
            public Entity Scene;
            public string Name;
        }

        static NativeArray<Entity> GetActiveWorldSections(World world, Hash128 sceneGUID)
        {
            var sceneSystem = world?.GetExistingSystem<SceneSystem>();
            var entities = world?.EntityManager;
            if (sceneSystem == null)
                return default;

            var sceneEntity = sceneSystem.GetSceneEntity(sceneGUID);

            if (!entities.HasComponent<ResolvedSectionEntity>(sceneEntity))
                return default;
            return entities.GetBuffer<ResolvedSectionEntity>(sceneEntity).Reinterpret<Entity>().AsNativeArray();
        }


        public static LoadableScene[] GetLoadableScenes(SubScene[] scenes)
        {
            var loadables = new List<LoadableScene>();
            
            foreach (var scene in scenes)
            {
                foreach (var section in GetActiveWorldSections(World.DefaultGameObjectInjectionWorld, scene.SceneGUID))
                {
                    var name = scene.SceneAsset.name;
                    if (World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<SceneSectionData>(section))
                    {
                        var sectionIndex = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<SceneSectionData>(section).SubSectionIndex;
                        if (sectionIndex != 0)
                            name += $" Section: {sectionIndex}";
                        
                        loadables.Add(new LoadableScene
                        {
                            Scene = section,
                            Name = name
                        });
                    }
                }
            }
    
            return loadables.ToArray();
        }
        
        public static bool IsEditingAll(SubScene[] scenes)
        {
            foreach (var scene in scenes)
            {
                if (!scene.IsLoaded)
                    return false;
            }
    
            return true;
        }
        
        public static bool CanEditScene(SubScene scene)
        {
#if UNITY_EDITOR
            // Disallow editing when in prefab edit mode
            if (PrefabStageUtility.GetPrefabStage(scene.gameObject) != null)
                return false;
            if (!scene.isActiveAndEnabled)
                return false;
#endif
    
            return !scene.IsLoaded;
        }
    
        public static bool IsLoaded(SubScene[] scenes)
        {
            foreach (var subScene in scenes)
            {
                if (subScene.IsLoaded)
                    return true;
            }
    
            return false;
        }
        
        public static bool CanEditScene(SubScene[] scenes)
        {
            foreach (var subScene in scenes)
            {
                if (CanEditScene(subScene))
                    return true;
            }
    
            return false;
        }
    
        public static void EditScene(params SubScene[] scenes)
        {
            foreach (var subScene in scenes)
            {
                if (CanEditScene(subScene))
                {
                    Scene scene;
                    if (Application.isPlaying)
                        scene = EditorSceneManager.LoadSceneInPlayMode(subScene.EditableScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
                    else
                        scene = EditorSceneManager.OpenScene(subScene.EditableScenePath, OpenSceneMode.Additive);
                    scene.isSubScene = true;
                }
            }
        }
    
        
        public static void CloseAndAskSaveIfUserWantsTo(SubScene[] subScenes)
        {
            if (!Application.isPlaying)
            {
                var dirtyScenes = new List<Scene>();
                foreach (var scene in subScenes)
                {
                    if (scene.EditingScene.isLoaded && scene.EditingScene.isDirty)
                    {
                        dirtyScenes.Add(scene.EditingScene);
                    }
                }
    
                if (dirtyScenes.Count != 0)
                {
                    if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(dirtyScenes.ToArray()))
                        return;
                }
            
                CloseSceneWithoutSaving(subScenes);
            }
            else
            {
                foreach (var scene in subScenes)
                {
                    if (scene.EditingScene.isLoaded)
                        EditorSceneManager.UnloadSceneAsync(scene.EditingScene);
                }
            }
        }
        
        public static void SaveScene(SubScene[] subScenes)
        {
            foreach (var scene in subScenes)
            {
                if (scene.EditingScene.isLoaded && scene.EditingScene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene.EditingScene);
                }
            }
        }
        public static bool IsDirty(SubScene[] scenes)
        {
            foreach (var scene in scenes)
            {
                if (scene.EditingScene.isLoaded && scene.EditingScene.isDirty)
                    return true;
            }
    
            return false;
        }
        
        public static MinMaxAABB GetActiveWorldMinMax(World world, UnityEngine.Object[] targets)
        {
            MinMaxAABB bounds = MinMaxAABB.Empty;

            var entities = world?.EntityManager;
            foreach (SubScene subScene in targets)
            {
                foreach (var section in GetActiveWorldSections(World.DefaultGameObjectInjectionWorld, subScene.SceneGUID))
                {
                    if (entities.HasComponent<SceneBoundingVolume>(section))
                        bounds.Encapsulate(entities.GetComponentData<SceneBoundingVolume>(section).Value);
                }
            }

            return bounds;
        }
         
        // Visualize SubScene using bounding volume when it is selected.
        public static void DrawSubsceneBounds(SubScene scene)
        {
            var isEditing = scene.IsLoaded;

            var entities = World.DefaultGameObjectInjectionWorld?.EntityManager;
            foreach (var section in GetActiveWorldSections(World.DefaultGameObjectInjectionWorld, scene.SceneGUID))
            {
                if (!entities.HasComponent<SceneBoundingVolume>(section))
                    continue;
                
                if (isEditing)
                    Gizmos.color = Color.green;
                else
                    Gizmos.color = Color.gray;
                
                AABB aabb = entities.GetComponentData<SceneBoundingVolume>(section).Value;
                Gizmos.DrawWireCube(aabb.Center, aabb.Size);
            }
        }
    
        #if !ENABLE_SUBSCENE_IMPORTER
        public static void RebuildEntityCache(params SubScene[] scenes)
        {
            try
            {
                Profiler.BeginSample("AssetDatabase.StartAssetEditing");
                AssetDatabase.StartAssetEditing();
                Profiler.EndSample();
                
                for (int i = 0; i != scenes.Length; i++)
                {
                    var scene = scenes[i];
                    EditorUtility.DisplayProgressBar("Rebuilding Entity Cache", scene.SceneName, (float) i / scenes.Length);

                    var isLoaded = scene.IsLoaded;
                    if (!isLoaded)
                        EditScene(scene);

                    try
                    {
                        EditorEntityScenes.WriteEntityScene(scene);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }


                    if (!isLoaded)
                        CloseSceneWithoutSaving(scene);
                }
            }
            finally
            {
                Profiler.BeginSample("AssetDatabase.StopAssetEditing");
                AssetDatabase.StopAssetEditing();
                Profiler.EndSample();

                EditorUtility.ClearProgressBar();
            }
        }
        #endif
    
        public static string GetEntitySceneWarning(SubScene[] scenes, out bool requireCacheRebuild)
        {
            requireCacheRebuild = false;
            foreach (var scene in scenes)
            {
                if (scene.SceneAsset == null)
                    return $"Please assign a valid Scene Asset";
                
#if !ENABLE_SUBSCENE_IMPORTER
                var sceneHeaderPath = EntityScenesPaths.GetLoadPath(scene.SceneGUID, EntityScenesPaths.PathType.EntitiesHeader, -1);
                if (!File.Exists(sceneHeaderPath))
                {
                    requireCacheRebuild = true;
                    return $"The entity binary file header couldn't be found. Please Rebuild Entity Cache.";
                }
#endif
                //@TODO: validate header against wrong types?
                //@TODO: validate actual errors when loading
                //@TODO: validate against dependency chain being out of date
            }
    
            return null;
        }
        
        public static LiveLinkMode LiveLinkMode
        {
            get => (LiveLinkMode)EditorPrefs.GetInt("Unity.Entities.Streaming.SubScene.LiveLinkEnabled3", 0);
            set
            {
                LiveLinkConnection.GlobalDirtyLiveLink();
                EditorPrefs.SetInt("Unity.Entities.Streaming.SubScene.LiveLinkEnabled3", (int)value);
            }
        }

        static class LiveLinkMenu
        {
            const string k_Menu                 = "DOTS/LiveLink Mode";
            const string k_Disabled             = k_Menu + "/Disabled";
            const string k_LiveConvertGameView  = k_Menu + "/(Experimental) LiveConvertGameView";
            const string k_LiveConvertSceneView = k_Menu + "/(Experimental) LiveConvertSceneView";

            static bool IsEditingAnySubScenes()
            {
                for (var i = 0; i < SceneManager.sceneCount; ++i)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && scene.isSubScene)
                        return true;
                }

                return false;
            }
            
            // would be nice if we could disable an entire menu, and not have to disable each individual one..
            
            [MenuItem(k_Disabled)]
            static void Disabled() => LiveLinkMode = LiveLinkMode.Disabled;
            [MenuItem(k_Disabled, true)]
            static bool ValidateDisabled()
            {
                Menu.SetChecked(k_Disabled, LiveLinkMode == LiveLinkMode.Disabled);
                return true;
            }

            [MenuItem(k_LiveConvertGameView)]
            static void LiveConvertGameView() => LiveLinkMode = LiveLinkMode.LiveConvertGameView;  
            [MenuItem(k_LiveConvertGameView, true)]
            static bool ValidateLiveConvertGameView()
            {
                Menu.SetChecked(k_LiveConvertGameView, LiveLinkMode == LiveLinkMode.LiveConvertGameView);
                return true;
            }

            [MenuItem(k_LiveConvertSceneView)]
            static void LiveConvertSceneView() => LiveLinkMode = LiveLinkMode.LiveConvertSceneView; 
            [MenuItem(k_LiveConvertSceneView, true)]
            static bool ValidateLiveConvertSceneView()
            {
                Menu.SetChecked(k_LiveConvertSceneView, LiveLinkMode == LiveLinkMode.LiveConvertSceneView);
                return true;
            }

        }
    }
}
