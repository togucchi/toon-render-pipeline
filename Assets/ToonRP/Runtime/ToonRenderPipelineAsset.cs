using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Toguchi.Rendering
{
    [ExecuteInEditMode]
    [CreateAssetMenu(menuName = "ToonRenderPipelineAsset")]
    public class ToonRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField]
        private float modelRenderResolutionRate = 0.7f;
        public float ModelRenderResolutionRate => modelRenderResolutionRate;

        [SerializeField]
        private bool useReflection = true;
        public bool UseReflection => useReflection;

        [SerializeField]
        private PlanarReflection.ReflectionSettings reflectionSettings;
        public PlanarReflection.ReflectionSettings ReflectionSettings => reflectionSettings;

        protected override RenderPipeline CreatePipeline()
        {
            return new ToonRenderPipeline(this);
        }
    }
}