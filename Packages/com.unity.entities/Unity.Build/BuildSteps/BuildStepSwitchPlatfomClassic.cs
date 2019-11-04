using UnityEditor;
using UnityEngine;

namespace Unity.Build
{
    class BuildStepSwitchPlatfomClassic : IBuildStep
    {
        public string Description => "Switch Active Platform (Classic)";
        public bool IsEnabled(BuildContext context)
        {
            return true;
        }

        public bool RunStep(BuildContext context)
        {
            var buildSettings = context.BuildSettings;
            var target = buildSettings.ClassicBuildProfile.Target;
            if (buildSettings.ClassicBuildProfile.Target == BuildTarget.NoTarget)
            {
                Debug.LogError($"Invalid build target in build settings object {buildSettings.name}");
                return false;
            }

            if (EditorUserBuildSettings.activeBuildTarget == target)
                return true;

            if (EditorUserBuildSettings.SwitchActiveBuildTarget(UnityEditor.BuildPipeline.GetBuildTargetGroup(target), target))
            {
                Debug.LogError($"Editor's active Build Target needed to be switched. Please wait for switch to complete and then build again.");
                return false;
            }

            Debug.LogError($"Editor's active Build Target could not be switched. Look in the console or the editor log for additional errors.");
            return false;
        }
        public void CleanupStep(BuildContext context) { }
    }
}
