using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
[RequiresEntityConversion]
public class BoundariesAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float Left;
    public float Right;
    
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new Boundaries {Left = this.Left, Right = this.Right});
    }
}