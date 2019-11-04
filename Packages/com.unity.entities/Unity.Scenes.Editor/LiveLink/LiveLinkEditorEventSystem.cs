using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.Networking.PlayerConnection;
using UnityEditor.SceneManagement;
using System.Linq;
using Unity.Build;

namespace Unity.Scenes.Editor
{
    [ExecuteAlways]
    [UpdateInGroup(typeof(LiveLinkEditorSystemGroup))]
    class LiveLinkEditorEventSystem: ComponentSystem
    {
        [MenuItem("DOTS/LiveLink Player/Reset Game")]
        static void ResetMenu()
        {
            EditorConnection.instance.Send(LiveLinkMsg.ResetGame, new byte[0]);
        }

        [MenuItem("DOTS/LiveLink Player/Clear LiveLinkCache")]
        static void ClearLiveLinkCache()
        {
            FileUtil.DeleteFileOrDirectory("Builds");
            FileUtil.DeleteFileOrDirectory(LiveLinkAssetBundleBuildSystem.LiveLinkAssetBundleCache);
        }

        protected override void OnUpdate()
        {
        }

        protected override void OnCreate()
        {
            // After domain reload we need to reconnect all data to the player.
            // Optimally we would keep all state alive across domain reload...
            EditorConnection.instance.Send(LiveLinkMsg.ResetGame, new byte[0]);
        }


        protected override void OnDestroy()
        {
        }
    }
}