using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shuimian.Gameplay
{
    /// <summary>Per-view planar reflection capture, independent of Crest components.</summary>
    public sealed class ShuimianPlanarReflection : IDisposable
    {
        sealed class View
        {
            public Camera source, camera;
            public RenderTexture texture;
        }
        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        public static bool IsRendering { get; private set; }

        public Texture GetTexture(Camera source)
        {
            return views.TryGetValue(source.GetInstanceID(), out var view) ? view.texture : null;
        }

        public void Render(ScriptableRenderContext context, Camera source, Renderer surface,
            float seaLevel, int size, LayerMask layers, float clipOffset)
        {
            if (IsRendering || source == null || surface == null || source.transform.position.y < seaLevel) return;
            // Scene-view cameras can be replaced when layouts change. Release their captures.
            var stale = new List<int>();
            foreach (var pair in views) if (pair.Value.source == null) stale.Add(pair.Key);
            foreach (int key in stale) { Release(views[key]); views.Remove(key); }
            int id = source.GetInstanceID();
            if (!views.TryGetValue(id, out var view))
            {
                var go = new GameObject("Shuimian Reflection Camera") { hideFlags = HideFlags.HideAndDontSave };
                var camera = go.AddComponent<Camera>();
                camera.enabled = false;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderShadows = false;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.renderPostProcessing = false;
                view = new View { source = source, camera = camera };
                views.Add(id, view);
            }
            size = Mathf.Clamp(size, 128, 2048);
            int height = Mathf.Max(64, Mathf.RoundToInt(size / Mathf.Max(.1f, source.aspect)));
            if (view.texture == null || view.texture.width != size || view.texture.height != height)
            {
                Destroy(view.texture);
                view.texture = new RenderTexture(size, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Shuimian Planar Reflection", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                view.texture.Create();
            }
            Camera reflection = view.camera;
            reflection.CopyFrom(source);
            reflection.enabled = false;
            // URP 12 only reads additional camera data for Game/VR cameras.
            reflection.cameraType = CameraType.Game;
            reflection.targetTexture = view.texture;
            reflection.rect = new Rect(0, 0, 1, 1);
            reflection.allowMSAA = false;
            reflection.allowHDR = false;
            reflection.useOcclusionCulling = false;
            reflection.cullingMask = source.cullingMask & layers.value;
            // Alpha overlays reflected geometry over the material's existing sky/probe reflection.
            reflection.clearFlags = CameraClearFlags.SolidColor;
            reflection.backgroundColor = Color.clear;

            Matrix4x4 mirror = Matrix4x4.identity;
            mirror.m11 = -1f;
            mirror.m13 = 2f * seaLevel;
            Vector3 position = mirror.MultiplyPoint(source.transform.position);
            Vector3 forward = mirror.MultiplyVector(source.transform.forward);
            Vector3 up = mirror.MultiplyVector(source.transform.up);
            reflection.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
            reflection.worldToCameraMatrix = source.worldToCameraMatrix * mirror;
            Vector3 clipPoint = reflection.worldToCameraMatrix.MultiplyPoint(new Vector3(0f, seaLevel + clipOffset, 0f));
            Vector3 clipNormal = reflection.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
            var clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal));
            reflection.projectionMatrix = source.CalculateObliqueMatrix(clipPlane);
            reflection.cullingMatrix = reflection.projectionMatrix * reflection.worldToCameraMatrix;

            bool oldCulling = GL.invertCulling;
            bool oldVisibility = surface.forceRenderingOff;
            try
            {
                IsRendering = true;
                surface.forceRenderingOff = true;
                GL.invertCulling = !oldCulling;
                UniversalRenderPipeline.RenderSingleCamera(context, reflection);
            }
            finally
            {
                GL.invertCulling = oldCulling;
                surface.forceRenderingOff = oldVisibility;
                IsRendering = false;
            }
        }
        public void Dispose()
        {
            foreach (var view in views.Values) Release(view);
            views.Clear();
        }
        static void Release(View view)
        {
            Destroy(view.texture);
            if (view.camera != null) Destroy(view.camera.gameObject);
        }
        static void Destroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
