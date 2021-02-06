using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RuntimePipelineSwitcher : MonoBehaviour
{
    [SerializeField]
    private RenderPipelineAsset pipelineAsset;

    private RenderPipelineAsset renderPipelineAsset;

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.S))
        {
            if (renderPipelineAsset == null)
            {
                renderPipelineAsset = pipelineAsset;
            }
            else
            {
                renderPipelineAsset = null;
            }

            GraphicsSettings.renderPipelineAsset = renderPipelineAsset;
        }
    }
}
