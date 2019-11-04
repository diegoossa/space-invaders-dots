using NUnit.Framework;
using System.IO;
using System.Text;
using Unity.Properties;
using Unity.Serialization.Json;
using UnityEditor;
using UnityEngine;

namespace Unity.Build.Tests
{
    struct SomeStruct
    {
        public bool Test;
    }

    struct UserSettings : IBuildSettingsComponent
    {
        public int Integer;
        public float Float;
        public string String;
        public SomeStruct Nested;

        public string Name { get { return "User Settings"; } }

        public bool OnGUI()
        {
            return false;
        }
    }

    class BuildSettingsComponentVisitor : PropertyVisitor
    {
        readonly BuildSettings m_BuildSettings;
        private readonly StringBuilder m_LogBuilder;
        int m_Indent;

        public BuildSettingsComponentVisitor(BuildSettings buildSettings)
        {
            m_BuildSettings = buildSettings;
            m_LogBuilder = new StringBuilder();
        }

        public void Log()
        {
            Debug.Log(m_LogBuilder.ToString());
        }

        public void ResetLog()
        {
            m_LogBuilder.Clear();
        }

        public override bool IsExcluded<TProperty, TContainer, TValue>(TProperty property, ref TContainer container)
        {
            return property.GetName() == JsonVisitor.Style.TypeInfoKey;
        }

        protected override VisitStatus Visit<TProperty, TContainer, TValue>(TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            var inherited = m_BuildSettings.IsComponentInherited(typeof(TContainer));
            var overridden = m_BuildSettings.IsComponentOverridden(typeof(TContainer));
            var indent = new string(' ', JsonVisitor.Style.Space * m_Indent);
            m_LogBuilder.AppendLine($"{indent}{property.GetName()} = {value.ToString()}{(inherited ? " [Inherited]" : "")}{(overridden ? " [Overridden]" : "")}");
            return VisitStatus.Handled;
        }

        protected override VisitStatus BeginContainer<TProperty, TContainer, TValue>(TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            var inherited = m_BuildSettings.IsComponentInherited(typeof(TContainer));
            var overridden = m_BuildSettings.IsComponentOverridden(typeof(TContainer));
            m_LogBuilder.AppendLine($"{property.GetName()} (Container){(inherited ? " [Inherited]" : "")}{(overridden ? " [Overridden]" : "")}");
            m_Indent++;
            return VisitStatus.Handled;
        }

        protected override void EndContainer<TProperty, TContainer, TValue>(TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            m_Indent--;
        }

        protected override VisitStatus BeginCollection<TProperty, TContainer, TValue>(TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            var inherited = m_BuildSettings.IsComponentInherited(typeof(TContainer));
            var overridden = m_BuildSettings.IsComponentOverridden(typeof(TContainer));
            m_LogBuilder.AppendLine($"{property.GetName()} (List){(inherited ? " [Inherited]" : "")}{(overridden ? " [Overridden]" : "")}");
            m_Indent++;
            return VisitStatus.Handled;
        }

        protected override void EndCollection<TProperty, TContainer, TValue>(TProperty property, ref TContainer container, ref TValue value, ref ChangeTracker changeTracker)
        {
            m_Indent--;
        }
    }

    public class BuildSettingsTests
    {
        const string k_DefaultBuildSettingsAssetPath = "Assets/Tests/DefaultBuildSettings.buildsettings";
        const string k_AssetPathA = "Assets/Tests/TestBuildSettingsA.buildsettings";
        const string k_AssetPathB = "Assets/Tests/TestBuildSettingsB.buildsettings";
        const string k_BuildAndRunSettingsAssetPath = "Assets/Tests/BuildAndRunSettings.buildsettings";
        const string k_BuildAndRunPipelineAssetPath = "Assets/Tests/BuildAndRunPipeline.asset";
        const string k_BuildAndRunExePath = "Assets/Tests/BuildAndRunPipeline.txt";

        [OneTimeSetUp]
        public void CreateAssets()
        {
            CreateBuildSettingsAsset(k_AssetPathA);
            CreateBuildSettingsAsset(k_AssetPathB);
        }

        [OneTimeTearDown]
        public void CleanupAssets()
        {
            AssetDatabase.DeleteAsset(k_DefaultBuildSettingsAssetPath);
            AssetDatabase.DeleteAsset(k_AssetPathA);
            AssetDatabase.DeleteAsset(k_AssetPathB);
            AssetDatabase.DeleteAsset(k_BuildAndRunSettingsAssetPath);
            AssetDatabase.DeleteAsset(k_BuildAndRunPipelineAssetPath);
            AssetDatabase.DeleteAsset(k_BuildAndRunExePath);
        }

        /// <summary>
        /// Verify that default build settings can be used as a dependency along with user settings.
        /// </summary>
        [Test]
        public void DefaultBuildSettings()
        {
            var buildSettings = ScriptableObject.CreateInstance<BuildSettings>();
            buildSettings.AddDependency(CreateBuildSettingsAsset(k_DefaultBuildSettingsAssetPath));
            buildSettings.SetComponent(new UserSettings
            {
                Integer = 1,
                Float = 123.456f,
                String = "test",
                Nested = new SomeStruct
                {
                    Test = true
                }
            });

            // Override build profile configuration
            var buildProfile = buildSettings.GetComponent<DotsRuntimeBuildProfile>();
            buildProfile.Configuration = BuildConfiguration.Release;
            buildSettings.SetComponent(buildProfile);

            // Verify user settings
            var userSettings = buildSettings.GetComponent<UserSettings>();
            Assert.That(userSettings.Integer, Is.EqualTo(1));
            Assert.That(userSettings.Float, Is.EqualTo(123.456f));
            Assert.That(userSettings.String, Is.EqualTo("test"));
            Assert.That(userSettings.Nested.Test, Is.EqualTo(true));

            // Verify build profile
            Assert.That(buildSettings.HasComponent<DotsRuntimeBuildProfile>(), Is.True);
            Assert.That(buildSettings.IsComponentOverridden<DotsRuntimeBuildProfile>(), Is.True);
            Assert.That(buildSettings.GetComponent<DotsRuntimeBuildProfile>().Configuration, Is.EqualTo(BuildConfiguration.Release));
        }

        [Test]
        public void DependenciesSerialization()
        {
            // Load build settings assets from database
            var assetA = AssetDatabase.LoadAssetAtPath<BuildSettings>(k_AssetPathA);
            var assetB = AssetDatabase.LoadAssetAtPath<BuildSettings>(k_AssetPathB);
            Assert.That(assetA, Is.Not.Null);
            Assert.That(assetB, Is.Not.Null);

            // Add dependency and re-serialize
            assetB.AddDependency(assetA);
            assetB.SerializeToFile(k_AssetPathB);
            AssetDatabase.ImportAsset(k_AssetPathB, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            assetB = AssetDatabase.LoadAssetAtPath<BuildSettings>(k_AssetPathB);
            Assert.That(assetB, Is.Not.Null);

            // Test dependencies
            Assert.That(assetA.GetDependencies().Count, Is.EqualTo(0));
            Assert.That(assetB.GetDependencies().Count, Is.EqualTo(1));
            Assert.That(assetB.GetDependencies()[0], Is.EqualTo(assetA));
        }

        static BuildSettings CreateBuildSettingsAsset(string path)
        {
            var buildSettings = ScriptableObject.CreateInstance<BuildSettings>();
            buildSettings.SetComponent(new DotsRuntimeBuildProfile());
            buildSettings.SerializeToFile(path);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<BuildSettings>(path);
        }

        [Test]
        public void BuildSettingsCanBuild_Returns_False_Without_Pipeline_Component()
        {
            var bs = CreateBuildSettingsAsset(k_BuildAndRunSettingsAssetPath);
            Assert.IsFalse(bs.CanBuild());
        }
        [Test]
        public void BuildSettingsCanBuild_Returns_False_With_Pipeline_Component_Without_Pipeline()
        {
            var bs = CreateBuildSettingsAsset(k_BuildAndRunSettingsAssetPath);
            bs.SetComponent(new BuildPipelineComponent());
            Assert.IsFalse(bs.CanBuild());
        }

        [Test]
        public void BuildSettingsCanBuild_Returns_True_With_Valid_Pipeline_Component()
        {
            var bs = CreateBuildSettingsAsset(k_BuildAndRunSettingsAssetPath);
            var pipeline = BuildPipeline.CreateNew(k_BuildAndRunPipelineAssetPath);
            bs.SetComponent(new BuildPipelineComponent() { Pipeline = pipeline });
            Assert.IsTrue(bs.CanBuild());
        }

        [BuildStep(flags = BuildStepAttribute.Flags.Hidden)]
        class CreateExeData : IBuildStep
        {
            public string Description => "";
            public bool IsEnabled(BuildContext context) => true;

            public bool RunStep(BuildContext context)
            {
                File.WriteAllText(k_BuildAndRunExePath, "asdf");
                context.Set(new ExecutableFile() { Path = new FileInfo(k_BuildAndRunExePath) });
                return true;
            }
            public void CleanupStep(BuildContext context) { }
        }

        [BuildStep(flags = BuildStepAttribute.Flags.Hidden)]
        class FailStep : IBuildStep
        {
            public string Description => "";
            public bool IsEnabled(BuildContext context) => true;
            public bool RunStep(BuildContext context)
            {
                return false;
            }
            public void CleanupStep(BuildContext context) { }
        }

        [Test]
        public void BuildSettingsCanRun_Returns_True_After_Build()
        {
            var bs = CreateBuildSettingsAsset(k_BuildAndRunSettingsAssetPath);
            bs.ClearLastBuild();
            var pipeline = BuildPipeline.CreateNew(k_BuildAndRunPipelineAssetPath);
            pipeline.AddStep(new CreateExeData());
            bs.SetComponent(new BuildPipelineComponent() { Pipeline = pipeline });
            Assert.IsTrue(bs.Build().Success);
            Assert.IsTrue(bs.CanRun());
        }

        [Test]
        public void BuildSettingsCanRun_Returns_False_Before_Build()
        {
            var bs = CreateBuildSettingsAsset(k_BuildAndRunSettingsAssetPath);
            bs.ClearLastBuild();
            var pipeline = BuildPipeline.CreateNew(k_BuildAndRunPipelineAssetPath);
            bs.SetComponent(new BuildPipelineComponent() { Pipeline = pipeline });
            Assert.IsFalse(bs.CanRun());
        }

        [Test]
        public void BuildSettingsCanRun_Returns_False_After_Failed_Build()
        {
            var bs = CreateBuildSettingsAsset(k_BuildAndRunSettingsAssetPath);
            bs.ClearLastBuild();
            var pipeline = BuildPipeline.CreateNew(k_BuildAndRunPipelineAssetPath);
            pipeline.AddStep<FailStep>();
            bs.SetComponent(new BuildPipelineComponent() { Pipeline = pipeline });
            Assert.IsFalse(bs.Build().Success);
            Assert.IsFalse(bs.CanRun());
        }

        //TODO: enable this test when Run, IsRunning, and StopRunning are implemented
        /*
        [Test]
        public void BuildSettingsIsRunning_Returns_False_Before_Run_And_True_After()
        {
            var bs = CreateBuildSettingsAsset(k_BuildAndRunSettingsAssetPath);
            var pipeline = BuildPipeline.CreateNew(k_BuildAndRunPipelineAssetPath);
            bs.SetComponent(new BuildPipelineComponent() { Pipeline = pipeline });
            Assert.IsFalse(bs.IsRunning());
            Assert.IsTrue(bs.Run());
            Assert.IsTrue(bs.IsRunning());
            bs.StopRunning();
            Assert.IsFalse(bs.IsRunning());
        }
        */
    }
}
