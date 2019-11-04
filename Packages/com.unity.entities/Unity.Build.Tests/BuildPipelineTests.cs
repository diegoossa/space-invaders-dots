using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Build.Tests
{
    [TestFixture]
    class BuildPipelineTests
    {
        class NotABuildStep
        {
        }

        class StepRunResults
        {
            public List<string> stepsRun = new List<string>();
        }

        class StepCleanupResults
        {
            public List<string> stepsRun = new List<string>();
        }

        [BuildStep(flags = BuildStepAttribute.Flags.Hidden)]
        class TestStep1 : IBuildStep
        {
            public class Data
            {
                public string value;
            }

            public bool enabled = true;
            public string Description => "TestStep1";
            string addedValue;
            public bool IsEnabled(BuildContext context)
            {
                return enabled;
            }

            public bool RunStep(BuildContext context)
            {
                context.GetOrCreate<StepRunResults>().stepsRun.Add(addedValue = context.Get<Data>().value);
                return true;
            }
            public void CleanupStep(BuildContext context)
            {
                context.GetOrCreate<StepCleanupResults>().stepsRun.Add(addedValue);
            }
        }

        [BuildStep(flags = BuildStepAttribute.Flags.Hidden)]
        class TestStep2 : IBuildStep
        {
            public class Data
            {
                public string value;
            }

            public bool enabled = true;
            public string Description => "TestStep2";
            string addedValue;
            public bool IsEnabled(BuildContext context)
            {
                return enabled;
            }

            public bool RunStep(BuildContext context)
            {
                context.GetOrCreate<StepRunResults>().stepsRun.Add(addedValue = context.Get<Data>().value);
                return true;
            }
            public void CleanupStep(BuildContext context)
            {
                context.GetOrCreate<StepCleanupResults>().stepsRun.Add(addedValue);
            }
        }
        [BuildStep(flags = BuildStepAttribute.Flags.Hidden)]
        class FailStep : IBuildStep
        {
            public string Description => "";
            public bool IsEnabled(BuildContext context) => true;
            public bool RunStep(BuildContext context)
            {
                context.GetOrCreate<StepRunResults>().stepsRun.Add("fail");
                return false;
            }
            public void CleanupStep(BuildContext context)
            {
                context.GetOrCreate<StepCleanupResults>().stepsRun.Add("fail");
            }
        }

        [Test]
        public void CanCreateStepFromType()
        {
            var step = BuildPipeline.CreateStepFromType(typeof(TestStep1));
            Assert.IsNotNull(step);
            Assert.IsInstanceOf<IBuildStep>(step);
        }

        [Test]
        public void Create_Step_With_Invalid_Type_Returns_Null()
        {
            Assert.IsNull(BuildPipeline.CreateStepFromType(typeof(NotABuildStep)));
        }

        [Test]
        public void Create_Step_With_Invalid_Type_String_Returns_Null()
        {
            Assert.IsNull(BuildPipeline.CreateStepFromData(typeof(NotABuildStep).AssemblyQualifiedName));
        }

        [Test]
        public void Create_Step_With_Invalid_Step_Data_Returns_Null()
        {
            Assert.IsNull(BuildPipeline.CreateStepFromData("InvalidData"));
        }

        [Test]
        public void CanSetAndGetSteps()
        {
            var pipeline = BuildPipeline.CreateInstance<BuildPipeline>();
            var steps = new IBuildStep[] { new TestStep1(), new TestStep1(), new TestStep2(), new TestStep1(), new TestStep1() };
            pipeline.SetSteps(steps);
            var stepList = new List<IBuildStep>();
            var result = pipeline.GetSteps(stepList);
            Assert.True(result);
            Assert.AreEqual(steps.Length, pipeline.StepCount);
            Assert.AreEqual(stepList.Count, pipeline.StepCount);
            Assert.IsAssignableFrom<TestStep1>(pipeline.GetStep(0));
            Assert.IsAssignableFrom<TestStep2>(pipeline.GetStep(2));
        }


        [Test]
        public void CanCreateStepFromTypeName()
        {
            var step = BuildPipeline.CreateStepFromData(typeof(TestStep1).AssemblyQualifiedName);
            Assert.IsNotNull(step);
            Assert.IsInstanceOf<IBuildStep>(step);
        }

        [Test]
        public void CanCreateStepData()
        {
            var step = new TestStep1();
            var data = BuildPipeline.CreateStepData(step);
            Assert.IsNotNull(data);
            Assert.IsNotEmpty(data);
            var step2 = BuildPipeline.CreateStepFromData(data);
            Assert.IsNotNull(step2);
        }

        BuildContext CreateTestContext()
        {
            return new BuildContext(new TestStep1.Data() { value = "step1" }, new TestStep2.Data() { value = "step2" });
        }

        void ValidateTestContext(BuildContext context)
        {
            var results = context.Get<StepRunResults>();
            Assert.IsNotNull(results);
            Assert.AreEqual(results.stepsRun.Count, 2);
            Assert.AreEqual(results.stepsRun[0], "step1");
            Assert.AreEqual(results.stepsRun[1], "step2");
        }

        [Test]
        public void CanRunPipelineWithTypeArray()
        {
            var context = CreateTestContext();
            BuildPipeline.RunSteps(context, typeof(TestStep1), typeof(TestStep2));
            ValidateTestContext(context);
        }

        [Test]
        public void CanRunPipelineWithStepDataArray()
        {
            var context = CreateTestContext();
            BuildPipeline.RunSteps(context, typeof(TestStep1).AssemblyQualifiedName, typeof(TestStep2).AssemblyQualifiedName);
            ValidateTestContext(context);
        }

        [Test]
        public void When_Pipeline_Succeeds_All_Cleanup_Called()
        {
            var context = CreateTestContext();
            BuildPipeline.RunSteps(context, typeof(TestStep1), typeof(TestStep2));
            var results = context.Get<StepCleanupResults>();
            Assert.AreEqual(results.stepsRun.Count, 2);
            Assert.AreEqual(results.stepsRun[0], "step2");
            Assert.AreEqual(results.stepsRun[1], "step1");
        }

        [Test]
        public void When_Pipeline_Fails_Only_Run_Cleanups_Called()
        {
            var context = CreateTestContext();
            BuildPipeline.RunSteps(context, typeof(TestStep1), typeof(FailStep),  typeof(TestStep2));
            var results = context.Get<StepCleanupResults>();
            Assert.AreEqual(results.stepsRun.Count, 2);
            Assert.AreEqual(results.stepsRun[0], "fail");
            Assert.AreEqual(results.stepsRun[1], "step1");
        }

        [Test]
        public void When_Nested_Pipeline_Fails_Parent_Pipeline_Fails()
        {
            var subPipe = BuildPipeline.CreateInstance<BuildPipeline>();
            subPipe.AddStep<FailStep>();
            var path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/sub_pipeline_test_asset.asset");
            UnityEditor.AssetDatabase.CreateAsset(subPipe, path);

            var parentPipe = BuildPipeline.CreateInstance<BuildPipeline>();
            parentPipe.AddStep(subPipe);
            var res = parentPipe.RunSteps(new BuildContext());
            Assert.AreEqual(false, res.Success);
            UnityEditor.AssetDatabase.DeleteAsset(path);
        }

        [BuildStep(flags = BuildStepAttribute.Flags.Hidden)]
        class UnityObjectBuildStep : ScriptableObject, IBuildStep
        {
            public string Description => "";
            public void CleanupStep(BuildContext context) { }
            public bool IsEnabled(BuildContext context) => true;
            public bool RunStep(BuildContext context) => true;
        }

        [BuildStep(flags = BuildStepAttribute.Flags.Hidden)]
        class DisabledBuildStep : IBuildStep
        {
            public string Description => "";
            public void CleanupStep(BuildContext context) { }
            public bool IsEnabled(BuildContext context) => false;
            public bool RunStep(BuildContext context) => throw new Exception("Should not run");
        }

        [Test]
        public void Cannot_Add_NonSerialized_UnityObject_BuildStep()
        {
            var subPipe = BuildPipeline.CreateInstance<UnityObjectBuildStep>();
            var pipeline = BuildPipeline.CreateInstance<BuildPipeline>();
            Assert.IsFalse(pipeline.AddStep(subPipe));
        }

        [Test]
        public void Cannot_Add_Null_BuildStep_Object()
        {
            var pipeline = BuildPipeline.CreateInstance<BuildPipeline>();
            Assert.IsFalse(pipeline.AddStep(default(IBuildStep)));
        }

        [Test]
        public void Cannot_Add_Null_BuildStep_Type()
        {
            var pipeline = BuildPipeline.CreateInstance<BuildPipeline>();
            Assert.IsFalse(pipeline.AddStep(default(Type)));
        }

        [Test]
        public void Cannot_Add_Empty_Null_BuildStep_String()
        {
            var pipeline = BuildPipeline.CreateInstance<BuildPipeline>();
            Assert.IsFalse(pipeline.AddStep(default(string)));
            Assert.IsFalse(pipeline.AddStep(string.Empty));
        }

        [Test]
        public void Disabled_Step_Doesnt_Run()
        {
            var pipeline = BuildPipeline.CreateInstance<BuildPipeline>();
            pipeline.AddStep<DisabledBuildStep>();
            Assert.DoesNotThrow(() => pipeline.RunStep(new BuildContext()), "");
        }

        [Test]
        public void CanRunPipelineWithNestedPipelines()
        {
            var context = CreateTestContext();
            var subPipe1 = BuildPipeline.CreateInstance<BuildPipeline>();
            subPipe1.AddStep<TestStep1>();
            subPipe1.AddStep<TestStep1>();

            var subPipe2 = BuildPipeline.CreateInstance<BuildPipeline>();
            subPipe2.AddStep<TestStep2>();
            subPipe2.AddStep<TestStep2>();

            BuildPipeline.RunSteps(context, new IBuildStep[]{
                subPipe1,
                BuildPipeline.CreateStepFromType(typeof(TestStep1)),
                subPipe2,
                BuildPipeline.CreateStepFromType(typeof(TestStep2))
                });
            var results = context.Get<StepRunResults>();
            Assert.IsNotNull(results);
            Assert.AreEqual(results.stepsRun.Count, 6);
            Assert.AreEqual(results.stepsRun[0], "step1");
            Assert.AreEqual(results.stepsRun[1], "step1");
            Assert.AreEqual(results.stepsRun[2], "step1");
            Assert.AreEqual(results.stepsRun[3], "step2");
            Assert.AreEqual(results.stepsRun[4], "step2");
            Assert.AreEqual(results.stepsRun[5], "step2");
        }

        [Test]
        public void EventsCalledWhenPipelineRuns()
        {
            bool startCalled = false;
            bool completeCalled = false;
            BuildPipeline.BuildStarted += p => startCalled = true;
            BuildPipeline.BuildCompleted += (r) => completeCalled = true;
            var pipeline = ScriptableObject.CreateInstance<BuildPipeline>();
            pipeline.RunSteps(new BuildContext());
            Assert.IsTrue(startCalled);
            Assert.IsTrue(completeCalled);
        }
    }

    [TestFixture]
    class BuildPipelineContextTests
    {
        class TestA { }
        class TestB { }
        [Test]
        public void CanSetContextObjectWithConstructor()
        {
            var context = new BuildContext(new TestA());
            Assert.IsNotNull(context.Get<TestA>());
        }

        [Test]
        public void CanSetContextWithObject()
        {
            var context = new BuildContext();
            context.Set(new TestA());
            Assert.IsNotNull(context.Get<TestA>());
        }

        [Test]
        public void SetWithExistingTypeFails()
        {
            var context = new BuildContext();
            context.Set(new TestA());
            Assert.IsFalse(context.Set(new TestA()));
        }
        [Test]
        public void CanSetAndRemove()
        {
            var context = new BuildContext();
            context.Set(new TestA());
            Assert.IsNotNull(context.Get<TestA>());
            context.Remove<TestA>();
            Assert.IsNull(context.Get<TestA>());
        }

        [Test]
        public void GetOrCreateSucceedsWhenObjectNotPresent()
        {
            var context = new BuildContext();
            Assert.IsNotNull(context.GetOrCreate<TestA>());
        }
        [Test]
        public void GetOrCreateSucceedsWhenObjectPresent()
        {
            var context = new BuildContext();
            context.Set(new TestA());
            Assert.IsNotNull(context.GetOrCreate<TestA>());
        }

        [Test]
        public void SetAllAddsAllObjects()
        {
            var context = new BuildContext();
            context.SetAll(new TestA(), new TestB());
            Assert.IsNotNull(context.Get<TestA>());
            Assert.IsNotNull(context.Get<TestB>());
        }
        [Test]
        public void SetAllSkipsNullObjects()
        {
            var context = new BuildContext();
            var added = context.SetAll(new TestA(), new TestB(), null);
            Assert.AreEqual(2, added);
        }

        [Test]
        public void SetAllSkipsDuplicateObjects()
        {
            var context = new BuildContext();
            var added = context.SetAll(new TestA(), new TestB(), new TestA());
            Assert.AreEqual(2, added);
        }
    }
}