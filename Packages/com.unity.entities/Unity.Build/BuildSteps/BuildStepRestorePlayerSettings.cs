namespace Unity.Build
{
    [BuildStep(description = kDescription, category = "Classic")]
    class BuildStepRestorePlayerSettings : IBuildStep
    {
        const string kDescription = "Restore Player Settings";
        public string Description => kDescription;
        public bool IsEnabled(BuildContext context) => true;

        public bool RunStep(BuildContext context)
        {
            var stored = context.Get<StoredPlayerSettings>();
            if (stored == null)
                return false;
            stored.RestoreValues();
            return true;
        }
        public void CleanupStep(BuildContext context) { }
    }
}
