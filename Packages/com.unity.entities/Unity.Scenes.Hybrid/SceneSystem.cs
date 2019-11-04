using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    [ExecuteAlways]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class SceneSystem : ComponentSystem
    {
        public struct LoadParameters
        {
            private bool _AutoLoadInv;

            public bool AutoLoad
            {
                get { return !_AutoLoadInv; }
                set { _AutoLoadInv = !value; }
            }

            //public Entity   ParentScene;
        }

        public Hash128 BuildSettingsGUID { get; set; }

        public Entity LoadSceneAsync(Hash128 sceneGUID, LoadParameters parameters = default)
        {
            var sceneEntity = Entity.Null;
            Entities.ForEach((Entity entity, ref SceneReference scene) =>
            {
                if (scene.SceneGUID == sceneGUID)
                    sceneEntity = entity;
            });

            if (sceneEntity != Entity.Null)
            {
                if (parameters.AutoLoad)
                {
                    EntityManager.AddComponentData(sceneEntity, new RequestSceneLoaded());

                    if (EntityManager.HasComponent<ResolvedSectionEntity>(sceneEntity))
                    {
                        foreach(var s in EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity))
                            EntityManager.AddComponentData(s.SectionEntity, new RequestSceneLoaded());
                    }
                }
                

                return sceneEntity;
            }
            else
            {
                sceneEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(sceneEntity, new SceneReference {SceneGUID = sceneGUID});

                if (parameters.AutoLoad)
                    EntityManager.AddComponentData(sceneEntity, new RequestSceneLoaded());
                
                return sceneEntity;
            }
        }
        

        [Flags]
        public enum UnloadParameters
        {
            Default = 0,
            DestroySectionProxyEntities = 1 << 1,
            DestroySceneProxyEntity = 1 << 2,
            DontRemoveRequestSceneLoaded = 1 << 3
        }

        public void UnloadScene(Entity sceneEntity, UnloadParameters unloadParams = UnloadParameters.Default)
        {
            var streamingSystem = World.GetExistingSystem<SceneSectionStreamingSystem>();

            bool removeRequest = (unloadParams & UnloadParameters.DontRemoveRequestSceneLoaded) == 0;
            bool destroySceneProxyEntity = (unloadParams & UnloadParameters.DestroySceneProxyEntity) != 0;
            bool destroySectionProxyEntities = (unloadParams & UnloadParameters.DestroySectionProxyEntities) != 0;

            if (destroySceneProxyEntity && !destroySectionProxyEntities)
                throw new ArgumentException("When unloading a scene it's not possible to destroy the scene entity without also destroying the section entities. Please also add the UnloadParameters.DestroySectionProxyEntities flag");

            if (EntityManager.HasComponent<ResolvedSectionEntity>(sceneEntity))
            {
                using (var sections = EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity).ToNativeArray(Allocator.Temp))
                {
                    foreach (var section in sections)
                    {
                        //@TODO: Should this really be in SubSceneStreamingSystem?
                        streamingSystem.UnloadSectionImmediate(section.SectionEntity);

                        if (destroySectionProxyEntities)
                            EntityManager.DestroyEntity(section.SectionEntity);
                        else if (removeRequest)
                            EntityManager.RemoveComponent<RequestSceneLoaded>(section.SectionEntity);
                    }
                }
            }

            if (destroySceneProxyEntity)
            {
                EntityManager.RemoveComponent<ResolvedSectionEntity>(sceneEntity);
                EntityManager.DestroyEntity(sceneEntity);
            }
            else
            {
                if (destroySectionProxyEntities)
                {
                    EntityManager.RemoveComponent<ResolvedSectionEntity>(sceneEntity);
#if ENABLE_SUBSCENE_IMPORTER
                    EntityManager.RemoveComponent<ResolvedSceneHash>(sceneEntity);
#endif
                }

                if (removeRequest)
                    EntityManager.RemoveComponent<RequestSceneLoaded>(sceneEntity);
            }
        }

        public void UnloadScene(Hash128 sceneGUID, UnloadParameters unloadParams = UnloadParameters.Default)
        {
            var sceneEntity = GetSceneEntity(sceneGUID);
            if (sceneEntity != Entity.Null)
                UnloadScene(sceneEntity, unloadParams);
        }

        public Entity GetSceneEntity(Hash128 sceneGUID)
        {
            Entity sceneEntity = Entity.Null;
            Entities.ForEach((Entity entity, ref SceneReference scene) =>
            {
                if (scene.SceneGUID == sceneGUID)
                    sceneEntity = entity;
            });

            return sceneEntity;
        }

        protected override void OnUpdate()
        {
            var streamingSystem = World.GetExistingSystem<SceneSectionStreamingSystem>();

            // Cleanup all Scenes that were destroyed explicitly 
            Entities.WithNone<SceneReference>().ForEach((Entity sceneEntity, DynamicBuffer<ResolvedSectionEntity> sections) =>
            {
                foreach (var section in sections.ToNativeArray(Allocator.Temp))
                {
                    streamingSystem.UnloadSectionImmediate(section.SectionEntity);
                    EntityManager.DestroyEntity(section.SectionEntity);
                }

                EntityManager.RemoveComponent<ResolvedSectionEntity>(sceneEntity);
            });    
        }        
    }
}