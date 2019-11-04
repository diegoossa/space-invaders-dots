using System;
using System.IO;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    class EntityScenesPaths
    {
        public static Type SubSceneImporterType = null;

#if !ENABLE_SUBSCENE_IMPORTER
        public static string GetSceneCachePath(Hash128 sceneGUID, PathType type, string subsectionName)
        {
            if (sceneGUID == new Hash128())
                return "";

            string sceneName = sceneGUID.ToString();
            if (!String.IsNullOrEmpty(subsectionName) && type != PathType.EntitiesHeader)
                sceneName += "_" + subsectionName;

            if (type == PathType.EntitiesUnityObjectReferences)
                return "Assets/EntityCache/Resources/" + sceneName + "_objrefs.asset";
            if (type == PathType.EntitiesHeader)
                return "Assets/StreamingAssets/EntityCache/" + sceneName + "_header.entities";
            if (type == PathType.EntitiesBinary)
                return "Assets/StreamingAssets/EntityCache/" + sceneName + ".entities";
            throw new ArgumentException();
        }

        public static bool HasEntitySceneCache(Hash128 sceneGUID)
        {
            string headerPath = GetSceneCachePath(sceneGUID, EntityScenesPaths.PathType.EntitiesHeader, "");
            return File.Exists(headerPath);
        }

#endif

        public enum PathType
        {
            EntitiesUnityObjectReferences,
            EntitiesBinary,
            EntitiesHeader
        }

        public static string GetExtension(PathType pathType)
        {
            switch (pathType)
            {
                case PathType.EntitiesUnityObjectReferences: return "asset";
                case PathType.EntitiesBinary : return "entities";
                case PathType.EntitiesHeader : return "entityheader";
            }

            throw new System.ArgumentException("Unknown PathType");
        }

#if ENABLE_SUBSCENE_IMPORTER

#if UNITY_EDITOR
        public struct SceneWithBuildSettingsGUIDs
        {
            public Hash128 SceneGUID;
            public Hash128 BuildSettings;
        }

        public static unsafe Hash128 CreateBuildSettingSceneFile(Hash128 sceneGUID, Hash128 buildSettingGUID)
        {
            var guids = new SceneWithBuildSettingsGUIDs { SceneGUID = sceneGUID, BuildSettings = buildSettingGUID};
            
            Hash128 guid;
            guid.Value.x = math.hash(&guids, sizeof(SceneWithBuildSettingsGUIDs));
            guid.Value.y = math.hash(&guids, sizeof(SceneWithBuildSettingsGUIDs), 0x96a755e2);
            guid.Value.z = math.hash(&guids, sizeof(SceneWithBuildSettingsGUIDs), 0x4e936206);
            guid.Value.w = math.hash(&guids, sizeof(SceneWithBuildSettingsGUIDs), 0xac602639);

            string dir = "Assets/SceneDependencyCache";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string fileName = $"{dir}/{guid}.sceneWithBuildSettings";
            if (!File.Exists(fileName))
            {
                using(var writer = new Entities.Serialization.StreamBinaryWriter(fileName))
                {
                    writer.WriteBytes(&guids, sizeof(SceneWithBuildSettingsGUIDs));
                }
                File.WriteAllText(fileName + ".meta", 
                    $"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n");
                
                // Refresh is necessary because it appears the asset pipeline
                // can't depend on an asset on disk that has not yet been refreshed.
                AssetDatabase.Refresh();
            }
            return guid;
        }

        public static Hash128 GetSubSceneArtifactHash(Hash128 sceneGUID, Hash128 buildSettingGUID, UnityEditor.Experimental.AssetDatabaseExperimental.ImportSyncMode syncMode)
        {
            var guid = CreateBuildSettingSceneFile(sceneGUID, buildSettingGUID);
            var res = UnityEditor.Experimental.AssetDatabaseExperimental.GetArtifactHash(guid.ToString(), SubSceneImporterType, syncMode);
            return res;
        }        
        
        public static string GetLoadPathFromArtifactPaths(string[] paths, PathType type, int sectionIndex)
        {
            string prefix;
            if (type == PathType.EntitiesHeader)
                prefix = GetExtension(type);
            else
                prefix = $"{sectionIndex}.{GetExtension(type)}";

            return paths.First(p => p.EndsWith(prefix));
        }
#endif
        public static string GetLoadPath(Hash128 sceneGUID, PathType type, int sectionIndex)
        {
            var extension = GetExtension(type);
            if (type == PathType.EntitiesBinary)
                return $"{Application.streamingAssetsPath}/SubScenes/{sceneGUID}.{sectionIndex}.{extension}";
            else if (type == PathType.EntitiesHeader)
                return $"{Application.streamingAssetsPath}/SubScenes/{sceneGUID}.{extension}";
            else if (type == PathType.EntitiesUnityObjectReferences)
                return $"{Application.streamingAssetsPath}/SubScenes/{sceneGUID}.{sectionIndex}.bundle";
            else
                return "";
        }

        public static int GetSectionIndexFromPath(string path)
        {
            var components = Path.GetFileNameWithoutExtension(path).Split('.');
            if (components.Length == 1)
                return 0;
            return int.Parse(components[1]);
        }

#else
        public static string GetLoadPath(Hash128 sceneGUID, PathType type, int sectionIndex)
        {
            if (type == PathType.EntitiesUnityObjectReferences)
                return $"{sceneGUID}_{sectionIndex}_objrefs";

            var path = GetSceneCachePath(sceneGUID, type, sectionIndex.ToString());

            if (type == PathType.EntitiesBinary)
                return Application.streamingAssetsPath + "/EntityCache/" + Path.GetFileName(path);
            else if (type == PathType.EntitiesHeader)
                return Application.streamingAssetsPath + "/EntityCache/" + Path.GetFileName(path);
            else
                return path;
        }
#endif
    }
}
