using System.Collections.Generic;
using System.IO;
using UnityEditor;
using PropertyAttribute = Unity.Properties.PropertyAttribute;

namespace Unity.Build
{
    public sealed class ClassicBuildProfile : IBuildSettingsComponent
    {
        BuildTarget m_Target;
        List<string> m_ExcludedAssemblies;

        /// <summary>
        /// Retrieve <see cref="BuildTypeCache"/> for this build profile.
        /// </summary>
        public BuildTypeCache TypeCache { get; } = new BuildTypeCache();

        /// <summary>
        /// Gets or sets which <see cref="UnityEditor.BuildTarget"/> this profile is going to use for the build.
        /// Used for building classic Unity standalone players.
        /// </summary>
        [Property]
        public BuildTarget Target
        {
            get => m_Target;
            set
            {
                m_Target = value;
                TypeCache.SetPlatformName(m_Target.ToString());
            }
        }

        /// <summary>
        /// Gets or sets which <see cref="Configuration"/> this profile is going to use for the build.
        /// </summary>
        [Property]
        public BuildConfiguration Configuration { get; set; } = BuildConfiguration.Develop;

        /// <summary>
        /// Directory path name where the build will be output.
        /// </summary>
        [Property]
        public DirectoryInfo OutputPath { get; set; } = new DirectoryInfo("Builds");

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

        public ClassicBuildProfile()
        {
            Target = BuildTarget.NoTarget;
            ExcludedAssemblies = new List<string>();
        }

        public string Name => nameof(ClassicBuildProfile);
        public bool OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            Target = (BuildTarget)EditorGUILayout.EnumPopup("Target", Target);
            Configuration = (BuildConfiguration)EditorGUILayout.EnumPopup("Configuration", Configuration);
            OutputPath = new DirectoryInfo(EditorGUILayout.TextField("Output Path", OutputPath.GetRelativePath()));
            return EditorGUI.EndChangeCheck();
        }
    }
}
