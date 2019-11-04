using UnityEngine;
using UnityEngine.Events;
using UnityEditor;
using BuildTarget = Unity.Platforms.BuildTarget;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.AnimatedValues;
using System.IO;
using UnityEditor.Experimental.AssetImporters;

namespace Unity.Build
{
    [CustomEditor(typeof(BuildSettingsScriptedImporter), true)]
    public class BuildSettingsInspector : ScriptedImporterEditor
    {
        private BuildSettingsScriptedImporter m_Importer;
        private BuildSettings m_Asset;
        private bool m_IsModified;

        public static class Styles
        {
            internal static GUIContent kIconToolbarMinus = EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove from list");
        }

        public class BuildSettingsInfo
        {
            AnimBool m_ShowExtraFields;

            public BuildSettingsInfo(UnityAction repaintCallback)
            {
                m_ShowExtraFields = new AnimBool(true);
                m_ShowExtraFields.valueChanged.AddListener(repaintCallback);
            }

            public AnimBool ShowExtraFields
            {
                get
                {
                    return m_ShowExtraFields;
                }
            }
        }

        private Dictionary<Type, BuildSettingsInfo> m_BuildSettingsInfo = new Dictionary<Type, BuildSettingsInfo>();
        private Type[] m_AvailableBuildSettings;
        private Rect m_AddBuildComponentRect;
        private Rect m_AddBuildDependencyRect;
        private Rect m_SelectPipelineRect;

        private BuildSettingsInfo GetBuildSettingsInfo(IBuildSettingsComponent settings)
        {
            BuildSettingsInfo info;
            if (m_BuildSettingsInfo.TryGetValue(settings.GetType(), out info))
                return info;
            info = new BuildSettingsInfo(Repaint);
            m_BuildSettingsInfo[settings.GetType()] = info;
            return info;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            m_IsModified = false;
            m_Importer = target as BuildSettingsScriptedImporter;
            m_Asset = AssetDatabase.LoadAssetAtPath<BuildSettings>(m_Importer.assetPath);
            m_AvailableBuildSettings = TypeCache.GetTypesDerivedFrom<IBuildSettingsComponent>().ToArray();
        }

        public override bool showImportedObject { get { return false; } }

        private Type[] GetFilteredBuildSettings(List<IBuildSettingsComponent> components)
        {
            var currentBuildSettingsTypes = components.Select(m => m.GetType());
            var settings = new List<Type>();
            foreach (var s in m_AvailableBuildSettings)
            {
                if (currentBuildSettingsTypes.Contains(s))
                    continue;
                settings.Add(s);
            }
            return settings.ToArray();
        }

        public override void OnInspectorGUI()
        {
            var cbs = (BuildSettings)m_Asset;
            var components = cbs.GetComponents();

            // TODO GetComponents creates a copy, probably not efficient
            foreach (var component in components)
            {
                if (!(component is IBuildSettingsComponent))
                    continue;
                var b = (IBuildSettingsComponent)component;

                var info = GetBuildSettingsInfo(b);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                bool isInherited = cbs.IsComponentInherited(b.GetType());
                bool isOverriden = cbs.IsComponentOverridden(b.GetType());
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(b.Name + (isInherited ? "(Inherited)" : ""), EditorStyles.boldLabel))
                    info.ShowExtraFields.target = !info.ShowExtraFields.target;

                if (isInherited)
                {
                    if (GUILayout.Button("Override", GUILayout.ExpandWidth(false)))
                    {
                        var copyComponent = cbs.GetComponent(b.GetType());
                        cbs.SetComponent(copyComponent); 
                        m_IsModified = true;
                        GUIUtility.ExitGUI();
                    }
                }

                var title = "Remove";
                if (isInherited) title = "Remove Dependency";
                else if (isOverriden) title = "Remove Override";

                if (!isInherited)
                {
                    if (GUILayout.Button(title, GUILayout.ExpandWidth(false)))
                    {
                        cbs.RemoveComponent(b.GetType());
                        m_IsModified = true;
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginDisabledGroup(isInherited);
                if (EditorGUILayout.BeginFadeGroup(info.ShowExtraFields.faded))
                {
                    EditorGUI.indentLevel++;

                    if (b.OnGUI())
                    {
                        cbs.SetComponent(b.GetType(), b);
                        m_IsModified = true;
                        GUIUtility.ExitGUI();
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFadeGroup();
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();


            if (GUILayout.Button("Add Build Component"))
            {
                var filteredSettings = GetFilteredBuildSettings(components);
                PopupWindow.Show(m_AddBuildComponentRect, new CustomSearchDropDown<Type, object>(
                    () => filteredSettings,
                    (m) => m.Name,
                    (o, search) => o.Name.ToLower().Contains(search),
                    (t, unused) =>
                    {
                        m_Asset.SetComponent(t, (IBuildSettingsComponent)Activator.CreateInstance(t));
                        m_IsModified = true;
                    },
                    new Vector2(200, 300), null));
            }
            if (Event.current.type == EventType.Repaint)
                m_AddBuildComponentRect = GUILayoutUtility.GetLastRect();


            EditorGUILayout.Space();

            if (GUILayout.Button("Add Build Dependency"))
            {
                // TODO: don't allow recursive dependency add
                var buildDependencies = Resources.FindObjectsOfTypeAll<BuildSettings>().Where(asset => asset != cbs && !cbs.GetDependencies().Contains(asset));
                PopupWindow.Show(m_AddBuildDependencyRect, new CustomSearchDropDown<BuildSettings, object>(
                    () => buildDependencies,
                    (dependency) => dependency.name,
                    (dependency, search) => dependency.name.ToLower().Contains(search),
                    (dependency, unused) =>
                    {
                        cbs.AddDependency(dependency);
                        m_IsModified = true;
                    },
                    new Vector2(200, 300), null));
            }
            if (Event.current.type == EventType.Repaint)
                m_AddBuildDependencyRect = GUILayoutUtility.GetLastRect();

            EditorGUILayout.Space();

            foreach (var d in cbs.GetDependencies())
            {
                if (GUILayout.Button("Remove Dependency " + d.name))
                {
                    cbs.RemoveDependency(d);
                    m_IsModified = true;
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.Space();
            var prevEnabled = GUI.enabled;
            GUI.enabled = true;
            EditorGUI.BeginDisabledGroup(!cbs.CanBuild());
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Build"))
                    {
                        cbs.Build();
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("Build and Run"))
                    {
                        if (cbs.Build().Success)
                            cbs.Run();
                        GUIUtility.ExitGUI();
                    }

                    EditorGUI.BeginDisabledGroup(!cbs.CanRun());
                    {
                        if (GUILayout.Button("Run"))
                        {
                            cbs.Run();
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(!cbs.IsRunning());
                    {
                        if (GUILayout.Button("Stop"))
                        {
                            cbs.StopRunning();
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.EndDisabledGroup();
            GUI.enabled = prevEnabled;
            ApplyRevertGUI();
        }

        public override bool HasModified()
        {
            return m_IsModified;
        }

        protected override void ResetValues()
        {
            if (m_Importer != null)
                BuildSettings.DeserializeFromPath(m_Asset, m_Importer.assetPath);

            m_IsModified = false;
        }

        protected override void Apply()
        {
            m_Asset.SerializeToFile(m_Importer.assetPath);
            m_IsModified = false;
        }

        protected override bool OnApplyRevertGUI()
        {
            using (new EditorGUI.DisabledScope(!HasModified()))
            {
                RevertButton();
                return ApplyButton();
            }
        }
    }
}
