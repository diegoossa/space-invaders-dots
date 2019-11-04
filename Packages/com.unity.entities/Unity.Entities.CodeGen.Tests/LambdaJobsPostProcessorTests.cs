using System;
using NUnit.Framework;
using Unity.Entities.CodeGen.Tests.LambdaJobs.Infrastructure;
using Unity.Collections;

namespace Unity.Entities.CodeGen.Tests
{
    [TestFixture]
    public class LambdaJobsPostProcessorTests : PostProcessorTestBase
    {
        [Test]
        public void LambdaTakingUnsupportedArgumentTest()
        {
            AssertProducesError(typeof(LambdaTakingUnsupportedArgument), nameof(UserError.DC0005));
        }

        class LambdaTakingUnsupportedArgument : TestJobComponentSystem
        {
            void Test()
            {
                Entities.ForEach(
                        (string whyAreYouPuttingAStringHereMakesNoSense) => { Console.WriteLine("Hello"); })
                    .Schedule(default);
            }
        }


        [Test]
        public void WithConflictingNameTest()
        {
            AssertProducesError(typeof(WithConflictingName), nameof(UserError.DC0003));
        }

        class WithConflictingName : TestJobComponentSystem
        {
            struct VeryCommonName
            {
            }

            void Test()
            {
                Entities
                    .WithName("VeryCommonName")
                    .ForEach(
                        (ref Translation t) => { })
                    .Schedule(default);
            }
        }

        [Test]
        public void ConflictingWithNoneTest()
        {
            AssertProducesError(typeof(ConflictingWithNone), nameof(UserError.DC0015), "Translation");
        }

        class ConflictingWithNone : TestJobComponentSystem
        {
            void Test()
            {
                Entities
                    .WithNone<Translation>()
                    .ForEach((in Translation translation) => { })
                    .Schedule(default);
            }
        }


        [Test]
        public void ConflictingWithNoneAndWithAnyTest()
        {
            AssertProducesError(typeof(ConflictingWithNoneAndWithAny), nameof(UserError.DC0016), "Translation");
        }

        class ConflictingWithNoneAndWithAny : TestJobComponentSystem
        {
            void Test()
            {
                Entities
                    .WithNone<Translation>()
                    .WithAny<Translation, Velocity>()
                    .ForEach((in Boid translation) => { })
                    .Schedule(default);
            }
        }

        [Test]
        public void WithReadOnly_IllegalArgument_Test()
        {
            AssertProducesError(typeof(WithReadOnly_IllegalArgument), nameof(UserError.DC0012));
        }


        class WithReadOnly_IllegalArgument : TestJobComponentSystem
        {
            void Test()
            {
                Entities
                    .WithReadOnly("stringLiteral")
                    .ForEach((in Boid translation) => { })
                    .Schedule(default);
            }
        }

        [Test]
        public void WithReadOnly_NonCapturedVariable_Test()
        {
            AssertProducesError(typeof(WithReadOnly_NonCapturedVariable), nameof(UserError.DC0012));
        }

        class WithReadOnly_NonCapturedVariable : TestJobComponentSystem
        {
            void Test()
            {
                var myNativeArray = new NativeArray<float>();

                Entities
                    .WithReadOnly(myNativeArray)
                    .ForEach((in Boid translation) => { })
                    .Schedule(default);
            }
        }

        [Test]
        public void WithUnsupportedParameterTest()
        {
            AssertProducesError(typeof(WithUnsupportedParameter), nameof(UserError.DC0005));
        }

        class WithUnsupportedParameter : TestJobComponentSystem
        {
            void Test()
            {
                Entities
                    .ForEach((in Boid translation, string whoKnowsWhatThisMeans) => { })
                    .Schedule(default);
            }
        }

        [Test]
        public void WithCapturedReferenceTypeTest()
        {
            AssertProducesError(typeof(WithCapturedReferenceType), nameof(UserError.DC0004));
        }

        class WithCapturedReferenceType : TestJobComponentSystem
        {
            class CapturedClass
            {
                public float value;
            }

            void Test()
            {
                var capturedClass = new CapturedClass() {value = 3.0f};
                Entities
                    .ForEach((ref Translation t) => { t.Value = capturedClass.value; })
                    .Schedule(default);
            }
        }

        [Test]
        public void CaptureFieldInLocalCapturingLambdaTest()
        {
            AssertProducesError(typeof(CaptureFieldInLocalCapturingLambda), nameof(UserError.DC0001), "myfield");
        }

        class CaptureFieldInLocalCapturingLambda : TestJobComponentSystem
        {
            private int myfield = 123;

            void Test()
            {
                int also_capture_local = 1;
                Entities
                    .ForEach((ref Translation t) => { t.Value = myfield + also_capture_local; })
                    .Schedule(default);
            }
        }

        [Test]
        public void CaptureFieldInNonLocalCapturingLambdaTest()
        {
            AssertProducesError(typeof(CaptureFieldInNonLocalCapturingLambda), nameof(UserError.DC0001), "myfield");
        }

        class CaptureFieldInNonLocalCapturingLambda : TestJobComponentSystem
        {
            private int myfield = 123;

            void Test()
            {
                Entities
                    .ForEach((ref Translation t) => { t.Value = myfield; })
                    .Schedule(default);
            }
        }


        [Test]
        public void InvokeInstanceMethodInCapturingLambdaTest()
        {
            AssertProducesError(typeof(InvokeInstanceMethodInCapturingLambda), nameof(UserError.DC0002));
        }

        class InvokeInstanceMethodInCapturingLambda : TestJobComponentSystem
        {
            public object GetSomething(int i) => default;

            void Test()
            {
                int also_capture_local = 1;
                Entities
                    .ForEach((ref Translation t) => { GetSomething(also_capture_local); })
                    .Schedule(default);
            }
        }

        [Test]
        public void InvokeInstanceMethodInNonCapturingLambdaTest()
        {
            AssertProducesError(typeof(InvokeInstanceMethodInNonCapturingLambda), nameof(UserError.DC0002));
        }

        class InvokeInstanceMethodInNonCapturingLambda : TestJobComponentSystem
        {
            public object GetSomething(int i) => default;

            void Test()
            {
                Entities
                    .ForEach((ref Translation t) => { GetSomething(3); })
                    .Schedule(default);
            }
        }



        [Test]
        public void LocalFunctionThatWritesBackToCapturedLocalTest()
        {
            AssertProducesError(typeof(LocalFunctionThatWritesBackToCapturedLocal), nameof(UserError.DC0013));
        }

        class LocalFunctionThatWritesBackToCapturedLocal : TestJobComponentSystem
        {
            void Test()
            {
                int capture_me = 123;
                Entities
                    .ForEach((ref Translation t) =>
                    {
                        void MyLocalFunction()
                        {
                            capture_me++;
                        }

                        MyLocalFunction();
                    }).Schedule(default);
            }
        }

        [Test]
        public void LambdaThatWritesBackToCapturedLocalTest()
        {
            AssertProducesError(typeof(LambdaThatWritesBackToCapturedLocal), nameof(UserError.DC0013));
        }

        class LambdaThatWritesBackToCapturedLocal : TestJobComponentSystem
        {
            void Test()
            {
                int capture_me = 123;
                Entities
                    .ForEach((ref Translation t) => { capture_me++; }).Schedule(default);
            }
        }
    }
}