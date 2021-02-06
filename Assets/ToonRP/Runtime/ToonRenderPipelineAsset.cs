using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Toguchi.Rendering
{
    [ExecuteInEditMode]
    public class ToonRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField]
        private float modelRenderResolutionRate = 0.7f;
        public float ModelRenderResolutionRate => modelRenderResolutionRate;

        protected override RenderPipeline CreatePipeline()
        {
            return new ToonRenderPipeline(this);
        }
    }
}