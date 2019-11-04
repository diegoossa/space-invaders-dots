using System.IO;
using System.Reflection;
using UnityEditor;

namespace Unity.Build
{
    [BuildStep(description = kDescription, category = "Classic")]
    class BuildStepBuildClassicPlayer : IBuildStep
    {
        const string kDescription = "Build Player";
        public string Description => kDescription;
        public bool IsEnabled(BuildContext context) => true;

        public bool RunStep(BuildContext context)
        {
            var settings = context.BuildSettings;
            if (settings.ClassicBuildProfile.Target <= 0)
            {
                UnityEngine.Debug.LogErrorFormat("Invalid build target in build settings object {0}", settings.name);
                return false;
            }

            var outputPath = settings.ClassicBuildProfile.OutputPath.FullName;
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var productName = settings.GetComponent<GeneralSettings>().ProductName;
            var extension = BuildSettings.GetExecutableExtension(settings.ClassicBuildProfile.Target);
            var locationPathName = Path.Combine(outputPath, productName + extension);

            var options = new BuildPlayerOptions()
            {
                scenes = settings.GetComponent<SceneList>().GetScenePathsForBuild(),
                target = settings.ClassicBuildProfile.Target,
                locationPathName = locationPathName,
                targetGroup = UnityEditor.BuildPipeline.GetBuildTargetGroup(settings.ClassicBuildProfile.Target),
                options = settings.ClassicBuildProfile.Configuration != BuildConfiguration.Release ? BuildOptions.Development | BuildOptions.ShowBuiltPlayer  : BuildOptions.ShowBuiltPlayer
            };

            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                var exe = context.GetOrCreate<ExecutableFile>();
                exe.Path = new FileInfo(report.summary.outputPath);
                return true;
            }
            return false;
        }

        public void CleanupStep(BuildContext context) { }
    }
}
