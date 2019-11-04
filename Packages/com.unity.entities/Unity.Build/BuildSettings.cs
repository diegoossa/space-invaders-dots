using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Properties;
using Unity.Serialization;
using Unity.Serialization.Json;
using UnityEditor;
using UnityEngine;

namespace Unity.Build
{
    /// <summary>
    /// Can stores a set of unique components, which can be inherited or overridden using dependencies.
    /// </summary>
    public sealed class BuildSettings : ComponentContainer<IBuildSettingsComponent>
    {
        const string k_LastBuildFileName = "lastbuild.txt";

        /// <summary>
        /// Quick access to <see cref="Build.DotsRuntimeBuildProfile"/> component.
        /// </summary>
        public DotsRuntimeBuildProfile DotsRuntimeBuildProfile
        {
            get => GetComponent<DotsRuntimeBuildProfile>();
            set => SetComponent(value);
        }

        /// <summary>
        /// Quick access to <see cref="Build.ClassicBuildProfile"/> component.
        /// </summary>
        public ClassicBuildProfile ClassicBuildProfile
        {
            get => GetComponent<ClassicBuildProfile>();
            set => SetComponent(value);
        }

        /// <summary>
        /// Check whether a BuildSettings object is buildable.  It must contain a BuildPipeline component with a valid BuildPipeline object.
        /// </summary>
        /// <returns>True if the object can be built.</returns>
        public bool CanBuild() => HasComponent<BuildPipelineComponent>() && GetComponent<BuildPipelineComponent>().Pipeline != null;

        /// <summary>
        /// Build a BuildSettings object.
        /// </summary>
        /// <returns>The result of the build.</returns>
        public BuildResult Build()
        {
            if (!HasComponent<BuildPipelineComponent>())
            {
                Debug.LogError($"BuildSettings object {name} does not have a BuildPipelineComponent");
                return new BuildResult() { Success = false };
            }

            var pipeline = GetComponent<BuildPipelineComponent>().Pipeline;
            if (pipeline == null)
            {
                Debug.LogError($"BuildPipelineComponent attached to BuildSettings object {name} does not have a valid BuildPipeline");
                return new BuildResult() { Success = false };
            }

            using (var progress = new BuildProgress($"Running BuildPipeline {pipeline} on BuildSettings {name}.", "Please wait..."))
            {
                return pipeline.RunSteps(new BuildContext(this, progress));
            }
        }

        /// <summary>
        /// Check whether a settings object can be run.
        /// </summary>
        /// <returns>True if the conditions for running are met.  This depends on having a lastbuild.txt in the BuildProfile.BuildPath folder with the name of the executable to run.</returns>
        public bool CanRun()
        {
            var lastBuildPath = GetLastBuildPath(this);
            return lastBuildPath != null && lastBuildPath.Exists;
        }

        /// <summary>
        /// Check if a build settings is running.
        /// </summary>
        /// <returns>True if the build settings is running, false otherwise.</returns>
        public bool IsRunning()
        {
            //TODO: how to do this?
            return false;
        }

        /// <summary>
        /// Clears the saved last build path.
        /// </summary>
        public void ClearLastBuild()
        {
            SetLastBuildPath(this, null);
        }

        /// <summary>
        /// Stop a running build.
        /// </summary>
        public void StopRunning()
        {
            //TODO: how to do this?
            Debug.LogWarning($"{nameof(BuildSettings.StopRunning)} not implemented.");
        }

        /// <summary>
        /// Run a previously build. This depends on having a lastbuild.txt in the BuildProfile.BuildPath folder with the name of the executable to run.
        /// </summary>
        /// <returns>True if the build is run.</returns>
        public bool Run()
        {
            var lastBuildPath = GetLastBuildPath(this);
            if (lastBuildPath == null || !lastBuildPath.Exists)
            {
                return false;
            }

            Platforms.BuildTarget target = null;
            if (HasComponent<DotsRuntimeBuildProfile>())
            {
                target = DotsRuntimeBuildProfile.Target;
            }
            else if (HasComponent<ClassicBuildProfile>())
            {
                target = GetPlatformsBuildTarget(ClassicBuildProfile.Target);
            }

            if (target == null)
            {
                Debug.LogWarningFormat("Unable to find BuildTarget from BuildSettings object {0}.", name);
                EditorUtility.RevealInFinder(lastBuildPath.FullName);
                return false;
            }
            return target.Run(lastBuildPath);
        }

#if UNITY_EDITOR
        public static BuildSettings LoadBuildSettings(GUID buildSettingsGUID)
        {
            var buildSettingGuidString = buildSettingsGUID.ToString();
            var assetGuid = AssetDatabase.GUIDToAssetPath(buildSettingGuidString);
            return AssetDatabase.LoadAssetAtPath<BuildSettings>(assetGuid);
        }
#endif

        static BuildSettings()
        {
            JsonVisitorRegistration += (JsonVisitor visitor) =>
            {
                visitor.AddAdapter(new BuildSettingsJsonAdapter(visitor));
            };

            TypeConversion.Register<SerializedStringView, Platforms.BuildTarget>((view) =>
            {
                return BuildSettingsJsonAdapter.FindBuildTargetByName(view.ToString());
            });
        }

        [InitializeOnLoadMethod]
        static void RegisterBuildPipelineCallbacks()
        {
            BuildPipeline.BuildCompleted += OnBuildPipelineCompleted;
        }

        static string GetBuildInfoPath(BuildSettings settings)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(settings, out var guid, out long fileId))
            {
                return string.Empty;
            }
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library/BuildInfo", guid);
        }

        static FileInfo GetLastBuildPath(BuildSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("Invalid BuildSettings object");
                return default;
            }

            var path = Path.Combine(GetBuildInfoPath(settings), k_LastBuildFileName);
            if (!File.Exists(path))
            {
                return default;
            }

            var exePath = File.ReadAllText(path);
            return new FileInfo(exePath);
        }

        static void SetLastBuildPath(BuildSettings settings, string value)
        {
            if (settings == null)
            {
                Debug.LogError("Invalid BuildSettings object");
                return;
            }

            var path = Path.Combine(GetBuildInfoPath(settings), k_LastBuildFileName);
            if (string.IsNullOrEmpty(value))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, value);
            }
        }

        static void OnBuildPipelineCompleted(BuildResult result)
        {
            try
            {
                if (result.Success)
                {
                    var exe = result.Context.Get<ExecutableFile>();
                    if (exe == null || !File.Exists(exe.Path.FullName) || result.Context.BuildSettings == null)
                    {
                        return;
                    }
                    SetLastBuildPath(result.Context.BuildSettings, exe.Path.FullName);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal static Platforms.BuildTarget GetPlatformsBuildTarget(BuildTarget unityBuildTarget)
        {
            if (unityBuildTarget == BuildTarget.StandaloneWindows)
                unityBuildTarget = BuildTarget.StandaloneWindows64;

#pragma warning disable CS0618
            if (unityBuildTarget == BuildTarget.StandaloneOSXIntel || unityBuildTarget == BuildTarget.StandaloneOSXIntel64)
                unityBuildTarget = BuildTarget.StandaloneOSX;

            if (unityBuildTarget == BuildTarget.StandaloneLinux || unityBuildTarget == BuildTarget.StandaloneLinuxUniversal)
                unityBuildTarget = BuildTarget.StandaloneLinux64;
#pragma warning restore CS0618

            var buildTarget = Platforms.BuildTarget.AvailableBuildTargets.FirstOrDefault(x => x.GetUnityPlatformName() == unityBuildTarget.ToString());
            if (buildTarget == null)
            {
                Debug.LogFormat($"Unable to find {nameof(Platforms.BuildTarget)} from {nameof(BuildTarget)} value {unityBuildTarget.ToString()}.");
            }
            return buildTarget;
        }

        internal static string GetExecutableExtension(BuildTarget unityBuildTarget)
        {
#pragma warning disable CS0618
            switch (unityBuildTarget)
            {
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneOSXIntel:
                case BuildTarget.StandaloneOSXIntel64:
                    return ".app";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return ".exe";
                case BuildTarget.NoTarget:
                    return string.Empty;
                default:
                    throw new ArgumentException($"Invalid or unhandled enum {unityBuildTarget.ToString()} (index {(int)unityBuildTarget})");
            }
#pragma warning restore CS0618
        }

        class BuildSettingsJsonAdapter : JsonVisitorAdapter,
            IVisitAdapter<Platforms.BuildTarget>
        {
            public BuildSettingsJsonAdapter(JsonVisitor visitor) : base(visitor) { }

            public VisitStatus Visit<TProperty, TContainer>(IPropertyVisitor visitor, TProperty property, ref TContainer container, ref Platforms.BuildTarget value, ref ChangeTracker changeTracker)
                where TProperty : IProperty<TContainer, Platforms.BuildTarget>
            {
                Append(property, value, (builder, v) => { builder.Append(v != null ? EncodeJsonString(v.GetBeeTargetName()) : "null"); });
                return VisitStatus.Handled;
            }

            sealed class UnknownBuildTarget : Platforms.BuildTarget
            {
                readonly string m_BeeTargetName;

                public override bool HideInBuildTargetPopup => true;

                public UnknownBuildTarget()
                {
                    // All BuildTarget based classes require parameter-less constructor.
                }

                public UnknownBuildTarget(string beeTargetName)
                {
                    m_BeeTargetName = beeTargetName;
                }

                public override string GetBeeTargetName() => m_BeeTargetName;
                public override string GetDisplayName() => throw new NotImplementedException();
                public override string GetUnityPlatformName() => throw new NotImplementedException();
                public override string GetExecutableExtension() => throw new NotImplementedException();
                public override bool Run(FileInfo buildTarget) => throw new NotImplementedException();
            }

            static readonly Dictionary<string, Platforms.BuildTarget> s_UnknownBuildTargetsCache = new Dictionary<string, Platforms.BuildTarget>();

            public static Platforms.BuildTarget FindBuildTargetByName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return null;

                var buildTarget = Platforms.BuildTarget.AvailableBuildTargets.FirstOrDefault(x => x.GetBeeTargetName() == name);
                if (buildTarget == null)
                {
                    if (!s_UnknownBuildTargetsCache.TryGetValue(name, out buildTarget))
                    {
                        buildTarget = new UnknownBuildTarget(name);
                        s_UnknownBuildTargetsCache.Add(name, buildTarget);
                    }
                }
                return buildTarget;
            }
        }
    }
}
