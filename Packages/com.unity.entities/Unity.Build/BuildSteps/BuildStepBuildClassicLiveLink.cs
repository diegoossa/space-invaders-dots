using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Build
{
    [BuildStep(description = kDescription, category = "Classic")]
    public class BuildStepBuildClassicLiveLink : IBuildStep
    {
        const string kDescription = "Build LiveLink Player";
        public string Description => kDescription;
        public bool IsEnabled(BuildContext context) => true;

        public bool RunStep(BuildContext context)
        {
            var settings = context.BuildSettings;
            var options = BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.AutoRunPlayer;
            var profile = settings.ClassicBuildProfile;
            if (profile.Target == BuildTarget.NoTarget)
            {
                Debug.LogError($"Invalid build target in build settings object {settings.name}");
                return false;
            }

            var scenesList = settings.GetComponent<SceneList>().GetScenePathsForBuild();
            if (scenesList.Length == 0)
            {
                Debug.LogError("There are no scenes to build");
                return false;
            }

            var outputPath = settings.ClassicBuildProfile.OutputPath.FullName;
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var productName = settings.GetComponent<GeneralSettings>().ProductName + " LiveLink";
            var extension = BuildSettings.GetExecutableExtension(profile.Target);
            var locationPathName = Path.Combine(outputPath, productName + extension);

            var buildTarget = profile.Target;
            var buildTargetGroup = UnityEditor.BuildPipeline.GetBuildTargetGroup(buildTarget);
            var oldDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
            var newDefines = oldDefines + ";ENABLE_PLAYER_LIVELINK";
            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newDefines);
            var buildSucceeded = false;
            try
            {
                var report = UnityEditor.BuildPipeline.BuildPlayer(scenesList, locationPathName, EditorUserBuildSettings.activeBuildTarget, options);
                buildSucceeded = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, oldDefines);
            }
            return buildSucceeded;
        }
        public void CleanupStep(BuildContext context) { }
    }
}
