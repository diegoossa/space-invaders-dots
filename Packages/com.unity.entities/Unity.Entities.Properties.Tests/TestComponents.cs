using System;
using System.Collections.Generic;
using Unity.Mathematics;


namespace Unity.Entities.Properties.Tests
{
    [System.Serializable]
    public struct TestComponent : IComponentData
    {
        public float x;
    }

    public enum MyEnum
    {
        ONE,
        TWO,
        THREE
    }

    [System.Serializable]
    public struct TestEnumComponent : IComponentData
    {
        public float x;
        public MyEnum e;
    }

    [System.Serializable]
    public struct TestComponent2 : IComponentData
    {
        public int x;
        public byte b;
    }

    [System.Serializable]
    public struct MathComponent : IComponentData
    {
        public float2 v2;
        public float3 v3;
        public float4 v4;
        public float2x2 m2;
        public float3x3 m3;
        public float4x4 m4;
    }

    [System.Serializable]
    public struct NestedComponent : IComponentData
    {
        public TestComponent test;
    }

    [System.Serializable]
    public struct BlitMe
    {
        public float x;
        public double y;
        public sbyte z;
    }

    [System.Serializable]
    public struct BlitComponent : IComponentData
    {
        public BlitMe blit;
        public float flt;
    }

    [System.Serializable]
    [InternalBufferCapacity(8)]
    public struct TestBufferElementData : IBufferElementData
    {
        public BlitMe blit;
        public float flt;
    }

    [System.Serializable]
    public struct TestSharedComponent : ISharedComponentData
    {
        public float value;

        public TestSharedComponent(float v) { value = v; }
    }
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    [System.Serializable]
    public class TestManagedComponent : IComponentData, IEquatable<TestManagedComponent>
    {
        public readonly float value;

        public TestManagedComponent()
        {
            value = 5.0f;
        }

        public bool Equals(TestManagedComponent other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return value.Equals(other.value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TestManagedComponent) obj);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
    }
#endif
}
