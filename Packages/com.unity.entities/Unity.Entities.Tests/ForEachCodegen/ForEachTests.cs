using System;
using NUnit.Framework;
using Unity.Jobs;

namespace Unity.Entities.Tests.ForEachCodegen
{
    [TestFixture]
    public class ForEachCodegenTests : ECSTestsFixture
    {
        [InternalBufferCapacity(8)]
        public struct TestBufferElement : IBufferElementData
        {
            public static implicit operator int(TestBufferElement e) { return e.Value; }
            public static implicit operator TestBufferElement(int e) { return new TestBufferElement { Value = e }; }
            public int Value;
        }
            
        private MyTestSystem TestSystem;
        private Entity TestEntity;

        [SetUp]
        public void SetUp()
        {
#if !ENABLE_DOTS_COMPILER
            Assert.Ignore("These tests test a feature that requires ENABLE_DOTS_COMPILER to run");
#endif
            TestSystem = World.GetOrCreateSystem<MyTestSystem>();
            var myArch = m_Manager.CreateArchetype(
                ComponentType.ReadWrite<EcsTestData>(),
                ComponentType.ReadWrite<EcsTestData2>(),
                ComponentType.ReadWrite<EcsTestSharedComp>(),
                ComponentType.ReadWrite<TestBufferElement>());
            TestEntity = m_Manager.CreateEntity(myArch);
            m_Manager.SetComponentData(TestEntity, new EcsTestData() { value = 3});
            m_Manager.SetComponentData(TestEntity, new EcsTestData2() { value0 = 4});
            var buffer = m_Manager.GetBuffer<TestBufferElement>(TestEntity);
            buffer.Add(new TestBufferElement() {Value = 18});
            buffer.Add(new TestBufferElement() {Value = 19});
            
            m_Manager.SetSharedComponentData(TestEntity, new EcsTestSharedComp() { value = 5 });
        }
        
        [Test]
        public void SimplestCase()
        {
            TestSystem.SimplestCase().Complete();
            Assert.AreEqual(7, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }
        
        [Test]
        public void WithAllSharedComponent()
        {
            TestSystem.WithAllSharedComponentData().Complete();
            Assert.AreEqual(4, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }
        
        [Test]
        public void WithSharedComponentFilter()
        {
            TestSystem.WithSharedComponentFilter().Complete();
            Assert.AreEqual(4, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }
        
        [Test]
        public void AddToDynamicBuffer()
        {
            TestSystem.AddToDynamicBuffer().Complete();
            var buffer = m_Manager.GetBuffer<TestBufferElement>(TestEntity);
            Assert.AreEqual(3, buffer.Length);
            Assert.AreEqual(4, buffer[buffer.Length-1].Value);
        }

        [Test]
        public void IterateExistingDynamicBufferReadOnly()
        {
            TestSystem.IterateExistingDynamicBufferReadOnly().Complete();
            Assert.AreEqual(18+19, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }

        [Test]
        public void IterateExistingDynamicBuffer_NoModifier()
        {
            TestSystem.IterateExistingDynamicBuffer_NoModifier().Complete();
            Assert.AreEqual(18+19+20, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }


        [Test]
        public void WithNone()
        {
            TestSystem.WithNone().Complete();
            AssertNothingChanged();
        }

        [Test]
        public void WithAny_DoesntExecute_OnEntityWithoutThatComponent()
        {
            TestSystem.WithAny_DoesntExecute_OnEntityWithoutThatComponent().Complete();
            AssertNothingChanged();
        }

        [Test]
        public void ExecuteLocalFunctionThatCapturesTest()
        {
            TestSystem.ExecuteLocalFunctionThatCaptures().Complete();
            Assert.AreEqual(9, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }

        [Test]
        public void FirstCapturingSecondNotCapturingTest()
        {
            TestSystem.FirstCapturingSecondNotCapturing().Complete();
            Assert.AreEqual(9, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }

        [Test]
        public void FirstNotCapturingThenCapturingTest()
        {
            TestSystem.FirstNotCapturingThenCapturing().Complete();
            Assert.AreEqual(9, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }
        
        [Test]
        public void UseEntityIndexTest()
        {
            TestSystem.UseEntityIndex();
            Assert.AreEqual(1234, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }

        [Test]
        public void InvokeMethodWhoseLocalsLeakTest()
        {
            TestSystem.InvokeMethodWhoseLocalsLeak();
            Assert.AreEqual(9, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
        }

        class MyTestSystem : TestJobComponentSystem
        {
            public JobHandle SimplestCase()
            {
                //int multiplier = 1;
                return Entities.ForEach((ref EcsTestData e1, in EcsTestData2 e2) => { e1.value += e2.value0;}).Schedule(default);
            }
            
            public JobHandle WithNone()
            {
                int multiplier = 1;
                return Entities
                    .WithNone<EcsTestData2>()
                    .ForEach((ref EcsTestData e1) => { e1.value += multiplier;})
                    .Schedule(default);
            }
            
            public JobHandle WithAny_DoesntExecute_OnEntityWithoutThatComponent()
            {
                int multiplier = 1;
                return Entities
                    .WithAny<EcsTestData3>()
                    .ForEach((ref EcsTestData e1) => { e1.value += multiplier;})
                    .Schedule(default);
            }
            
            public JobHandle WithAllSharedComponentData()
            {
                int multiplier = 1;
                return Entities
                    .WithAll<EcsTestSharedComp>()
                    .ForEach((ref EcsTestData e1) => { e1.value += multiplier;})
                    .Schedule(default);
            }
            
            public JobHandle WithSharedComponentFilter()
            {
                int multiplier = 1;
                return Entities
                    .WithSharedComponentFilter(new EcsTestSharedComp() { value = 5 })
                    .ForEach((ref EcsTestData e1) => { e1.value += multiplier;})
                    .Schedule(default);
            }
            
            public JobHandle AddToDynamicBuffer()
            {
                return Entities
                    .ForEach((ref EcsTestData e1, ref DynamicBuffer<TestBufferElement> buf) =>
                    {
                        buf.Add(4);
                    })
                    .Schedule(default);
            }

            public JobHandle IterateExistingDynamicBufferReadOnly()
            {
                return Entities
                    .ForEach((ref EcsTestData e1, in DynamicBuffer<TestBufferElement> buf) =>
                    {
                        e1.value = SumOfBufferElements(buf);
                    })
                    .Schedule(default);
            }


            public JobHandle IterateExistingDynamicBuffer_NoModifier()
            {
                return Entities
                    .ForEach((ref EcsTestData e1, DynamicBuffer<TestBufferElement> buf) =>
                    {
                        buf.Add(20);
                        e1.value = SumOfBufferElements(buf);
                    })
                    .Schedule(default);
            }

            private static int SumOfBufferElements(DynamicBuffer<TestBufferElement> buf)
            {
                int total = 0;
                for (int i = 0; i != buf.Length; i++)
                    total += buf[i].Value;
                return total;
            }


            public JobHandle ExecuteLocalFunctionThatCaptures()
            {
                int capture_from_outer_scope = 1;
                return Entities
                    .ForEach((ref EcsTestData e1) =>
                    {
                        int capture_from_delegate_scope = 8;
                        int MyLocalFunction()
                        {
                            return capture_from_outer_scope + capture_from_delegate_scope;
                        }
                        e1.value = MyLocalFunction(); 
                    })
                    .Schedule(default);
            }

            public JobHandle FirstCapturingSecondNotCapturing()
            {
                int capturedValue = 3;
                var job1 = Entities.ForEach((ref EcsTestData e1) => e1.value = capturedValue).Schedule(default);
                return Entities.ForEach((ref EcsTestData e1) => e1.value *= 3).Schedule(job1);
            }
            
            public JobHandle FirstNotCapturingThenCapturing()
            {
                int capturedValue = 3;
                var job1 = Entities.ForEach((ref EcsTestData e1) => e1.value = 3).Schedule(default);
                return Entities.ForEach((ref EcsTestData e1) => e1.value *= capturedValue).Schedule(job1);
            }

            public void InvokeMethodWhoseLocalsLeak()
            {
                var normalDelegate = MethodWhoseLocalsLeak();
                Assert.AreEqual(8, normalDelegate());
            }

            public Func<int> MethodWhoseLocalsLeak()
            {
                int capturedValue = 3;
                Entities.ForEach((ref EcsTestData e1) => e1.value *= capturedValue).Schedule(default).Complete();
                int someOtherValue = 8;
                return () => someOtherValue;
            }
            
            public void UseEntityIndex()
            {
                Entities.ForEach((int entityInQueryIndex, ref EcsTestData etd) =>
                    {
                        etd.value = entityInQueryIndex + 1234;
                    }).Run();
            }
        }
        
        void AssertNothingChanged() => Assert.AreEqual(3, m_Manager.GetComponentData<EcsTestData>(TestEntity).value);
    }
    
    public class TestJobComponentSystem : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps) => inputDeps;
    }
}