using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Unity.Build
{
    /// <summary>
    /// Context object for setting the executable file path.
    /// </summary>
    public class ExecutableFile 
    {
        /// <summary>
        /// The file path.
        /// </summary>
        public FileInfo Path { get; set; }
    }

    /// <summary>
    /// Holds the results of the execution of a <see cref="BuildPipeline"/>.
    /// </summary>
    public struct BuildResult
    {
        /// <summary>
        /// <see langword="true"/> if the BuildPipeline completed sucessfully, <see langword="false"/> otherwise.
        /// </summary>
        public bool Success { get; internal set; }

        /// <summary>
        /// The build context used for the build.
        /// </summary>
        public BuildContext Context { get; internal set; }

        /// <summary>
        /// The total duration of the <see cref="BuildPipeline"/> execution.
        /// </summary>
        public TimeSpan Duration { get; internal set; }

        /// <summary>
        /// A list of <see cref="BuildStepStatistics"/> collected during the <see cref="BuildPipeline"/> execution for each <see cref="IBuildStep"/>.
        /// </summary>
        public IReadOnlyCollection<BuildStepStatistics> Statistics { get; internal set; }

        /// <summary>
        /// Get the <see cref="BuildResult"/> as a string that can be used for logging.
        /// </summary>
        /// <returns>The <see cref="BuildResult"/> as a string.</returns>
        public override string ToString()
        {
            var stats = string.Join(Environment.NewLine, Statistics.Select(step => step.ToString()));
            return $"Build {(Success ? "succeeded" : "failed")} after {Duration} in {Context.Get<ExecutableFile>()?.Path.Directory}{Environment.NewLine}{Environment.NewLine}{stats}";
        }
    }
}
