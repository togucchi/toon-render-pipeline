using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace Toguchi.Rendering
{
    public class ToonRPCreator
    {
        [MenuItem("Assets/Create/ToonRPAsset")]
        public static void CreateToonRP()
        {
            var instance = ScriptableObject.CreateInstance<ToonRenderPipelineAsset>();
            AssetDatabase.CreateAsset(instance, "Assets/ToonRenderPipeline.asset");
            GraphicsSettings.renderPipelineAsset = instance;
        }
    }
}