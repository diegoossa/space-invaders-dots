using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Serializable]
    public struct EnemyLine
    {
        public GameObject Prefab;
        public int Count;
    }

    public EnemyLine[] EnemyLines;
    public float3 StartPosition;
    public int Columns = 10;
    public int Rows = 5;
    public float2 Separation;

    private void Start()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        //var parent = entityManager.CreateEntity(typeof(EnemyHorde), typeof(Translation), typeof(Parent));


        Entity[] enemyPrefabs = new Entity[EnemyLines.Length];

        for (int i = 0; i < EnemyLines.Length; i++)
        {
            enemyPrefabs[i] = GameObjectConversionUtility.ConvertGameObjectHierarchy(EnemyLines[i].Prefab, World.DefaultGameObjectInjectionWorld);
        }

        var lineCounter = 0;
        var prefabCounter = 0;
                                                                                                                                                                                                                                                                            
        for (int x = 0; x < Rows; x++)
        {
            for (int y = 0; y < Columns; y++)
            {
                var instance = entityManager.Instantiate(enemyPrefabs[prefabCounter]);
                var position = StartPosition + new float3(y * Separation.x, -x * Separation.y, 0) ;
                entityManager.SetComponentData(instance, new Translation {Value = position});
            }
            
            lineCounter++;
            if (lineCounter >= EnemyLines[prefabCounter].Count)
            {
                prefabCounter++;
                lineCounter = 0;
            }
        }
    }
}