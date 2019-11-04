using NUnit.Framework;
using Unity.Entities.CodeGen.Tests.LambdaJobs.Infrastructure;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities.CodeGen.Tests
{
    [TestFixture]
    public class SingleJob : IntegrationTest
    {
        [Test]
        public void SingleJobTest() => RunTest<System>();
        
        class System : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                var myCapturedFloats = new NativeArray<float>();

                return Job
                    .WithNativeDisableUnsafePtrRestriction(myCapturedFloats)
                    .WithCode(() =>
                    {
                        for (int i = 0; i != myCapturedFloats.Length; i++)
                            myCapturedFloats[i] *= 2;

                    }).Schedule(inputDeps);
            }
        }
    }
}