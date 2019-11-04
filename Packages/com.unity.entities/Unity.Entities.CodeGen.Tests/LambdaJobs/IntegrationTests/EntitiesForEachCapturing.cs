using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.CodeGen.Tests.LambdaJobs.Infrastructure;
using Unity.Jobs;

namespace Unity.Entities.CodeGen.Tests
{
    [TestFixture]
    public class EntitiesForEachCapturing : IntegrationTest
    {
        [Test]
        public void EntitiesForEachCapturingTest() => RunTest<System>();
        
        class System : JobComponentSystem
        {
            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                var myCapturedFloats = new NativeArray<float>();

                return Entities
                    .WithBurst(enabled: true, FloatMode.Deterministic, FloatPrecision.High,
                        synchronousCompilation: true)
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                    .WithChangeFilter<Translation>()
                    .WithNone<Boid>()
                    .WithAll<Velocity>()
                    .WithReadOnly(myCapturedFloats)
                    .WithDeallocateOnJobCompletion(myCapturedFloats)
                    .WithNativeDisableContainerSafetyRestriction(myCapturedFloats)
                    .WithNativeDisableUnsafePtrRestriction(myCapturedFloats)
                    .ForEach(
                        (ref Translation translation, in Acceleration acceleration, int entityInQueryIndex,
                            Entity myEntity,
                            DynamicBuffer<MyBufferInt> myBufferInts) =>
                        {
                            translation.Value += (myCapturedFloats[2] + acceleration.Value + entityInQueryIndex +
                                                  myEntity.Version + myBufferInts[2].Value);
                        })
                    .Schedule(inputDeps);
            }
        }
    }
}