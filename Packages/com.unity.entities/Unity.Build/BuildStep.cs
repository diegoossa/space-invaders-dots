using System;

namespace Unity.Build
{
    /// <summary>
    /// Attribute for hiding build steps from the GUI and specifying a display name with only a Type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class BuildStepAttribute : Attribute
    {
        /// <summary>
        /// Flags types for build steps.
        /// </summary>
        public enum Flags
        {
            None = 0,
            Hidden = 1
        }
        /// <summary>
        /// Flags for the build step.
        /// </summary>
        public Flags flags = Flags.None;
        /// <summary>
        /// Description name for type.  If set, this will be used instead of the class name when selecting new steps in the GUI.
        /// </summary>
        public string description = "";
        /// <summary>
        /// Optional category used to put build step in its own sub menu when selectiong from the dropdown.
        /// </summary>
        public string category = "";
    }

    /// <summary>
    /// Interface for a build step.
    /// </summary>
    public interface IBuildStep
    {
        /// <summary>
        /// Description of the <see cref="IBuildStep" />. This is used in build progress reporting.
        /// </summary>
        string Description { get; }
        /// <summary>
        /// Whether or not the <see cref="IBuildStep" /> will be executed by the <see cref="BuildPipeline" />.
        /// </summary>
        /// <param name="context">The <see cref="BuildContext" /> used for the <see cref="BuildPipeline" /> execution.</param>
        /// <returns>
        /// <see langword="true" /> if the <see cref="IBuildStep" /> will be executed, otherwise <see langword="false" />.
        /// </returns>
        bool IsEnabled(BuildContext context);
        /// <summary>
        /// Execute the <see cref="IBuildStep" />.
        /// </summary>
        /// <param name="context">The <see cref="BuildContext" /> used for the <see cref="BuildPipeline" /> execution.</param>
        /// <returns>
        /// <see langword="true" /> if the <see cref="IBuildStep" /> execution was successful, otherwise <see langword="false" />.
        /// </returns>
        bool RunStep(BuildContext context);
        /// <summary>
        /// Cleans up the results of Executing the <see cref="IBuildStep" />.
        /// </summary>
        /// <param name="context">The <see cref="BuildContext" /> used for the <see cref="BuildPipeline" /> cleanup.</param>
        void CleanupStep(BuildContext context);
    }

    /// <summary>
    /// Holds various statistics about a <see cref="IBuildStep"/> execution.
    /// </summary>
    public struct BuildStepStatistics
    {
        /// <summary>
        /// The index order for which the <see cref="IBuildStep"/> was executed.
        /// </summary>
        public uint Index { get; internal set; }

        /// <summary>
        /// The description of the <see cref="IBuildStep"/>.
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// The total duration of the <see cref="IBuildStep"/>.
        /// </summary>
        public TimeSpan Duration { get; internal set; }

        /// <summary>
        /// Get the <see cref="BuildStepStatistics"/> as a string that can be used for logging.
        /// </summary>
        /// <returns>The <see cref="BuildStepStatistics"/> as a string.</returns>
        public override string ToString()
        {
            return $"{Index}. {Description}: {Duration}";
        }
    }
}
