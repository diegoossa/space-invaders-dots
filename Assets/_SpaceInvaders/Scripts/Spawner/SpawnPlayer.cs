using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class SpawnPlayer : MonoBehaviour
{
    public GameObject Prefab;

    void Start()
    {
        var prefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(Prefab, World.DefaultGameObjectInjectionWorld);
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        var instance = entityManager.Instantiate(prefab);
        entityManager.SetComponentData(instance, new Translation {Value = float3.zero});
    }
}