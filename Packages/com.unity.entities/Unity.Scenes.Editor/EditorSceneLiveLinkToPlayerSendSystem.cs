using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor
{
    [ExecuteAlways]
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(LiveLinkEditorSystemGroup))]
    class EditorSceneLiveLinkToPlayerSendSystem : ComponentSystem
    {
        //@TODO: Multi-world connection support...
        Dictionary<int, LiveLinkConnection> _Connections = new Dictionary<int, LiveLinkConnection>();
        MsgReceiver                         _MsgReceiver;
        
        // Temp data cached to reduce gc allocations
        List<LiveLinkChangeSet>             _ChangeSets = new List<LiveLinkChangeSet>();
        NativeList<Hash128>                 _UnloadScenes = new NativeList<Hash128>(Allocator.Persistent);
        NativeList<Hash128>                 _LoadScenes = new NativeList<Hash128>(Allocator.Persistent);

        class MsgReceiver : ScriptableObject
        {
            public EditorSceneLiveLinkToPlayerSendSystem system;
            
            unsafe public void SetLoadedScenes(MessageEventArgs args)
            {
                LiveLinkMsg.LogInfo("SetLoadedScenes");

                if (!system._Connections.TryGetValue(args.playerId, out var connection))
                {
                    Debug.LogError("SetLoadedScenes was sent but the connection has not been created");
                    return;
                }

                var msg = LiveLinkSceneMsg.FromMsg(args.data, Allocator.TempJob);
                connection.ApplyLiveLinkSceneMsg(msg);
                msg.Dispose();
            }
            
            public void ConnectLiveLink(MessageEventArgs args)
            {
                LiveLinkMsg.LogInfo("ConnectLiveLink");

                int player = args.playerId;
                var buildSettings = args.Receive<Hash128>();

                //@TODO: Implement this properly
                //system.World.GetExistingSystem<EditorSubSceneLiveLinkSystem>().CleanupAllScenes();
                
                //@TODO: How does this work with multiple connections?
                system.World.GetExistingSystem<LiveLinkAssetBundleBuildSystem>().ClearUsedAssetsTargetHash();

                if (system._Connections.TryGetValue(player, out var connection))
                    connection.Dispose();
                
                system._Connections[player] = new LiveLinkConnection(buildSettings);

                EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
            }

            public void OnPlayerConnected(int playerID)
            {
                LiveLinkMsg.LogInfo("OnPlayerConnected " + playerID);
            }
            
            public void OnPlayerDisconnected(int playerID)
            {
                LiveLinkMsg.LogInfo("OnPlayerDisconnected" + playerID);
                
                if (system._Connections.TryGetValue(playerID, out var connection))
                {
                    connection.Dispose();
                    system._Connections.Remove(playerID);
                }
            }
        }
        
        static void SendChangeSet(LiveLinkChangeSet entityChangeSet, int playerID)
        {
            var buffer = entityChangeSet.Serialize();
            EditorConnection.instance.Send(LiveLinkMsg.ReceiveEntityChangeSet, buffer, playerID);
            LiveLinkMsg.LogInfo("Sent patch" + buffer.Length);
        }
        
        static void SendUnloadScenes(NativeArray<Hash128> unloadScenes, int playerID)
        {
            EditorConnection.instance.SendArray(LiveLinkMsg.UnloadScenes, unloadScenes, playerID);
        }

        static void SendLoadScenes(NativeArray<Hash128> loadScenes, int playerID)
        {
            EditorConnection.instance.SendArray(LiveLinkMsg.LoadScenes, loadScenes, playerID);
        }
        
        protected override void OnUpdate()
        {
            foreach (var c in _Connections)
            {
                try
                {
                    var connection = c.Value;
                    connection.Update(_ChangeSets, _LoadScenes, _UnloadScenes, LiveLinkMode.LiveConvertGameView);

                    // Load scenes that are not being edited live
                    SendLoadScenes(_LoadScenes.AsArray(), c.Key);
                    // Unload scenes that are no longer being edited / need to be reloaded etc
                    SendUnloadScenes(_UnloadScenes.AsArray(), c.Key);
                    
                    // Apply changes to scenes that are being edited
                    foreach (var change in _ChangeSets)
                    {
                        SendChangeSet(change, c.Key);
                        change.Dispose();
                    }
                }
                finally
                {
                    _ChangeSets.Clear();
                    _UnloadScenes.Clear();
                    _LoadScenes.Clear();
                }
            }
        }

        protected override void OnCreate()
        {
            _MsgReceiver = MsgReceiver.CreateInstance<MsgReceiver>();
            _MsgReceiver.hideFlags = HideFlags.HideAndDontSave;
            _MsgReceiver.system = this;

            EditorConnection.instance.Register(LiveLinkMsg.ConnectLiveLink, _MsgReceiver.ConnectLiveLink);
            EditorConnection.instance.Register(LiveLinkMsg.SetLoadedScenes, _MsgReceiver.SetLoadedScenes);
            EditorConnection.instance.RegisterConnection(_MsgReceiver.OnPlayerConnected);
            EditorConnection.instance.RegisterDisconnection(_MsgReceiver.OnPlayerDisconnected);

            // After domain reload we need to reconnect all data to the player.
            // Optimally we would keep all state alive across domain reload...
            EditorConnection.instance.Send(LiveLinkMsg.ResetGame, new byte[0]);
        }

        protected override void OnDestroy()
        {
            EditorConnection.instance.Unregister(LiveLinkMsg.ConnectLiveLink, _MsgReceiver.ConnectLiveLink);
            EditorConnection.instance.UnregisterConnection(_MsgReceiver.OnPlayerConnected);

            foreach (var connection in _Connections)
                connection.Value.Dispose();
            _Connections.Clear();

            _MsgReceiver.system = null;
            UnityEngine.Object.DestroyImmediate(_MsgReceiver);

            _UnloadScenes.Dispose();
            _LoadScenes.Dispose();
        }

    }
}