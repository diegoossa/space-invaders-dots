using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Build
{
    /// <summary>
    /// Component that contains a reference to a BuildPipeline asset.
    /// </summary>
    [Serializable]
    public class BuildPipelineComponent : IBuildSettingsComponent
    {
        //TODO: tried to use GUID cbut it is not serializable...
        /// <summary>
        /// The guid of the referenced pipeline asset.
        /// </summary>
        public string PipelineAsset;

        /// <summary>
        /// Accessor for the pipeline asset.
        /// </summary>
        public BuildPipeline Pipeline
        {
            get
            {
                if (string.IsNullOrEmpty(PipelineAsset))
                    return null;
                return AssetDatabase.LoadAssetAtPath<BuildPipeline>(AssetDatabase.GUIDToAssetPath(PipelineAsset));
            }
            set
            {
                if (value == null)
                {
                    PipelineAsset = null;
                }
                else
                {
                    var path = AssetDatabase.GetAssetPath(value);
                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.LogWarningFormat("Unable to determine asset path for object {0}.", value);
                        return;
                    }
                    var guidStr = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guidStr))
                    {
                        Debug.LogWarningFormat("Failed to get guid from asset path {0}.", path);
                        return;
                    }
                    PipelineAsset = guidStr;

//                    if (!GUID.TryParse(guidStr, out PipelineAsset))
//                        Debug.LogWarningFormat("Failed to parse guid {0} from asset path {1}.", guidStr, path);
                }
            }
        }

        public string Name => "BuildPipeline";

        public bool OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            var pipeline = (BuildPipeline)EditorGUILayout.ObjectField(Pipeline, typeof(BuildPipeline), false);
            if (EditorGUI.EndChangeCheck())
            {
                Pipeline = pipeline;
                return true;
            }
            
            return false;
        }
    }
}