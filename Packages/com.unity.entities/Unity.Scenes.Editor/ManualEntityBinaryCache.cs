using Unity.Entities;
using Unity.Scenes;

namespace Unity.Scenes.Editor
{
    public static class ManualEntityBinaryCache
    {
        static public void NotifyEntityCacheChanged(Hash128 guid)
        {
#if !ENABLE_SUBSCENE_IMPORTER
            foreach (var world in World.AllWorlds)
            {
                var resolveSystem = world.GetExistingSystem<ResolveSceneReferenceSystem>();
                if (resolveSystem != null)
                    resolveSystem.NotifySceneContentsHasChanged(guid);
            }
#endif
            
        }
    }
}