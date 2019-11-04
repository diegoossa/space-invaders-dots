using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Unity.Scenes
{
    struct LiveLinkSceneMsg
    {
        public NativeArray<Hash128> LoadedScenes;
        public NativeArray<Hash128> RemovedScenes;

        public void Dispose()
        {
            LoadedScenes.Dispose();
            RemovedScenes.Dispose();
        }

        unsafe public byte[] ToMsg()
        {
            var buffer = new UnsafeAppendBuffer(0, 16, Allocator.TempJob);
            Serialize(ref buffer);
            var bytes = buffer.ToBytes();
            buffer.Dispose();
            return bytes;
        }

        unsafe public static LiveLinkSceneMsg FromMsg(byte[] buffer, Allocator allocator)
        {
            fixed (byte* ptr = buffer)
            {
                var reader = new UnsafeAppendBuffer.Reader(ptr, buffer.Length);
                LiveLinkSceneMsg msg = default;
                msg.Deserialize(ref reader, allocator);
                return msg;
            }
        }
        
        void Serialize(ref UnsafeAppendBuffer buffer)
        {
            buffer.Add(LoadedScenes);
            buffer.Add(RemovedScenes);
        }
        
        void Deserialize(ref UnsafeAppendBuffer.Reader buffer, Allocator allocator)
        {
            buffer.ReadNext(out LoadedScenes, allocator);
            buffer.ReadNext(out RemovedScenes, allocator);
        }
    }

    class LiveLinkSceneChangeTracker
    {
        private EntityQuery _LoadedScenesQuery;
        private EntityQuery _UnloadedScenesQuery;
        private NativeList<SceneReference>  m_PreviousScenes;

        public LiveLinkSceneChangeTracker(EntityManager manager)
        {
            _LoadedScenesQuery = manager.CreateEntityQuery(typeof(SceneReference));
            _UnloadedScenesQuery = manager.CreateEntityQuery(ComponentType.Exclude<SceneReference>(), ComponentType.ReadOnly<LiveLinkPatcher.LiveLinkedSceneState>());
            m_PreviousScenes = new NativeList<SceneReference>(Allocator.Persistent);
        }

        public void Dispose()
        {
            _LoadedScenesQuery.Dispose();
            _UnloadedScenesQuery.Dispose();
            m_PreviousScenes.Dispose();
        }

        public void Reset()
        {
            m_PreviousScenes.Clear();
        }
        
        public bool GetSceneMessage(out LiveLinkSceneMsg msg)
        {
            var loadedScenes = _LoadedScenesQuery.ToComponentDataArray<SceneReference>(Allocator.Persistent);
            if (loadedScenes.ArraysEqual(m_PreviousScenes.AsArray()) && _UnloadedScenesQuery.IsEmptyIgnoreFilter)
            {
                loadedScenes.Dispose();
                msg = default;
                return false;
            }
            
            msg.LoadedScenes = loadedScenes.Reinterpret<Hash128>();
            msg.RemovedScenes = _UnloadedScenesQuery.ToComponentDataArray<LiveLinkPatcher.LiveLinkedSceneState>(Allocator.TempJob).Reinterpret<Hash128>();

            m_PreviousScenes.Clear();
            m_PreviousScenes.AddRange(loadedScenes);

            return true;
        }
    }
}