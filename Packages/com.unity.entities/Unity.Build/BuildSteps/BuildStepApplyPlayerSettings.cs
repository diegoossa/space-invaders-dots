using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Unity.Build
{
    [BuildStep(description = kDescription, category = "Classic")]
    class BuildStepApplyPlayerSettings : IBuildStep
    {
        const string kDescription = "Apply Player Settings";
        public string Description => kDescription;
        public bool IsEnabled(BuildContext context) => true;

        public bool RunStep(BuildContext context)
        {
            var buildSettings = context.BuildSettings;
            var generalSettings = buildSettings.GetComponent<GeneralSettings>();
            var playerSettingsType = typeof(PlayerSettings);
            var editorUserBuildSettingsType = typeof(EditorUserBuildSettings);
            var storedSettings = context.GetOrCreate<StoredPlayerSettings>();
            storedSettings.SetValue(playerSettingsType.GetProperty(nameof(PlayerSettings.productName)), generalSettings.ProductName);
            storedSettings.SetValue(playerSettingsType.GetProperty(nameof(PlayerSettings.companyName)), generalSettings.CompanyName);
            var buildTarget = buildSettings.ClassicBuildProfile.Target;

            switch (buildTarget)
            {
                case BuildTarget.Android:
                {
                    var playerSettingsAndroidType = typeof(PlayerSettings.Android);
                    var androidSettings = buildSettings.GetComponent<AndroidSettings>();
                    AndroidBuildType androidBuildType;
                    switch (buildSettings.ClassicBuildProfile.Configuration)
                    {
                        case BuildConfiguration.Debug: androidBuildType = AndroidBuildType.Debug; break;
                        case BuildConfiguration.Develop: androidBuildType = AndroidBuildType.Development; break;
                        case BuildConfiguration.Release: androidBuildType = AndroidBuildType.Release; break;
                        default: throw new NotImplementedException("AndroidBuildType");
                    }

                    storedSettings.SetValue(editorUserBuildSettingsType.GetProperty(nameof(EditorUserBuildSettings.androidBuildType)), androidBuildType);
                    storedSettings.SetValue(playerSettingsAndroidType.GetProperty(nameof(PlayerSettings.Android.targetArchitectures)), androidSettings.targetArchitectures);
                    storedSettings.SetValue(playerSettingsType.GetMethod(nameof(PlayerSettings.SetApplicationIdentifier)),
                        new object[] { BuildTargetGroup.Android, androidSettings.packageName },
                        new object[] { BuildTargetGroup.Android, PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) });
                }
                break;
            }

            return true;
        }
        public void CleanupStep(BuildContext context) { }
    }

    class StoredPlayerSettings
    {
        readonly Dictionary<PropertyInfo, object> m_StoredPropertyValues = new Dictionary<PropertyInfo, object>();
        readonly Dictionary<MethodInfo, object[]> m_StoredMethodValues = new Dictionary<MethodInfo, object[]>();

        public void SetValue(PropertyInfo propertyInfo, object value)
        {
            if (m_StoredPropertyValues.ContainsKey(propertyInfo))
                throw new Exception($"Property {propertyInfo.Name} was already saved");
            m_StoredPropertyValues[propertyInfo] = propertyInfo.GetValue(null);
            propertyInfo.SetValue(null, value);
        }

        public void SetValue(MethodInfo methodInfo, object[] newValues, object[] oldValues)
        {
            if (m_StoredMethodValues.ContainsKey(methodInfo))
                throw new Exception($"Property {methodInfo.Name} was already saved");
            m_StoredMethodValues[methodInfo] = oldValues;
            methodInfo.Invoke(null, newValues);
        }

        public void RestoreValues()
        {
            foreach (var v in m_StoredPropertyValues)
            {
                v.Key.SetValue(null, v.Value);
            }
            m_StoredPropertyValues.Clear();

            foreach (var v in m_StoredMethodValues)
            {
                v.Key.Invoke(null, v.Value);
            }
            m_StoredMethodValues.Clear();
        }

    }
}
