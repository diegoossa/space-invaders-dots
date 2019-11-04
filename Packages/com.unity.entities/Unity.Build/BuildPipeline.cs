using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Build
{
    /// <summary>
    /// Contains a list of build steps to run in order
    /// </summary>
    [Serializable]
    [BuildStep(flags = BuildStepAttribute.Flags.Hidden)]
    public partial class BuildPipeline : ScriptableObject, IBuildStep
    {
        [SerializeField]
        List<string> serializedStepData = new List<string>();

        /// <summary>
        /// Overwrites all build steps.
        /// </summary>
        /// <param name="steps">The set of steps to overwrite with.</param>
        /// <returns>False if a step is unable to be converted to serialized step data.</returns>
        public bool SetSteps(IEnumerable<IBuildStep> steps)
        {
            serializedStepData.Clear();
            foreach (var s in steps)
                if (!AddStep(s))
                    return false;
            return true;
        }

        /// <summary>
        /// Get a build step by index.
        /// </summary>
        /// <param name="index">The step index.</param>
        /// <returns>The build step or null if the index is out of range.</returns>
        public IBuildStep GetStep(int index)
        {
            if (index < 0 || index >= serializedStepData.Count)
                return null;
            return CreateStepFromData(serializedStepData[index]);
        }

        /// <summary>
        /// Creates build steps from serialized data and adds to the specified list.  It is up to the caller to clear the list if desired.
        /// </summary>
        /// <param name="steps">The list to fill with steps.</param>
        /// <returns>True if all serialized steps were added, false if any failed to create from serialized data.</returns>
        public bool GetSteps(List<IBuildStep> steps)
        {
            foreach (var s in serializedStepData)
            {
                var step = CreateStepFromData(s);
                if (step == null)
                    return false;
                steps.Add(step);
            }
            return true;
        }

        /// <summary>
        /// Event sent when the build started.
        /// </summary>
        public static event Action<BuildContext> BuildStarted = delegate { };

        /// <summary>
        /// Event sent when the build completed.
        /// </summary>
        public static event Action<BuildResult> BuildCompleted = delegate { };

        /// <summary>
        /// Number of build steps.
        /// </summary>
        public int StepCount => serializedStepData.Count;

        /// <summary>
        /// The name of this pipeline, for IBuildStep interface.
        /// </summary>
        public string Description => $"Pipeline => {name}";

        /// <summary>
        /// Get the display text for a step.
        /// </summary>
        /// <param name="stepIndex">The setp index.</param>
        /// <returns>The display name of the step.</returns>
        public string GetStepDisplayName(int stepIndex)
        {
            return GetStep(stepIndex).Description;
        }

        /// <summary>
        /// Run the build pipeline.
        /// </summary>
        /// <param name="context">Context for running.</param>
        /// <returns>The result of the build.</returns>
        public BuildResult RunSteps(BuildContext context)
        {
            var prevPipeline = context.Get<BuildPipeline>();
            context.Remove<BuildPipeline>();
            context.Set(this);

            var steps = new List<IBuildStep>();
            if (!GetSteps(steps))
            {
                Debug.LogError($"Failed to get build steps from pipeline {name}");
                return new BuildResult();
            }
            var result = RunSteps(context, steps);
            if (prevPipeline != null)
            {
                context.Remove<BuildPipeline>();
                context.Set(prevPipeline);
            }
            return result;
        }
        public void CleanupStep(BuildContext context) { }

        /// <summary>
        /// Checks for the existence of a build step type.
        /// </summary>
        /// <typeparam name="T">The type of step.</typeparam>
        /// <returns>True if there is at least one step of this type.</returns>
        public bool HasStep<T>()
        {
            for (int i = 0; i < StepCount; i++)
            {
                var step = GetStep(i);
                if (typeof(T).IsAssignableFrom(step.GetType()))
                    return true;
                if (typeof(BuildPipeline).IsAssignableFrom(step.GetType()))
                {
                    var pipelineStep = step as BuildPipeline;
                    if (pipelineStep.HasStep<T>())
                        return true;
                }
            }
            return false;
        }


        /// <summary>
        /// Adds a build step to the pipeline.
        /// </summary>
        /// <param name="step">The step to add.</param>
        public bool AddStep(IBuildStep step)
        {
            if (step == null)
                return false;
            var data = CreateStepData(step);
            if (string.IsNullOrEmpty(data))
                return false;
            serializedStepData.Add(data);
            return true;
        }

        /// <summary>
        /// Adds a build step to the pipeline.
        /// </summary>
        /// <typeparam name="T">The type of the step to add.</typeparam>
        /// <returns>True if the step was added.</returns>
        public bool AddStep<T>() where T : IBuildStep
        {
            return AddStep(typeof(T));
        }

        /// <summary>
        /// Adds a build step to the pipeline.
        /// </summary>
        /// <param name="data">The step data.</param>
        /// <returns>True if the step was added.</returns>
        public bool AddStep(string data)
        {
            return AddStep(CreateStepFromData(data));
        }

        /// <summary>
        /// Adds a build step to the pipeline.
        /// </summary>
        /// <param name="type">The type of the step to add.</param>
        /// <returns>True if the step was added.</returns>
        public bool AddStep(Type type)
        {
            return AddStep(CreateStepFromType(type));
        }

        /// <summary>
        /// Adds a set of build steps by type.
        /// </summary>
        /// <param name="types"></param>
        public void AddSteps(params Type[] types)
        {
            foreach (var t in types)
                AddStep(t);
        }

        /// <summary>
        /// Run a set of build steps with a given context.  This can be used without a BuildPipeline object.
        /// </summary>
        /// <param name="context">The context for the execution.</param>
        /// <param name="steps">The set of steps to run.</param>
        /// <param name="logSteps">If true, each step is logged to the Editor console.</param>
        /// <returns>The result of the build.</returns>
        public static BuildResult RunSteps(BuildContext context, IEnumerable<IBuildStep> steps, bool logSteps = false)
        {
            var pipelineTimer = new System.Diagnostics.Stopwatch();
            pipelineTimer.Start();
            if (logSteps) Debug.Log($"Running {steps.Count()} Build Steps");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("Building is not allowed while Unity is compiling.");

            BuildStarted.Invoke(context);
            var progress = context.BuildProgress;
            var count = steps.Count();
            uint current = 0;
            var success = true;
            var stepTimer = new System.Diagnostics.Stopwatch();
            var stats = new List<BuildStepStatistics>();
            var currentPipeline = context.BuildPipeline;
            var name = currentPipeline != null ? currentPipeline.Description : progress?.Title;
            var cleanupSteps = new Stack<IBuildStep>();
            foreach (var buildStep in steps)
            {
                if (progress?.Update($"{name} (Step {current + 1} of {count})", buildStep.Description + "...", (float)current / (count * 2)) ?? false)
                {
                    success = false;
                    break;
                }

                try
                {
                    if (buildStep.IsEnabled(context))
                    {
                        cleanupSteps.Push(buildStep);
                        stepTimer.Restart();
                        success = buildStep.RunStep(context);
                        stepTimer.Stop();

                        stats.Add(new BuildStepStatistics
                        {
                            Index = current,
                            Description = buildStep.Description,
                            Duration = stepTimer.Elapsed
                        });
                    }
                    else
                    {
                        if (logSteps) Debug.Log($"Skipping disabled Step {current + 1} of {count}) - {buildStep.Description}");
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Build step '{buildStep.Description}' failed with exception: {exception}");
                    success = false;
                }
                if (logSteps) Debug.Log($"({current + 1}/{count}) - {buildStep.Description}, result = {success}, duration = {stepTimer.Elapsed}");
                current++;
                if (!success)
                    break;
            }
            if (logSteps) Debug.Log($"Running {cleanupSteps.Count} Cleanup Steps");
            count = cleanupSteps.Count; //actual number of steps that ran
            
            foreach (var buildStep in cleanupSteps)
            {
                var index = (count - (current - count));
                progress?.Update($"{name} Cleanup (Step {index}/{count})", buildStep.Description + "...", (float)current / (count * 2));
                try
                {
                    stepTimer.Restart();
                    buildStep.CleanupStep(context);
                    stepTimer.Stop();

                    stats.Add(new BuildStepStatistics
                    {
                        Index = (uint)index,
                        Description = buildStep.Description + " cleanup",
                        Duration = stepTimer.Elapsed
                    });
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Build step '{buildStep.Description}' cleanup failed with exception: {exception}");
                    success = false;
                }
                if (logSteps) Debug.Log($"({index}/{count}) - {buildStep.Description}, duration = {stepTimer.Elapsed}");
                current++;
            }
            var result = new BuildResult() { Duration = pipelineTimer.Elapsed, Context = context, Statistics = stats, Success = success };
            BuildCompleted(result);
            if (logSteps) Debug.Log($"Completed Pipeline with result {result}");
            return result;
        }

        /// <summary>
        /// Run a set of build steps with a given context.  This can be used without a BuildPipeline object.
        /// </summary>
        /// <param name="context">The context for the execution.</param>
        /// <param name="steps">The set of step types to run.</param>
        /// <returns>The result of the build.</returns>
        public static BuildResult RunSteps(BuildContext context, params Type[] types)
        {
            return RunSteps(context, types.Select(t => CreateStepFromType(t)));
        }

        /// <summary>
        /// Run a set of build steps with a given context.  This can be used without a BuildPipeline object.
        /// </summary>
        /// <param name="context">The context for the execution.</param>
        /// <param name="steps">The set of step data to run.</param>
        /// <returns>The result of the build.</returns>
        public static BuildResult RunSteps(BuildContext context, params string[] steps)
        {
            return RunSteps(context, steps.Select(s => CreateStepFromData(s)));
        }

        /// <summary>
        /// Creates serializable step data.
        /// </summary>
        /// <param name="step">The build step.</param>
        /// <returns>The string data for the step.</returns>
        public static string CreateStepData(IBuildStep step)
        {
            if (step == null)
                return null;
            var obj = step as UnityEngine.Object;
            if (obj != null)
            {
                string guid;
                long lfid;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out lfid))
                    return $"@{guid}";
                Debug.LogWarningFormat("IBuildSteps that are UnityEngine.Object must be saved to an asset before being added to a BuildPipeline.");
                return null;
            }
            return step.GetType().AssemblyQualifiedName;
        }

        /// <summary>
        /// Creates a build step from the serialized data.
        /// </summary>
        /// <param name="stepData">The step data to convert.</param>
        /// <returns>The created step.</returns>
        public static IBuildStep CreateStepFromData(string stepData)
        {
            if (string.IsNullOrEmpty(stepData))
            {
                Debug.LogWarning($"Invalid build step data {stepData}");
                return null;
            }
            if (stepData[0] == '@')
            {
                var path = AssetDatabase.GUIDToAssetPath(stepData.Substring(1));
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning($"Unable to determine asset path from step data '{stepData}'.");
                    return null;
                }
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) as IBuildStep;
            }
            return CreateStepFromType(Type.GetType(stepData));
        }

        /// <summary>
        /// Creates a build step from a given type.
        /// </summary>
        /// <param name="type">The build step type.</param>
        /// <returns>The created step.</returns>
        public static IBuildStep CreateStepFromType(Type type)
        {
            if (!typeof(IBuildStep).IsAssignableFrom(type))
            {
                Debug.LogWarning($"Invalid build step type {type}");
                return null;
            }
            return Activator.CreateInstance(type) as IBuildStep;
        }

        /// <summary>
        /// Creates a new BuildPipeline asset.
        /// </summary>
        /// <param name="pipelinePath">The asset path for the created pipepline.</param>
        /// <returns>The created pipeline object.</returns>
        public static BuildPipeline CreateNew(string pipelinePath)
        {
            var buildSettings = CreateInstance<BuildPipeline>();
            AssetDatabase.CreateAsset(buildSettings, pipelinePath);
            return AssetDatabase.LoadAssetAtPath<BuildPipeline>(pipelinePath);
        }

        /// <summary>
        /// Retrieves a list of valid types for build steps.
        /// </summary>
        /// <param name="results">The types of build steps that are available.</param>
        /// <param name="filter">Optional filter function for types.</param>
        public static void GetAvailableSteps(List<Type> results, Func<Type, bool> filter = null)
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<IBuildStep>())
            {
                if (t.IsAbstract || t.IsInterface)
                    continue;
                if (filter != null && !filter(t))
                    continue;
                results.Add(t);
            }
        }

        /// <summary>
        /// Is this build step enabled.
        /// </summary>
        /// <param name="context">The build context.</param>
        /// <returns>True if the step is  enabled.</returns>
        public bool IsEnabled(BuildContext context) => true;

        /// <summary>
        /// Implementation of IBuildStep interface.  Pipelines can be run as build steps.
        /// </summary>
        /// <param name="context">The context to run with.</param>
        public bool RunStep(BuildContext context)
        {
            //TODO: what about stats, etc?
            return RunSteps(context).Success;
        }
    }
}
