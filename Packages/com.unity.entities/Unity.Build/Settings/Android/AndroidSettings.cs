using System;
using UnityEditor;
using Unity.Properties;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Build
{
    // Todo: should live inside com.unity.platforms.android
    internal class AndroidSettings : IBuildSettingsComponent
    {
        public string Name
        {
            get
            {
                return "Android Settings";
            }
        }

        [Property]
        private string m_PackageName;
        [Property]
        private int m_MinAPILevel;
        [Property]
        private int m_TargetAPILevel;
        [Property]
        private AndroidArchitecture m_TargetArchitectures;

        readonly Dictionary<int, string> kAndroidCodeNames = new Dictionary<int, string>
        {
            { 19, "Android 4.4 'KitKat' (API level 19)" },
            { 20, "Android 4.4W 'KitKat' (API level 20)" },
            { 21, "Android 5.0 'Lollipop' (API level 21)" },
            { 22, "Android 5.1 'Lollipop' (API level 22)" },
            { 23, "Android 6.0 'Marshmallow' (API level 23)" },
            { 24, "Android 7.0 'Nougat' (API level 24)" },
            { 25, "Android 7.1 'Nougat' (API level 25)" },
            { 26, "Android 8.0 'Oreo' (API level 26)" },
            { 27, "Android 8.1 'Oreo' (API level 27)" },
            { 28, "Android 9.0 'Pie' (API level 28)" },
        };


        public string packageName
        {
            set
            {
                m_PackageName = value;
            }

            get
            {
                if (string.IsNullOrEmpty(m_PackageName))
                    return "com.unity.DefaultPackage";
                return m_PackageName;
            }
        }

        public int minAPILevel
        {
            set
            {
                m_MinAPILevel = value;
            }

            get
            {
                if (!kAndroidCodeNames.ContainsKey(m_MinAPILevel))
                    m_MinAPILevel = kAndroidCodeNames.Keys.First();
                return m_MinAPILevel;
            }
        }

        public int targetAPILevel
        {
            set
            {
                m_TargetAPILevel = value;
            }

            get
            {
                if (!kAndroidCodeNames.ContainsKey(m_TargetAPILevel))
                    m_TargetAPILevel = kAndroidCodeNames.Keys.First();
                return m_TargetAPILevel;
            }
        }
        public AndroidArchitecture targetArchitectures
        {
            set
            {
                m_TargetArchitectures = value;
            }

            get
            {
                return m_TargetArchitectures;
            }
        }

        private int APILevelToDictionaryIndex(int apiLevel)
        {
            var levels = kAndroidCodeNames.Keys.ToArray();
            for (int i = 0; i < levels.Length; i++)
            {
                if (apiLevel == levels[i])
                    return i;
            }
            return 0;
        }

        private int DictionaryIndexToAPILevel(int index)
        {
            var levels = kAndroidCodeNames.Keys.ToArray();
            if (index >= 0 && index < levels.Length)
                return levels[index];
            return levels[0];
        }

        public bool OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            packageName = EditorGUILayout.TextField("Package Name", packageName);
            minAPILevel = DictionaryIndexToAPILevel(EditorGUILayout.Popup("Min API Level", APILevelToDictionaryIndex(minAPILevel), kAndroidCodeNames.Values.ToArray()));
            targetAPILevel = DictionaryIndexToAPILevel(EditorGUILayout.Popup("Target API Level", APILevelToDictionaryIndex(targetAPILevel), kAndroidCodeNames.Values.ToArray()));
            EditorGUILayout.LabelField("Target Architectures");

            var newTargetArchitectures = AndroidArchitecture.None;
            foreach (var t in (AndroidArchitecture[])Enum.GetValues(typeof(AndroidArchitecture)))
            {
                if (t == AndroidArchitecture.None || t == AndroidArchitecture.All)
                    continue;

                if (EditorGUILayout.Toggle(t.ToString(), (targetArchitectures & t) != 0))
                    newTargetArchitectures |= t;
            }
            targetArchitectures = newTargetArchitectures;

            return EditorGUI.EndChangeCheck();
        }
    }
}