using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Tests;

[assembly: RegisterGenericComponentType(typeof(EcsTestGeneric<int>))]
[assembly: RegisterGenericComponentType(typeof(EcsTestGeneric<float>))]
[assembly: RegisterGenericComponentType(typeof(EcsTestGenericTag<int>))]
[assembly: RegisterGenericComponentType(typeof(EcsTestGenericTag<float>))]

namespace Unity.Entities.Tests
{
    // In case we need a generic way to access the first int of the EcsTestData* structures
    interface IGetValue
    {
        int GetValue();
    }
    public struct EcsTestData : IComponentData, IGetValue
    {
        public int value;

        public EcsTestData(int inValue)
        {
            value = inValue;
        }

        public override string ToString()
        {
            return value.ToString();
        }

        public int GetValue() => value;
    }

    public struct EcsTestData2 : IComponentData, IGetValue
    {
        public int value0;
        public int value1;

        public EcsTestData2(int inValue)
        {
            value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    public struct EcsTestData3 : IComponentData, IGetValue
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestData3(int inValue)
        {
            value2 = value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    public struct EcsTestData4 : IComponentData, IGetValue
    {
        public int value0;
        public int value1;
        public int value2;
        public int value3;

        public EcsTestData4(int inValue)
        {
            value3 = value2 = value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    public struct EcsTestData5 : IComponentData, IGetValue
    {
        public int value0;
        public int value1;
        public int value2;
        public int value3;
        public int value4;

        public EcsTestData5(int inValue)
        {
            value4 = value3 = value2 = value1 = value0 = inValue;
        }

        public int GetValue() => value0;
    }

    public struct EcsTestFloatData : IComponentData
    {
        public float Value;
    }

    public struct EcsTestSharedComp : ISharedComponentData
    {
        public int value;

        public EcsTestSharedComp(int inValue)
        {
            value = inValue;
        }
    }

    public struct EcsTestSharedComp2 : ISharedComponentData
    {
        public int value0;
        public int value1;

        public EcsTestSharedComp2(int inValue)
        {
            value0 = value1 = inValue;
        }
    }

    public struct EcsTestSharedComp3 : ISharedComponentData
    {
        public int value0;
        public int value1;
        public int value2;

        public EcsTestSharedComp3(int inValue)
        {
            value0 = value1 = value2 = inValue;
        }
    }

    [MaximumChunkCapacity(475)]
    struct EcsTestSharedCompWithMaxChunkCapacity : ISharedComponentData
    {
        public int Value;

        public EcsTestSharedCompWithMaxChunkCapacity(int value)
        {
            Value = value;
        }
    }

    public struct EcsTestDataEntity : IComponentData
    {
        public int value0;
        public Entity value1;

        public EcsTestDataEntity(int inValue0, Entity inValue1)
        {
            value0 = inValue0;
            value1 = inValue1;
        }
    }

    public struct EcsTestDataBlobAssetRef : IComponentData
    {
        public BlobAssetReference<int> value;
    }

    public struct EcsTestDataBlobAssetArray : IComponentData
    {
        public BlobAssetReference<BlobArray<float>> array;
    }

    public struct EcsTestDataBlobAssetElement : IBufferElementData
    {
        public BlobAssetReference<int> blobElement;
    }

    public struct EcsTestDataBlobAssetElement2 : IBufferElementData
    {
        public BlobAssetReference<int> blobElement;
        byte pad;
        public BlobAssetReference<int> blobElement2;
    }

    public struct EcsTestSharedCompEntity : ISharedComponentData
    {
        public Entity value;

        public EcsTestSharedCompEntity(Entity inValue)
        {
            value = inValue;
        }

    }

    struct EcsState1 : ISystemStateComponentData
    {
        public int Value;

        public EcsState1(int value)
        {
            Value = value;
        }
    }

    struct EcsStateTag1 : ISystemStateComponentData
    {
    }

    [InternalBufferCapacity(8)]
    public struct EcsIntElement : IBufferElementData
    {
        public static implicit operator int(EcsIntElement e)
        {
            return e.Value;
        }

        public static implicit operator EcsIntElement(int e)
        {
            return new EcsIntElement {Value = e};
        }

        public int Value;
    }

    [InternalBufferCapacity(8)]
    public struct EcsIntElement2 : IBufferElementData
    {
        public int Value0;
        public int Value1;
    }

    [InternalBufferCapacity(8)]
    public struct EcsIntElement3 : IBufferElementData
    {
        public int Value0;
        public int Value1;
        public int Value2;
    }

    [InternalBufferCapacity(8)]
    public struct EcsIntElement4 : IBufferElementData
    {
        public int Value0;
        public int Value1;
        public int Value2;
        public int Value3;
    }

    [InternalBufferCapacity(8)]
    public struct EcsIntStateElement : ISystemStateBufferElementData
    {
        public static implicit operator int(EcsIntStateElement e)
        {
            return e.Value;
        }

        public static implicit operator EcsIntStateElement(int e)
        {
            return new EcsIntStateElement {Value = e};
        }

        public int Value;
    }

    [InternalBufferCapacity(4)]
    public struct EcsComplexEntityRefElement : IBufferElementData
    {
        public int Dummy;
        public Entity Entity;
    }

    public struct EcsTestTag : IComponentData
    {
    }

    public struct EcsTestComponentWithBool : IComponentData, IEquatable<EcsTestComponentWithBool>
    {
        public bool value;

        public override int GetHashCode()
        {
            return value ? 0x11001100 : 0x22112211;
        }

        public bool Equals(EcsTestComponentWithBool other)
        {
            return other.value == value;
        }
    }

    public struct EcsStringSharedComponent : ISharedComponentData, IEquatable<EcsStringSharedComponent>
    {
        public string Value;

        public bool Equals(EcsStringSharedComponent other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    public struct EcsTestGeneric<T> : IComponentData
        where T : struct
    {
        public T value;
    }

    public struct EcsTestGenericTag<T> : IComponentData
        where T : struct
    {
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public class EcsTestManagedDataEntity : IComponentData, IEquatable<EcsTestManagedDataEntity>
    {
        public string value0;
        public Entity value1;

        public EcsTestManagedDataEntity()
        {
        }

        public EcsTestManagedDataEntity(string inValue0, Entity inValue1)
        {
            value0 = inValue0;
            value1 = inValue1;
        }

        public bool Equals(EcsTestManagedDataEntity other)
        {
            return value0.Equals(other.value0) && value1 == other.value1;
        }

        public override int GetHashCode()
        {
            return (value0.GetHashCode() * 122949823) ^ value1.GetHashCode();
        }
    }

    public class EcsTestManagedDataEntityCollection : IComponentData, IEquatable<EcsTestManagedDataEntityCollection>
    {
        public List<string> value0;
        public List<Entity> value1;

        public EcsTestManagedDataEntityCollection()
        {
        }

        public EcsTestManagedDataEntityCollection(string[] inValue0, Entity[] inValue1)
        {
            value0 = new List<string>(inValue0);
            value1 = new List<Entity>(inValue1);
        }

        public bool Equals(EcsTestManagedDataEntityCollection other)
        {
            return value0.Equals(other.value0) && value1.Equals(other.value1);
        }

        public override int GetHashCode()
        {
            return (value0.GetHashCode() * 122949823) ^ value1.GetHashCode();
        }
    }

    public class EcsTestManagedComponent : IComponentData, IEquatable<EcsTestManagedComponent>
    {
        public string value;

        public EcsTestManagedComponent()
        {
            
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public bool Equals(EcsTestManagedComponent other)
        {
            return value == other.value;
        }
    }

    public class EcsTestManagedComponent2 : EcsTestManagedComponent, IEquatable<EcsTestManagedComponent2>
    {
        public string value2;

        public EcsTestManagedComponent2()
        {
            
        }

        public override int GetHashCode()
        {
            return (value.GetHashCode() * 7) ^ value2.GetHashCode();
        }

        public bool Equals(EcsTestManagedComponent2 other)
        {
            return value == other.value
                && value2 == other.value2;
        }
    }

    public class EcsTestManagedComponent3 : EcsTestManagedComponent2, IEquatable<EcsTestManagedComponent3>
    {
        public string value3;

        public EcsTestManagedComponent3()
        {
            
        }

        public override int GetHashCode()
        {
            return (value.GetHashCode() * 7) ^ (value2.GetHashCode() * 13) ^ value3.GetHashCode();
        }

        public bool Equals(EcsTestManagedComponent3 other)
        {
            return value == other.value
                && value2 == other.value2
                && value3 == other.value3;
        }
    }

    public class EcsTestManagedComponent4 : EcsTestManagedComponent3, IEquatable<EcsTestManagedComponent4>
    {
        public string value4;

        public EcsTestManagedComponent4()
        {
            
        }

        public override int GetHashCode()
        {
            return (value.GetHashCode() * 7) ^ (value2.GetHashCode() * 13) ^ (value3.GetHashCode() * 31) ^ value4.GetHashCode();
        }

        public bool Equals(EcsTestManagedComponent4 other)
        {
            return value == other.value
                && value2 == other.value2
                && value3 == other.value3
                && value4 == other.value4;
        }
    }
#endif
}
