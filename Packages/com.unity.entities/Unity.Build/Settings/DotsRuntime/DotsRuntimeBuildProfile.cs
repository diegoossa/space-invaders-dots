using System.Collections.Generic;
using System.Linq;
using Unity.Platforms;
using PropertyAttribute = Unity.Properties.PropertyAttribute;

namespace Unity.Build
{
    public sealed class DotsRuntimeBuildProfile : IBuildSettingsComponent
    {
        BuildTarget m_Target;
        List<string> m_ExcludedAssemblies;

        /// <summary>
        /// Retrieve <see cref="BuildTypeCache"/> for this build profile.
        /// </summary>
        public BuildTypeCache TypeCache { get; } = new BuildTypeCache();

        /// <summary>
        /// Gets or sets which <see cref="Platforms.BuildTarget"/> this profile is going to use for the build.
        /// Used for building Dots Runtime players.
        /// </summary>
        [Property]
        public BuildTarget Target
        {
            get => m_Target;
            set
            {
                m_Target = value;
                TypeCache.SetPlatformName(m_Target?.GetUnityPlatformName());
            }
        }

        /// <summary>
        /// Gets or sets which <see cref="Configuration"/> this profile is going to use for the build.
        /// </summary>
        [Property]
        public BuildConfiguration Configuration { get; set; } = BuildConfiguration.Develop;

        /// <summary>
        /// List of assemblies that should be explicitly excluded for the build.
        /// </summary>
        [Property]
        public List<string> ExcludedAssemblies
        {
            get => m_ExcludedAssemblies;
            set
            {
                m_ExcludedAssemblies = value;
                TypeCache.SetExcludedAssemblies(value);
            }
        }

        public DotsRuntimeBuildProfile()
        {
            Target = BuildTarget.DefaultBuildTarget;
            ExcludedAssemblies = new List<string>();
        }

        public string Name => nameof(DotsRuntimeBuildProfile);
        public bool OnGUI()
        {
            // Placeholder GUI until we'll have generic inspector
            int index = -1;
            for (int i = 0; i < BuildTarget.AvailableBuildTargets.Count; i++)
            {
                if (Target.Equals(BuildTarget.AvailableBuildTargets[i]))
                {
                    index = i;
                    break;
                }
            }
            UnityEditor.EditorGUI.BeginChangeCheck();
            index = UnityEditor.EditorGUILayout.Popup("Target", index, BuildTarget.AvailableBuildTargets.Select(m => m.GetDisplayName()).ToArray());
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                Target = BuildTarget.AvailableBuildTargets[index];
                return true;
            }

            UnityEditor.EditorGUI.BeginChangeCheck();
            Configuration = (BuildConfiguration)UnityEditor.EditorGUILayout.EnumPopup("Configuration", Configuration);
            return UnityEditor.EditorGUI.EndChangeCheck();
        }
    }
}
