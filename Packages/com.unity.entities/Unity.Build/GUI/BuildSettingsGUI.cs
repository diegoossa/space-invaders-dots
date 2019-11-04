using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Unity.Build
{
    static class BuildSettingsGUI
    {
        const string kBuildSettingsDOTS = "Assets/Create/Build Settings/Dots Runtime";
        const string kBuildSettingsClassic = "Assets/Create/Build Settings/Classic";
        const string kBuildPipeline = "Assets/Create/Build Pipeline";

        [MenuItem(kBuildSettingsDOTS, true)]
        static bool CreateNewBuildSettingsAssetValidationDOTS()
        {
            return Directory.Exists(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem(kBuildSettingsDOTS)]
        static void CreateNewBuildSettingsAssetDOTS()
        {
            CreateNewBuildSettingsAsset("DOTS", new DotsRuntimeBuildProfile());
        }

        [MenuItem(kBuildSettingsClassic, true)]
        static bool CreateNewBuildSettingsAssetValidationClassic()
        {
            return Directory.Exists(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem(kBuildSettingsClassic)]
        static void CreateNewBuildSettingsAssetClassic()
        {
            CreateNewBuildSettingsAsset("Classic", new ClassicBuildProfile());
        }

        [MenuItem(kBuildPipeline, true)]
        static bool AddPipelineContexMenuValidation()
        {
            return Directory.Exists(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem(kBuildPipeline)]
        static void AddPipelineContexMenu()
        {
            BuildPipeline.CreateNew(CreateAssetPathInActiveDirectory("BuildPipeline.asset"));
        }

        static BuildSettings CreateNewBuildSettingsAsset(string assetPrefix, IBuildSettingsComponent profileComponent)
        {
            var dependency = Selection.activeObject as BuildSettings;
            var path = CreateAssetPathInActiveDirectory(assetPrefix + "_BuildSettings.buildsettings");
            var buildSettings = ScriptableObject.CreateInstance<BuildSettings>();
            var pipelinePath = AssetDatabase.GenerateUniqueAssetPath(path.Replace(".buildsettings", "_pipeline.asset"));
            buildSettings.SetComponent(new BuildPipelineComponent() { Pipeline = BuildPipeline.CreateNew(pipelinePath) });
            buildSettings.SetComponent(new GeneralSettings());
            buildSettings.SetComponent(new SceneList());
            buildSettings.SetComponent(profileComponent.GetType(), profileComponent);
            if (dependency != null)
                buildSettings.AddDependency(dependency);
            buildSettings.SerializeToFile(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<BuildSettings>(path);
        }

        static string CreateAssetPathInActiveDirectory(string defaultFilename)
        {
            string path = null;
            if (Selection.activeObject != null)
            {
                var aoPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(aoPath))
                {
                    if (Directory.Exists(aoPath))
                        path = Path.Combine(aoPath, defaultFilename);
                    else
                        path = Path.Combine(Path.GetDirectoryName(aoPath), defaultFilename);
                }
            }
            return AssetDatabase.GenerateUniqueAssetPath(path);
        }




        [MenuItem("Assets/BuildSettings/Build", true)]
        static bool InvokeFromContexMenuValidation()
        {
            if (!(Selection.activeObject is BuildSettings))
                return false;
            return (Selection.activeObject as BuildSettings).CanBuild();
        }

        [MenuItem("Assets/BuildSettings/Build")]
        static void InvokeFromContexMenu()
        {
            (Selection.activeObject as BuildSettings).Build();
        }

        [MenuItem("Assets/BuildSettings/Build and Run", true)]
        static bool InvokeFromContexMenuValidationBuildAndRun()
        {
            if (!(Selection.activeObject is BuildSettings))
                return false;
            return (Selection.activeObject as BuildSettings).CanBuild();
        }

        [MenuItem("Assets/BuildSettings/Build and Run")]
        static void InvokeFromContexMenuBuildAndRun()
        {
            var settings = Selection.activeObject as BuildSettings;
            if (settings.Build().Success)
                settings.Run();

        }
        [MenuItem("Assets/BuildSettings/Run", true)]
        static bool InvokeFromContexMenuValidationRun()
        {
            if (!(Selection.activeObject is BuildSettings))
                return false;
            return (Selection.activeObject as BuildSettings).CanRun();
        }

        [MenuItem("Assets/BuildSettings/Run")]
        static void InvokeFromContexMenuRun()
        {
            (Selection.activeObject as BuildSettings).Run();
        }
        [MenuItem("Assets/BuildSettings/Stop", true)]
        static bool InvokeFromContexMenuValidationStop()
        {
            if (!(Selection.activeObject is BuildSettings))
                return false;
            return (Selection.activeObject as BuildSettings).IsRunning();
        }

        [MenuItem("Assets/BuildSettings/Stop")]
        static void InvokeFromContexMenuStop()
        {
            (Selection.activeObject as BuildSettings).StopRunning();
        }
    }
}