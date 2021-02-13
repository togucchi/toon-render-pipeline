using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

namespace Toguchi.Rendering
{
    public sealed class ToonRenderPipeline : RenderPipeline
    {
        private const int MAX_CAMERA_COUNT = 4;

        private const string FORWARD_SHADER_TAG = "ToonForward";
        private const string FORWARD_REFLECTION_SHADER_TAG = "ToonForwardReflection";

        private readonly int reflectionTextureId = Shader.PropertyToID("_ReflectionTex");

        private CommandBuffer commandBuffer;

        private ToonRenderPipelineAsset renderPipelineAsset;

        private CullingResults cullingResults;
        private CullingResults reflectionCullingResults;

        public static ToonRenderPipeline Instance;

        public static ToonRenderPipelineAsset Asset;

        public static System.Action<ScriptableRenderContext, CommandBuffer, Camera> OnBeforeRenderCamera;

        public enum RenderTextureType
        {
            ModelColor,
            ModelDepth,
            Reflection,

            Count,
        }

        private RenderTargetIdentifier[] renderTargetIdentifiers = new RenderTargetIdentifier[(int)RenderTextureType.Count];

        public ToonRenderPipeline(ToonRenderPipelineAsset asset)
        {
            renderPipelineAsset = asset;

            // CommandBufferの事前生成
            commandBuffer = new CommandBuffer();
            commandBuffer.name = "ToonRP";
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            // Set static asset
            Instance = this;
            Asset = renderPipelineAsset;

            for(int i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];

                // RenderTexture作成
                CreateRenderTexture(context, camera, commandBuffer);

                // カメラ描画前コールバック発火
                OnBeforeRenderCamera?.Invoke(context, commandBuffer, camera);

                // カメラプロパティ設定
                context.SetupCameraProperties(camera);

                // Culling
                if(!camera.TryGetCullingParameters(false, out var cullingParameters))
                {
                    continue;
                }
                cullingResults = context.Cull(ref cullingParameters);

                // モデル描画用RTのClear
                ClearModelRenderTexture(context, camera, commandBuffer);

                // ライト情報のセットアップ
                SetupLights(context, camera, commandBuffer);

                // 不透明オブジェクト描画
                DrawOpaque(context, camera, commandBuffer);

                // Skybox描画
                if(camera.clearFlags == CameraClearFlags.Skybox)
                {
                    context.DrawSkybox(camera);
                }

                // 半透明オブジェクト描画
                DrawTransparent(context, camera, commandBuffer);

                // CameraTargetに描画
                RestoreCameraTarget(context, commandBuffer);

#if UNITY_EDITOR
                // Gizmo
                if (UnityEditor.Handles.ShouldRenderGizmos())
                {
                    context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                }
#endif

                // PostProcessing

#if UNITY_EDITOR
                // Gizmo
                if (UnityEditor.Handles.ShouldRenderGizmos())
                {
                    context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
                }
#endif
                
                // RenderTexture解放
                ReleaseRenderTexture(context, commandBuffer);
            }

            context.Submit();
        }

        private void CreateRenderTexture(ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();

            var width = camera.targetTexture?.width ?? Screen.width;
            var height = camera.targetTexture?.height ?? Screen.height;

            var modelWidth = (int)((float)width * renderPipelineAsset.ModelRenderResolutionRate);
            var modelHeight = (int)((float)height * renderPipelineAsset.ModelRenderResolutionRate);

            commandBuffer.GetTemporaryRT((int)RenderTextureType.ModelColor, modelWidth, modelHeight, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            commandBuffer.GetTemporaryRT((int)RenderTextureType.ModelDepth, modelWidth, modelHeight, 16, FilterMode.Point, RenderTextureFormat.Depth);
            if(renderPipelineAsset.UseReflection)
            {
                var reflectionWidth = (int)(modelWidth / (float)renderPipelineAsset.ReflectionSettings.scale);
                var reflectionHeight = (int)(modelHeight / (float)renderPipelineAsset.ReflectionSettings.scale);

                commandBuffer.GetTemporaryRT((int)RenderTextureType.Reflection, reflectionWidth, reflectionHeight, 16, FilterMode.Bilinear, RenderTextureFormat.Default);
            }

            context.ExecuteCommandBuffer(commandBuffer);

            for(int i = 0; i < (int)RenderTextureType.Count; i++)
            {
                renderTargetIdentifiers[i] = new RenderTargetIdentifier(i);
            }
        }
        
        private void SetupLights(ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();

            // DirectionalLightの探索
            int lightIndex = -1;
            for(int i = 0; i < cullingResults.visibleLights.Length; i++)
            {
                var visibleLight = cullingResults.visibleLights[i];
                var light = visibleLight.light;
                
                
                if(light == null || light.shadows == LightShadows.None || light.shadowStrength <= 0f || light.type != LightType.Directional)
                {
                    continue;
                }

                lightIndex = i;
                break;
            }

            if(lightIndex < 0)
            {
                commandBuffer.DisableShaderKeyword("ENABLE_DIRECTIONAL_LIGHT");
                context.ExecuteCommandBuffer(commandBuffer);
                return;
            }

            // ライトのパラメータ設定
            {
                var visibleLight = cullingResults.visibleLights[lightIndex];
                var light = visibleLight.light;

                commandBuffer.EnableShaderKeyword("ENABLE_DIRECTIONAL_LIGHT");
                commandBuffer.SetGlobalColor("_LightColor", light.color * light.intensity);
                commandBuffer.SetGlobalVector("_LightVector", -light.transform.forward);
                context.ExecuteCommandBuffer(commandBuffer);
            }
        }

        private void ClearModelRenderTexture(ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();

            // RenderTarget設定
            commandBuffer.SetRenderTarget(renderTargetIdentifiers[(int)RenderTextureType.ModelColor], renderTargetIdentifiers[(int)RenderTextureType.ModelDepth]);

            if(camera.clearFlags == CameraClearFlags.Depth || camera.clearFlags == CameraClearFlags.Skybox)
            {
                commandBuffer.ClearRenderTarget(true, false, Color.black, 1.0f);
            }
            else if(camera.clearFlags == CameraClearFlags.SolidColor)
            {
                commandBuffer.ClearRenderTarget(true, true, camera.backgroundColor, 1.0f);
            }

            context.ExecuteCommandBuffer(commandBuffer);
        }

        private void DrawOpaque(ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();

            commandBuffer.SetRenderTarget(renderTargetIdentifiers[(int)RenderTextureType.ModelColor], renderTargetIdentifiers[(int)RenderTextureType.ModelDepth]);
            context.ExecuteCommandBuffer(commandBuffer);

            // Filtering, Sort
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var settings = new DrawingSettings(new ShaderTagId(FORWARD_SHADER_TAG), sortingSettings);
            var filterSettings = new FilteringSettings(
                new RenderQueueRange(0, (int)RenderQueue.GeometryLast),
                camera.cullingMask
                );

            // Rendering
            context.DrawRenderers(cullingResults, ref settings, ref filterSettings);
        }

        private void DrawTransparent(ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();

            commandBuffer.SetRenderTarget(renderTargetIdentifiers[(int)RenderTextureType.ModelColor], renderTargetIdentifiers[(int)RenderTextureType.ModelDepth]);
            context.ExecuteCommandBuffer(commandBuffer);

            // Filtering, Sort
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent };
            var settings = new DrawingSettings(new ShaderTagId(FORWARD_SHADER_TAG), sortingSettings);
            var filterSettings = new FilteringSettings(
                new RenderQueueRange((int)RenderQueue.GeometryLast, (int)RenderQueue.Transparent),
                camera.cullingMask
                );

            // 描画
            context.DrawRenderers(cullingResults, ref settings, ref filterSettings);
        }

        private void RestoreCameraTarget(ScriptableRenderContext context, CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();

            var cameraTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);

            commandBuffer.SetRenderTarget(cameraTarget);
            commandBuffer.Blit(renderTargetIdentifiers[(int)RenderTextureType.ModelColor], cameraTarget);

            context.ExecuteCommandBuffer(commandBuffer);
        }

        private void ReleaseRenderTexture(ScriptableRenderContext context, CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();

            for(int i = 0; i < (int)RenderTextureType.Count; i++)
            {
                commandBuffer.ReleaseTemporaryRT(i);
            }

            context.ExecuteCommandBuffer(commandBuffer);
        }

        #region Refleciton

        public void RenderRefletion(ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
        {
            // カメラプロパティ設定
            context.SetupCameraProperties(camera);

            // Culling
            if (!camera.TryGetCullingParameters(false, out var cullingParameters))
            {
                Debug.Log("Failed culling");
                return;
            }
            reflectionCullingResults = context.Cull(ref cullingParameters);

            commandBuffer.Clear();
            commandBuffer.SetRenderTarget(renderTargetIdentifiers[(int)RenderTextureType.Reflection]);
            if (camera.clearFlags == CameraClearFlags.Depth || camera.clearFlags == CameraClearFlags.Skybox)
            {
                commandBuffer.ClearRenderTarget(true, false, Color.black, 1.0f);
            }
            else if (camera.clearFlags == CameraClearFlags.SolidColor)
            {
                commandBuffer.ClearRenderTarget(true, true, camera.backgroundColor, 1.0f);
            }
            context.ExecuteCommandBuffer(commandBuffer);

            // 不透明オブジェクト描画
            DrawOpaqueReflection(context, camera, commandBuffer);

            // Skybox描画
            if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                context.DrawSkybox(camera);
            }

            // 半透明オブジェクト描画
            DrawTransparentReflection(context, camera, commandBuffer);

            // ExecuteCommandBuffer
            commandBuffer.Clear();
            commandBuffer.SetGlobalTexture(reflectionTextureId, new RenderTargetIdentifier((int)ToonRenderPipeline.RenderTextureType.Reflection));
            context.ExecuteCommandBuffer(commandBuffer);
        }

        private void DrawOpaqueReflection(ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();

            commandBuffer.SetRenderTarget(renderTargetIdentifiers[(int)RenderTextureType.Reflection]);
            context.ExecuteCommandBuffer(commandBuffer);

            // Filtering, Sort
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var settings = new DrawingSettings(new ShaderTagId(FORWARD_REFLECTION_SHADER_TAG), sortingSettings);
            var filterSettings = new FilteringSettings(
                new RenderQueueRange(0, (int)RenderQueue.GeometryLast),
                camera.cullingMask
                );

            // Rendering
            context.DrawRenderers(reflectionCullingResults, ref settings, ref filterSettings);
        }

        private void DrawTransparentReflection(ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
        {
            commandBuffer.Clear();

            commandBuffer.SetRenderTarget(renderTargetIdentifiers[(int)RenderTextureType.Reflection]); 
            context.ExecuteCommandBuffer(commandBuffer);

            // Filtering, Sort
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent };
            var settings = new DrawingSettings(new ShaderTagId(FORWARD_REFLECTION_SHADER_TAG), sortingSettings);
            var filterSettings = new FilteringSettings(
                new RenderQueueRange((int)RenderQueue.GeometryLast, (int)RenderQueue.Transparent),
                camera.cullingMask
                );

            // 描画
            context.DrawRenderers(reflectionCullingResults, ref settings, ref filterSettings);
        }

        #endregion
    }
}