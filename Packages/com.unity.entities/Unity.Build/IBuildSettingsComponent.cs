namespace Unity.Build
{
    /// <summary>
    /// Defines the settings used throughout a <see cref="BuildPipeline"/>.
    /// Base interface for all <see cref="BuildSettings"/> components.
    /// </summary>
    public interface IBuildSettingsComponent
    {
        /// <summary>
        /// To be removed, do not use.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// To be removed, do not use.
        /// </summary>
        bool OnGUI();
    }
}
