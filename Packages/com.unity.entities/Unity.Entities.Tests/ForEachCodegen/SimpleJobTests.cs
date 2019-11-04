
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities.Tests.ForEachCodegen
{
    [TestFixture]
    public class SimpleJobTests : ECSTestsFixture
    {
        private SimpleJobSystem TestSystem;

        [SetUp]
        public void SetUp()
        {
#if !ENABLE_DOTS_COMPILER
            Assert.Ignore("These tests test a feature that requires ENABLE_DOTS_COMPILER to run");
#endif
            TestSystem = World.GetOrCreateSystem<SimpleJobSystem>();
        }
        
        
        [Test]
        public void SimpleJob()
        {
            using (var myArray = new NativeArray<int>(10, Allocator.Persistent))
            {
                TestSystem.TestMe(myArray).Complete();
                Assert.AreEqual(12, myArray[5]);
            }
        }

        public class SimpleJobSystem : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps) => default;

            public JobHandle TestMe(NativeArray<int> myArray)
            {
                int capturedValue = 12;
                return Job.WithCode(() =>
                {
                    var nativeArray = myArray;
                    nativeArray[5] = capturedValue;
                }).Schedule(default);
            }
        }
    }
}
        