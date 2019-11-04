using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities
{
    static partial class EntityDiffer
    {
        [BurstCompile]
        struct BuildEntityGuidToEntity : IJob
        {
            [ReadOnly] public NativeArray<EntityInChunkWithGuid> SortedEntitiesWithGuid;
            [WriteOnly] public NativeList<EntityGuid> Duplicates;

            public void Execute()
            {
                if (SortedEntitiesWithGuid.Length == 0)
                {
                    return;
                }

                var previous = SortedEntitiesWithGuid[0].EntityGuid;
                var previousWasDuplicate = false;
                
                for (var i = 1; i < SortedEntitiesWithGuid.Length; i++)
                {
                    var entityGuid = SortedEntitiesWithGuid[i].EntityGuid;
                    
                    if (entityGuid == previous)
                    {
                        if (!previousWasDuplicate)
                        {
                            Duplicates.Add(entityGuid);
                            previousWasDuplicate = true;
                        }
                    }
                    else
                    {
                        previousWasDuplicate = false;
                    }
                    
                    previous = entityGuid;
                }
            }
        }
        
        static NativeList<EntityGuid> GetDuplicateEntityGuids(
            NativeArray<EntityInChunkWithGuid> sortedEntitiesWithGuid,
            Allocator allocator, 
            out JobHandle jobHandle, 
            JobHandle dependsOn = default)
        {
            var duplicates = new NativeList<EntityGuid>(1, allocator);

            jobHandle = new BuildEntityGuidToEntity
            {
                SortedEntitiesWithGuid = sortedEntitiesWithGuid,
                Duplicates = duplicates
            }.Schedule(dependsOn);

            return duplicates;
        }
    }
}