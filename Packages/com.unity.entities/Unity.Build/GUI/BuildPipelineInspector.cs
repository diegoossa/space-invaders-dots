using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.Build
{
    [CustomEditor(typeof(BuildPipeline), true)]
    class BuildPipelineInspector : Editor
    {
        BuildPipeline pipeline;
        ReorderableList stepList;
        List<IBuildStep> steps = new List<IBuildStep>();
        bool isModified = false;

        void MarkDirty()
        {
            isModified = true;
        }
        void Apply()
        {
            var path = AssetDatabase.GetAssetPath(pipeline);
            pipeline.SetSteps(steps);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.ForceReserializeAssets(new string[] { path }, ForceReserializeAssetsOptions.ReserializeAssets);
            isModified = false;
        }

        void Revert()
        {
            steps.Clear();
            if (!pipeline.GetSteps(steps))
                Debug.LogErrorFormat("Failed to get build steps from pipeline {0}", name);

            isModified = false;
        }

        private void OnEnable()
        {
            pipeline = (target as BuildPipeline);
            Revert();
            stepList = new ReorderableList(steps, typeof(IBuildStep), true, true, true, true);
            stepList.onAddDropdownCallback = AddDropdownCallbackDelegate;
            stepList.drawElementCallback = ElementCallbackDelegate;
            stepList.drawHeaderCallback = HeaderCallbackDelegate;
            stepList.onReorderCallback = ReorderCallbackDelegate;
            stepList.onRemoveCallback = RemoveCallbackDelegate;
            stepList.drawFooterCallback = FooterCallbackDelegate;
            stepList.drawNoneElementCallback = DrawNoneElementCallback;
        }

        private void OnDisable()
        {
            if (isModified)
            {
                if (EditorUtility.DisplayDialog("Unapplied Changes Detected", AssetDatabase.GetAssetPath(pipeline), "Apply", "Revert"))
                    Apply();
            }
        }

        static string GetDisplayName(Type t)
        {
            var attr = t.GetCustomAttribute<BuildStepAttribute>();
            var name = (attr == null || string.IsNullOrEmpty(attr.description)) ? t.Name : attr.description;
            var cat = (attr == null || string.IsNullOrEmpty(attr.category)) ? string.Empty : attr.category;
            if (string.IsNullOrEmpty(cat))
                return name;
            return $"{cat}/{name}";
        }

        static string GetCategory(Type t)
        {
            if (t == null)
                return string.Empty;
            var cat = t.GetCustomAttribute<BuildStepAttribute>()?.category;
            if (cat == null)
                return string.Empty;
            return cat;
        }

        static bool IsShown(Type t)
        {
            var flags = t.GetCustomAttribute<BuildStepAttribute>()?.flags;
            return (flags & BuildStepAttribute.Flags.Hidden) != BuildStepAttribute.Flags.Hidden;
        }

        void AddStep(Type t, object unused)
        {
            if (t != null)
            {
                steps.Add(BuildPipeline.CreateStepFromType(t));
                MarkDirty();
            }
        }

        static bool FilterSearch(Type t, string searchString)
        {
            if (t == null && !string.IsNullOrEmpty(searchString))
                return false;
            return GetDisplayName(t).ToLower().Contains(searchString.ToLower());
        }

        bool OnFooter(Rect r)
        {
            if (!GUI.Button(r, new GUIContent("Browse...")))
                return true;

            var selPath = EditorUtility.OpenFilePanel("Select Build Pipeline Step Asset", "Assets", "asset");
            if (string.IsNullOrEmpty(selPath))
                return true;

            var assetsPath = Application.dataPath;
            if (!selPath.StartsWith(assetsPath))
            {
                Debug.LogErrorFormat("Assets are required to be in the Assets folder.");
                return false;
            }

            var relPath = "Assets" + selPath.Substring(assetsPath.Length);
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relPath);
            if (obj == null)
            {
                Debug.LogErrorFormat("Unable to load asset at path {0}.", selPath);
                return false;
            }
            var step = obj as IBuildStep;
            if (step == null)
            {
                Debug.LogErrorFormat("Asset at path {0} is not a valid IBuildStep.", selPath);
                return false;
            }

            if (step == (IBuildStep)pipeline)
            {
                Debug.LogErrorFormat("IBuildStep at path {0} cannot be added to itself.", selPath);
                return false;
            }

            steps.Add(step);
            MarkDirty();
            return false;
        }

        void AddDropdownCallbackDelegate(Rect buttonRect, ReorderableList list)
        {
            var steps = new List<Type>();
            BuildPipeline.GetAvailableSteps(steps, t => IsShown(t));
            var height = Mathf.Min(600, (steps.Count + 4) * (EditorGUIUtility.singleLineHeight));
            PopupWindow.Show(buttonRect, new CustomSearchDropDown<Type, object>(() => steps, GetDisplayName, FilterSearch, AddStep, new Vector2(300, height), null, OnFooter));
        }

        void HandleDragDrop(Rect rect, int index)
        {
            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.ContextClick:

                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!rect.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (IBuildStep step in DragAndDrop.objectReferences)
                        {
                            steps.Insert(index, step);
                            MarkDirty();
                        }
                    }
                    break;
            }
        }

        void DrawNoneElementCallback(Rect rect)
        {
            ReorderableList.defaultBehaviours.DrawNoneElement(rect, false);
            HandleDragDrop(rect, 0);
        }

        void FooterCallbackDelegate(Rect rect)
        {
            ReorderableList.defaultBehaviours.DrawFooter(rect, stepList);
            HandleDragDrop(rect, steps.Count);
        }

        void ElementCallbackDelegate(Rect rect, int index, bool isActive, bool isFocused)
        {
            GUI.Label(rect, steps[index].Description);
            HandleDragDrop(rect, index);
        }

        void ReorderCallbackDelegate(ReorderableList list)
        {
            MarkDirty();
        }

        void HeaderCallbackDelegate(Rect rect)
        {
            GUI.Label(rect, new GUIContent("Build Steps"));
            HandleDragDrop(rect, 0);
        }

        void RemoveCallbackDelegate(ReorderableList list)
        {
            steps.RemoveAt(list.index);
            MarkDirty();
        }

        public override void OnInspectorGUI()
        {
            stepList.DoLayoutList();
            GUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(!isModified);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Revert"))
                        Revert();
                    if (GUILayout.Button("Apply"))
                        Apply();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}