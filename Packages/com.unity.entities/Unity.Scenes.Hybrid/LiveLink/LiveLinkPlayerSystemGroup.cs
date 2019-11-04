using Unity.Entities;
using UnityEngine;

namespace Unity.Scenes
{
    
#if UNITY_EDITOR || !ENABLE_PLAYER_LIVELINK
    [DisableAutoCreation]
#endif
    [ExecuteAlways]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(SceneSystem))]
    class LiveLinkRuntimeSystemGroup : ComponentSystemGroup
    {
    }
}