using System;
using System.IO;
using Unity.Entities;
using Unity.Entities.Conversion;
using UnityEngine;
using Component = UnityEngine.Component;
using UnityObject = UnityEngine.Object;

//@TODO
//namespace Unity.Entities
//{
[DisableAutoCreation]
[WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)]
public class GameObjectDeclareReferencedObjectsGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)]
public class GameObjectBeforeConversionGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)]
public class GameObjectConversionGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)]
public class GameObjectAfterConversionGroup : ComponentSystemGroup { }

[DisableAutoCreation]
[WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)]
public class GameObjectExportGroup : ComponentSystemGroup { }

/// <summary>
/// Derive from this class to create a system that can convert GameObjects and assets into Entities.
/// Use one of the GameObject*Group system groups with `[UpdateInGroup]` to select a particular phase of conversion
/// for the system (default if left unspecified is GameObjectConversionGroup).
/// </summary>
[WorldSystemFilter(WorldSystemFilterFlags.GameObjectConversion)]
public abstract partial class GameObjectConversionSystem : ComponentSystem
{
    GameObjectConversionMappingSystem m_MappingSystem;

    public EntityManager DstEntityManager => m_MappingSystem.DstEntityManager;

    protected override void OnCreate()
    {
        base.OnCreate();

        m_MappingSystem = World.GetExistingSystem<GameObjectConversionMappingSystem>();
    }

    // ** DISCOVERY **

    /// <summary>
    /// DeclareReferencedPrefab includes the referenced Prefab in the conversion process.
    /// Once it has been declared, you can use GetPrimaryEntity to find the Entity for the Prefab.
    /// If the object is a Prefab, all Entities in it will be made part of a LinkedEntityGroup, thus Instantiate will clone the whole group.
    /// All Entities in the Prefab will also be tagged with the Prefab component thus will not be picked up by an EntityQuery by default.
    /// </summary>
    public void DeclareReferencedPrefab(GameObject prefab)
        => m_MappingSystem.DeclareReferencedPrefab(prefab);

    /// <summary>
    /// DeclareReferencedAsset includes the referenced asset in the conversion process.
    /// Once it has been declared, you can use GetPrimaryEntity to find the Entity for the asset.
    /// This Entity will also be tagged with the Asset component.
    /// </summary>
    public void DeclareReferencedAsset(UnityObject asset)
        => m_MappingSystem.DeclareReferencedAsset(asset);

    /// <summary>
    /// Adds a LinkedEntityGroup to the primary Entity of this GameObject for all Entities that are created from this GameObject and its descendants.
    /// As a result, EntityManager.Instantiate and EntityManager.SetEnabled will work on those Entities as a group.
    /// </summary>
    public void DeclareLinkedEntityGroup(GameObject gameObject)
        => m_MappingSystem.DeclareLinkedEntityGroup(gameObject);

    public void DeclareDependency(GameObject target, GameObject dependsOn) =>
        m_MappingSystem.DeclareDependency(target, dependsOn);

    public void DeclareDependency(Component target, Component dependsOn)
    {
        if (target != null && dependsOn != null)
            m_MappingSystem.DeclareDependency(target.gameObject, dependsOn.gameObject);
    }

    // ** CONVERSION **

    /// <summary>Returns true if the `uobject` is included in the set of converted objects.</summary>
    public bool HasPrimaryEntity(UnityObject uobject) =>
        m_MappingSystem.HasPrimaryEntity(uobject);
    /// <summary>Returns true if the GameObject owning `component` is included in the set of converted objects.</summary>
    public bool HasPrimaryEntity(Component component) =>
        m_MappingSystem.HasPrimaryEntity(component != null ? component.gameObject : null);
    public Entity TryGetPrimaryEntity(UnityObject uobject) =>
        m_MappingSystem.TryGetPrimaryEntity(uobject);
    public Entity TryGetPrimaryEntity(Component component) =>
        m_MappingSystem.TryGetPrimaryEntity(component != null ? component.gameObject : null);
    public Entity GetPrimaryEntity(UnityObject uobject) =>
        m_MappingSystem.GetPrimaryEntity(uobject);
    public Entity GetPrimaryEntity(Component component) =>
        m_MappingSystem.GetPrimaryEntity(component != null ? component.gameObject : null);

    public Entity CreateAdditionalEntity(UnityObject uobject) =>
        m_MappingSystem.CreateAdditionalEntity(uobject);
    public Entity CreateAdditionalEntity(Component component) =>
        m_MappingSystem.CreateAdditionalEntity(component != null ? component.gameObject : null);

    public MultiListEnumerator<Entity> GetEntities(UnityObject uobject) =>
        m_MappingSystem.GetEntities(uobject);
    public MultiListEnumerator<Entity> GetEntities(Component component) =>
        m_MappingSystem.GetEntities(component != null ? component.gameObject : null);

    #if UNITY_EDITOR
    public T GetBuildSettingsComponent<T>() where T : Unity.Build.IBuildSettingsComponent
    {
        return m_MappingSystem.GetBuildSettingsComponent<T>();
    }
    #endif

    
    // ** EXPORT **

    public Guid GetGuidForAssetExport(UnityObject asset)
        => m_MappingSystem.GetGuidForAssetExport(asset);
    public Stream TryCreateAssetExportWriter(UnityObject asset)
        => m_MappingSystem.TryCreateAssetExportWriter(asset);

    // ** LIVE LINK **

    /// <summary>
    /// Configures rendering data for picking in the editor.
    /// </summary>
    /// <param name="entity">The entity to which we apply the configuration</param>
    /// <param name="pickableObject">The game object that should be picked when clicking on an entity</param>
    /// <param name="hasGameObjectBasedRenderingRepresentation">If there is a game object based rendering representation, like MeshRenderer this should be true. If the only way to render the object is through entities it should be false</param>
    public void ConfigureEditorRenderData(Entity entity, GameObject pickableObject, bool hasGameObjectBasedRenderingRepresentation)
        => m_MappingSystem.ConfigureEditorRenderData(entity, pickableObject, hasGameObjectBasedRenderingRepresentation);
}
//}
