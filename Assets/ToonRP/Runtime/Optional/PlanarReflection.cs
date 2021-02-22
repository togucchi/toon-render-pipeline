using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace Toguchi.Rendering
{
    [ExecuteAlways]
    [Serializable]
    public class PlanarReflection : MonoBehaviour
    {
        [Serializable]
        public enum ReflectionScale
        {
            Full = 1,
            Half = 2,
            Third = 3,
            Quater = 4,
        }

        [Serializable]
        public class ReflectionSettings
        {
            public ReflectionScale scale = ReflectionScale.Third;
            public float clipPlaneOffset = 0.07f;
            public float blurPower = 0.05f;
        }

        private static Camera reflectionCamera;

        private void OnEnable()
        {
            ToonRenderPipeline.OnBeforeRenderCamera += ExecutePlanarReflection;
        }

        private void OnDisable()
        {
            CleanUp();
        }

        private void OnDestroy()
        {
            CleanUp();
        }

        private void CleanUp()
        {
            ToonRenderPipeline.OnBeforeRenderCamera -= ExecutePlanarReflection;

            if (reflectionCamera)
            {
                reflectionCamera.targetTexture = null;
                DestroySafety(reflectionCamera);
            }
        }

        private static void DestroySafety(UnityEngine.Object obj)
        {
            if(Application.isEditor)
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
            else
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        private void UpdateCamera(Camera source, Camera destination)
        {
            if(destination == null)
            {
                return;
            }

            destination.CopyFrom(source);
            destination.useOcclusionCulling = false;
        }

        private void UpdateReflectionCamera(Camera baseCamera)
        {
            if(reflectionCamera == null)
            {
                reflectionCamera = CreateReflectionCamera();
            }

            var pos = Vector3.zero;
            var normal = Vector3.up;

            UpdateCamera(baseCamera, reflectionCamera);

            var d = -Vector3.Dot(normal, pos) - ToonRenderPipeline.Asset.ReflectionSettings.clipPlaneOffset;
            var reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

            var reflection = CalculateReflectionMatrix(reflectionPlane);

            var oldPosition = baseCamera.transform.position - new Vector3(0f, pos.y * 2f, 0f);
            var newPosition = ReflectPosition(oldPosition);

            reflectionCamera.transform.forward = Vector3.Scale(baseCamera.transform.forward, new Vector3(1, -1, 1));
            reflectionCamera.worldToCameraMatrix = baseCamera.worldToCameraMatrix * reflection;

            var clipPlane = CameraSpacePlane(reflectionCamera, pos - Vector3.up * 0.1f, normal, 1.0f);
            var projection = baseCamera.CalculateObliqueMatrix(clipPlane);

            reflectionCamera.projectionMatrix = projection;
            reflectionCamera.transform.position = newPosition;
        }

        private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
        {
            return new Matrix4x4()
            {
                m00 = 1f - 2f * plane[0] * plane[0],
                m01 = -2f * plane[0] * plane[1],
                m02 = -2f * plane[0] * plane[2],
                m03 = -2f * plane[3] * plane[0],
                m10 = -2f * plane[1] * plane[0],
                m11 = 1f - 2f * plane[1] * plane[1],
                m12 = -2f * plane[1] * plane[2],
                m13 = -2f * plane[3] * plane[1],
                m20 = -2f * plane[2] * plane[0],
                m21 = -2f * plane[2] * plane[1],
                m22 = 1f - 2f * plane[2] * plane[2],
                m23 = -2f * plane[3] * plane[2],
                m30 = 0f,
                m31 = 0f,
                m32 = 0f,
                m33 = 1f
            };
        }

        private static Vector3 ReflectPosition(Vector3 pos)
        {
            var reflectPos = new Vector3(pos.x, -pos.y, pos.z);
            return reflectPos;
        }

        private Camera CreateReflectionCamera()
        {
            var obj = new GameObject("ReflectionCamera", typeof(Camera));
            var reflectionCamera = obj.GetComponent<Camera>();

            reflectionCamera.depth = -10;
            reflectionCamera.enabled = false;
            obj.hideFlags = HideFlags.HideAndDontSave;

            return reflectionCamera;
        }

        private Vector4 CameraSpacePlane(Camera camera, Vector3 pos, Vector3 normal, float sideSign)
        {
            var offsetPos = pos + normal * ToonRenderPipeline.Asset.ReflectionSettings.clipPlaneOffset;
            var mat = camera.worldToCameraMatrix;
            var cameraPos = mat.MultiplyPoint(offsetPos);
            var cameraNormal = mat.MultiplyVector(normal).normalized * sideSign;

            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPos, cameraNormal));
        }

        private void ExecutePlanarReflection(ScriptableRenderContext context, CommandBuffer commandBuffer, Camera camera)
        {
            if(camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview)
            {
                return;
            }

            UpdateReflectionCamera(camera);

            PlanarReflectionSettingData.Set();

            // 反射描画
            ToonRenderPipeline.Instance.RenderReflection(context, reflectionCamera, commandBuffer);

            PlanarReflectionSettingData.Restore();
        }

        private static class PlanarReflectionSettingData
        {
            private static int maxLod;
            private static float lodBias;

            public static void Set()
            {
                maxLod = QualitySettings.maximumLODLevel;
                lodBias = QualitySettings.lodBias;

                GL.invertCulling = true;
                QualitySettings.maximumLODLevel = 1;
                QualitySettings.lodBias = lodBias * 0.5f;
            }

            public static void Restore()
            {
                GL.invertCulling = false;
                QualitySettings.maximumLODLevel = maxLod;
                QualitySettings.lodBias = lodBias;
            }
        }
    }
}