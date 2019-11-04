using System;
using UnityEditor;
using Unity.Properties;

namespace Unity.Build
{
    internal class GeneralSettings : IBuildSettingsComponent
    {
        string m_ProductName;
        string m_CompanyName;

        [Property]
        public string ProductName
        {
            get => m_ProductName ?? "Default Product";
            set => m_ProductName = value;
        }

        [Property]
        public string CompanyName
        {
            get => m_CompanyName ?? "Default Company";
            set => m_CompanyName = value;
        }

        public string Name => "General Settings";
        public bool OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            ProductName = EditorGUILayout.TextField("Product Name", ProductName);
            CompanyName = EditorGUILayout.TextField("Company Name", CompanyName);
            return EditorGUI.EndChangeCheck();
        }
    }
}