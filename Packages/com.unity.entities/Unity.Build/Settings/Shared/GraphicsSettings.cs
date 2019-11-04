using UnityEditor;
using UnityEngine;
namespace Unity.Build
{
    internal class GraphicsSettings : IBuildSettingsComponent
    {
        public string Name
        {
            get
            {
                return "Graphics Settings";
            }
        }

        ColorSpace m_ColorSpace;

        public bool OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            m_ColorSpace = (ColorSpace)EditorGUILayout.EnumPopup("Color space", m_ColorSpace);
            return EditorGUI.EndChangeCheck();
        }
    }
}