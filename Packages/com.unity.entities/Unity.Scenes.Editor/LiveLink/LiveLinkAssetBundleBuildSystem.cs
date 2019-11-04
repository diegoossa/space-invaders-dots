using UnityEngine.Networking.PlayerConnection;
using Hash128 = UnityEngine.Hash128;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine;

namespace Unity.Scenes
{
    [AlwaysUpdateSystem]
    [ExecuteAlways]
    [UpdateInGroup(typeof(LiveLinkEditorSystemGroup))]
    class LiveLinkAssetBundleBuildSystem : ComponentSystem
    {
        readonly Dictionary<GUID, Hash128> _UsedAssetsTargetHash = new Dictionary<GUID, Hash128>();
        public const string LiveLinkAssetBundleCache = "Library/LiveLinkAssetBundleCache/";
        public void ClearUsedAssetsTargetHash()
        {
            _UsedAssetsTargetHash.Clear();
        }

        class MsgReceiver : ScriptableObject
        {
            public LiveLinkAssetBundleBuildSystem system;
            public int LastConnectedPlayer;
            public void ReceiveBuildRequest(MessageEventArgs args)
            {
                var guid = args.Receive<GUID>();
                //Debug.Log($"ReceiveAssetBundleBuildRequest '{guid}' -> '{AssetDatabase.GUIDToAssetPath(guid.ToString())}'");

                LastConnectedPlayer = args.playerId;
                system.SendAssetBundle(args.playerId, guid);
            }

            public void RequestAssetBundleTargetHash(MessageEventArgs args)
            {
                //@TODO: should be based on connection / BuildSetting
                var buildTarget = EditorUserBuildSettings.activeBuildTarget;

                using (var assets = args.ReceiveArray<GUID>())
                {
                    var resolvedAssets = new HashSet<ResolvedAssetID>();
                    foreach(var asset in assets)
                    {
                        var targetHash = LiveLinkBuildPipeline.CalculateTargetHash(asset, buildTarget);
                        resolvedAssets.Add(new ResolvedAssetID { GUID = asset, TargetHash = targetHash });

                        LiveLinkBuildPipeline.CalculateTargetDependencies(asset, buildTarget, out ResolvedAssetID[] dependencies);
                        resolvedAssets.UnionWith(dependencies);
                    }

                    var resolved = new NativeArray<ResolvedAssetID>(resolvedAssets.Count, Allocator.Temp);
                    int j = 0;
                    foreach (var id in resolvedAssets)
                        resolved[j++] = id;

                    system.SendAssetBundleTargetHash(resolved, args.playerId);
                }
            }
        }

        private MsgReceiver _MsgReceiver;

        protected override void OnCreate()
        {
            // EditorConnection.instance.Register requires the receiver to be a UnityEngine.Object
            // @TODO: We should fix this in Unity. Its stupid.
            _MsgReceiver = MsgReceiver.CreateInstance<MsgReceiver>();
            _MsgReceiver.hideFlags = HideFlags.HideAndDontSave;
            _MsgReceiver.system = this;

            EditorConnection.instance.Register(LiveLinkMsg.RequestAssetBundleForGUID, _MsgReceiver.ReceiveBuildRequest);
            EditorConnection.instance.Register(LiveLinkMsg.RequestAssetBundleTargetHash, _MsgReceiver.RequestAssetBundleTargetHash);
        }

        protected override void OnDestroy()
        {
            EditorConnection.instance.Unregister(LiveLinkMsg.RequestAssetBundleForGUID, _MsgReceiver.ReceiveBuildRequest);
            EditorConnection.instance.Unregister(LiveLinkMsg.RequestAssetBundleTargetHash, _MsgReceiver.RequestAssetBundleTargetHash);
            _MsgReceiver.system = null;
            UnityEngine.Object.DestroyImmediate(_MsgReceiver);
        }

        static string ResolveCachePath(Unity.Entities.Hash128 targethash)
        {
            var path = "Library/LiveLinkAssetBundleCache/" + targethash;
            return path;
        }

        void SendAssetBundleTargetHash(NativeArray<ResolvedAssetID> resolvedAssets, int playerId)
        {
            EditorConnection.instance.SendArray(LiveLinkMsg.ResponseAssetBundleTargetHash, resolvedAssets, playerId);

            foreach (var asset in resolvedAssets)
                _UsedAssetsTargetHash[asset.GUID] = asset.TargetHash;
        }


        unsafe void SendAssetBundle(int playerID, GUID guid)
        {
            Hash128 targetHash;
            string path = BuildAssetBundleIfNotCached(guid, out targetHash);
            if (path == null)
                return;

            var stream = File.OpenRead(path);
            var assetBundleFileLength = stream.Length;
            var bufferSize = stream.Length + sizeof(Hash128) + sizeof(Hash128); 
            
            if (bufferSize > int.MaxValue)
            {
                Debug.LogError($"AssetBundle {guid} can't be sent to the player because it exceeds the 2GB size limit");
                return;
            }
                
            var bundleAndHeader = new byte[bufferSize];
            fixed (byte* data = bundleAndHeader)
            {
                var writer = new UnsafeAppendBuffer(data, bundleAndHeader.Length);
                writer.Add(guid);
                writer.Add(targetHash);
                stream.Read(bundleAndHeader, writer.Size, (int)assetBundleFileLength);
            }

            stream.Close();
            stream.Dispose();

            LiveLinkMsg.LogInfo($"Sending assetbundle: '{AssetDatabase.GUIDToAssetPath(guid.ToString())}' size: {assetBundleFileLength}");

            EditorConnection.instance.Send(LiveLinkMsg.ResponseAssetBundleForGUID, bundleAndHeader, playerID);
        }

        //@TODO: The asset pipeline should be building & cache the asset bundle
        public string BuildAssetBundleIfNotCached(GUID guid, out Hash128 targetHash)
        {
            //@TODO Get build target from player requesting it...
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            targetHash = LiveLinkBuildPipeline.CalculateTargetHash(guid, buildTarget);

            // TODO: Move caching into LiveLinkBuildPipeline
            var cachePath = ResolveCachePath(targetHash);

            if (File.Exists(cachePath))
                return cachePath;

            if (!Directory.Exists(cachePath))
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));

            // Debug.Log($"Building {guid} fresh");
            // Patching only works if the ObjectManifest comes from the same GUID every time.
            // So we can't delete this file. Optimally we would control the build pipeline
            // to make it always be at a specific local identifier in file
            var manifestPath = "Assets/TempObjectManifest.asset";
            var manifest = AssetDatabase.LoadAssetAtPath<AssetObjectManifest>(manifestPath);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<AssetObjectManifest>();
                AssetObjectManifestBuilder.BuildManifest(guid, manifest);
                AssetDatabase.CreateAsset(manifest, manifestPath);
            }
            else
            {
                AssetObjectManifestBuilder.BuildManifest(guid, manifest);
                // SaveAssets to ensure it is updated on disk correctly after modifications
                // TODO: Need to expose the SaveAsset api
                AssetDatabase.SaveAssets();
            }

            var didSucceed = LiveLinkBuildPipeline.BuildAssetBundle(manifestPath, guid, $"{cachePath}", EditorUserBuildSettings.activeBuildTarget);

            if (!didSucceed)
            {
                Debug.LogError($"Failed to build asset bundle: '{guid}'");
                return null;
            }

            return cachePath;
        }


        void DetectChangedAssets()
        {
            if (_UsedAssetsTargetHash.Count == 0)
                return;
            
            var changedAssets = new NativeList<ResolvedAssetID>(Allocator.Temp);
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            foreach (var asset in _UsedAssetsTargetHash)
            {
                //@TODO: Artifact hash API should give error message when used on V1 pipeline (currently does not).

                var targetHash = LiveLinkBuildPipeline.CalculateTargetHash(asset.Key, buildTarget);

                if (asset.Value != targetHash)
                {
                    var path = AssetDatabase.GUIDToAssetPath(asset.Key.ToString());
                    LiveLinkMsg.LogInfo("Detected asset change: " + path);
                    changedAssets.Add(new ResolvedAssetID { GUID = asset.Key, TargetHash = targetHash });
                }
            }

            if (changedAssets.Length != 0)
                SendAssetBundleTargetHash(changedAssets, _MsgReceiver.LastConnectedPlayer);
        }


        protected override void OnUpdate()
        {
            DetectChangedAssets();
        }
    }
}