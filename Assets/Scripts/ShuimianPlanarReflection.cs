// Independent water capture adapted from the mirrored-camera approach used by
// DemoScene01's MirrorRenderer/MirrorSurface. No demo components are attached.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shuimian.Gameplay
{
    /// <summary>Per-view water mirror capture with independently selectable scene effects.</summary>
    public sealed class ShuimianPlanarReflection : IDisposable
    {
        // Capture beyond the source view so normal distortion never samples a
        // faded/clamped screen edge. Keep the texture size and aspect unchanged.
        const float CaptureOverscan = 1.25f;
        public struct Settings
        {
            public bool sky, shadows, fog, postProcessing;
            public Settings(bool sky, bool shadows = false, bool fog = false, bool postProcessing = false)
            {
                this.sky = sky; this.shadows = shadows; this.fog = fog; this.postProcessing = postProcessing;
            }
        }

        sealed class View
        {
            public Camera source, camera;
            public UniversalAdditionalCameraData data;
            public Skybox skybox;
            public RenderTexture texture, unprocessed;
            public VolumeStack volumeStack;
            public CoveragePass coveragePass;
            public bool useCoverage;
        }

        // URP exposes SetRenderer but not its selected index. Reading the existing
        // serialized index keeps a source camera's selected renderer in URP 12.
        static readonly FieldInfo RendererIndex = typeof(UniversalAdditionalCameraData)
            .GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        readonly List<int> staleViews = new List<int>();
        public static bool IsRendering { get; private set; }

        public Texture GetTexture(Camera source)
        {
            return source != null && views.TryGetValue(source.GetInstanceID(), out var view) ? view.texture : null;
        }

        /// <summary>
        /// The same captured frame before post-processing. It is available for
        /// fractional post-processing strength or when transparent-sky coverage
        /// must survive URP's post-processing alpha overwrite.
        /// </summary>
        public Texture GetUnprocessedTexture(Camera source)
        {
            return source != null && views.TryGetValue(source.GetInstanceID(), out var view) ? view.unprocessed : null;
        }

        /// <summary>Maps source-screen UVs into the expanded reflection capture.</summary>
        public Vector4 GetScaleOffset(Camera source)
        {
            if (source == null || !views.TryGetValue(source.GetInstanceID(), out var view) || view.texture == null)
                return new Vector4(1f, 1f, 0f, 0f);
            float scale = 1f / CaptureOverscan;
            float offset = (1f - scale) * .5f;
            return new Vector4(scale, scale, offset, offset);
        }

        /// <summary>Releases only this source view, leaving other cameras' captures intact.</summary>
        public void ReleaseView(Camera source)
        {
            if (source == null) return;
            int id = source.GetInstanceID();
            if (!views.TryGetValue(id, out var view)) return;
            Release(view);
            views.Remove(id);
        }

        /// <summary>
        /// Post-processing in URP 12 overwrites alpha. When sky is excluded, this
        /// pre-postprocess alpha preserves the coverage of reflected geometry.
        /// Sample alpha at the same projected/distorted UV as GetTexture.
        /// </summary>
        public Texture GetCoverageTexture(Camera source)
        {
            return source != null && views.TryGetValue(source.GetInstanceID(), out var view) && view.useCoverage
                ? view.unprocessed : null;
        }

        // Preserve the previous transparent-sky capture contract for callers that
        // have not supplied the new mirror controls.
        public void Render(ScriptableRenderContext context, Camera source, Renderer surface,
            float seaLevel, int size, LayerMask layers, float clipOffset)
        {
            Render(context, source, surface, seaLevel, size, layers, clipOffset, false, false, true, false);
        }

        public void Render(ScriptableRenderContext context, Camera source, Renderer surface,
            float seaLevel, int size, LayerMask layers, float clipOffset, Settings settings)
        {
            Render(context, source, surface, seaLevel, size, layers, clipOffset,
                settings.sky, settings.shadows, settings.fog, settings.postProcessing);
        }

        public void Render(ScriptableRenderContext context, Camera source, Renderer surface,
            float seaLevel, int size, LayerMask layers, float clipOffset, bool reflectSky = true,
            bool reflectShadows = false, bool reflectFog = false, bool reflectPostProcessing = false,
            Transform forwardTransform = null, Renderer mirrorRenderer = null, float postProcessingStrength = 1f)
        {
            if (IsRendering || source == null || surface == null) return;
            Vector3 planePoint = forwardTransform != null ? forwardTransform.position : new Vector3(0f, seaLevel, 0f);
            Vector3 planeNormal = forwardTransform != null ? -forwardTransform.forward : Vector3.up;
            if (Vector3.Dot(source.transform.position - planePoint, planeNormal) < 0f) return;
            var pipeline = UniversalRenderPipeline.asset;
            if (pipeline == null) return;
            staleViews.Clear();
            foreach (var pair in views) if (pair.Value.source == null) staleViews.Add(pair.Key);
            foreach (int key in staleViews) { Release(views[key]); views.Remove(key); }
            int id = source.GetInstanceID();
            if (!views.TryGetValue(id, out var view))
            {
                var go = new GameObject("Shuimian Reflection Camera") { hideFlags = HideFlags.HideAndDontSave };
                var camera = go.AddComponent<Camera>(); camera.enabled = false;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderType = CameraRenderType.Base;
                data.allowXRRendering = false;
                view = new View
                {
                    source = source, camera = camera, data = data,
                    skybox = go.AddComponent<Skybox>()
                };
                view.coveragePass = new CoveragePass(view);
                views.Add(id, view);
            }

            bool hdr = source.allowHDR && pipeline.supportsHDR;
            var format = hdr ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            size = Mathf.Clamp(size, 128, 2048);
            int height = Mathf.Max(64, Mathf.RoundToInt(size / Mathf.Max(.1f, source.aspect)));
            if (view.texture == null || view.texture.width != size || view.texture.height != height || view.texture.format != format)
            {
                view.camera.targetTexture = null;
                ReleaseTexture(view.texture);
                view.texture = CreateTexture("Shuimian Mirror Reflection", size, height, 24, format);
            }
            postProcessingStrength = float.IsNaN(postProcessingStrength) ? 0f : Mathf.Clamp01(postProcessingStrength);
            bool applyPostProcessing = reflectPostProcessing && postProcessingStrength > 0f;
            view.useCoverage = applyPostProcessing && !reflectSky;
            bool keepUnprocessed = applyPostProcessing && (postProcessingStrength < 1f || view.useCoverage);
            if (keepUnprocessed && (view.unprocessed == null || view.unprocessed.width != size
                || view.unprocessed.height != height || view.unprocessed.format != format))
            {
                ReleaseTexture(view.unprocessed);
                // Keep the HDR format: blending a tonemapped image with a raw
                // image already clipped to LDR would give the wrong result.
                view.unprocessed = CreateTexture("Shuimian Mirror Before Post Processing", size, height, 0, format);
            }
            else if (!keepUnprocessed && view.unprocessed != null)
            {
                ReleaseTexture(view.unprocessed);
                view.unprocessed = null;
            }

            Camera reflection = view.camera;
            reflection.CopyFrom(source);
            reflection.enabled = false;
            // Unlike CameraType.Reflection, Game makes URP 12 honor the camera's
            // shadow, post-processing and texture requirements.
            reflection.cameraType = CameraType.Game;
            reflection.targetTexture = view.texture;
            reflection.rect = new Rect(0, 0, 1, 1);
            reflection.aspect = source.aspect;
            reflection.allowMSAA = false;
            reflection.allowHDR = hdr;
            reflection.useOcclusionCulling = false;
            // Mirror contents have their own layer selection, as in the demo.
            reflection.cullingMask = layers.value;
            reflection.clearFlags = reflectSky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            reflection.backgroundColor = Color.clear;
            var sourceSky = source.GetComponent<Skybox>();
            view.skybox.enabled = reflectSky;
            view.skybox.material = sourceSky != null && sourceSky.enabled ? sourceSky.material : null;

            var sourceData = source.GetComponent<UniversalAdditionalCameraData>();
            var volumeData = sourceData;
            if (volumeData == null && source.cameraType == CameraType.SceneView && Camera.main != null)
                volumeData = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            var dataForReflection = view.data;
            dataForReflection.renderShadows = reflectShadows;
            dataForReflection.renderPostProcessing = applyPostProcessing;
            // Scene effects such as soft particles and refraction may require
            // the source camera's depth/color inputs in the mirror as well.
            dataForReflection.requiresDepthOption = source.cameraType == CameraType.SceneView
                ? CameraOverrideOption.On : sourceData != null
                    ? sourceData.requiresDepthOption : CameraOverrideOption.UsePipelineSettings;
            dataForReflection.requiresColorOption = sourceData != null
                ? sourceData.requiresColorOption : CameraOverrideOption.UsePipelineSettings;
            dataForReflection.volumeLayerMask = volumeData != null ? volumeData.volumeLayerMask : (LayerMask)1;
            dataForReflection.volumeTrigger = volumeData != null && volumeData.volumeTrigger != null
                ? volumeData.volumeTrigger : source.transform;
            dataForReflection.antialiasing = applyPostProcessing && sourceData != null
                ? sourceData.antialiasing : AntialiasingMode.None;
            if (sourceData != null) dataForReflection.antialiasingQuality = sourceData.antialiasingQuality;
            dataForReflection.stopNaN = applyPostProcessing && sourceData != null && sourceData.stopNaN;
            dataForReflection.dithering = applyPostProcessing && sourceData != null && sourceData.dithering;
            int rendererIndex = sourceData != null && source.cameraType != CameraType.SceneView && RendererIndex != null
                ? (int)RendererIndex.GetValue(sourceData) : -1;
            dataForReflection.SetRenderer(rendererIndex);

            Matrix4x4 mirror = ReflectionMatrix(planePoint, planeNormal);
            Vector3 position = mirror.MultiplyPoint(source.transform.position);
            Vector3 forward = mirror.MultiplyVector(source.transform.forward);
            Vector3 up = mirror.MultiplyVector(source.transform.up);
            reflection.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
            reflection.worldToCameraMatrix = source.worldToCameraMatrix * mirror;
            Vector3 clipPoint = reflection.worldToCameraMatrix.MultiplyPoint(planePoint + planeNormal * clipOffset);
            Vector3 clipNormal = reflection.worldToCameraMatrix.MultiplyVector(planeNormal).normalized;
            var clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal));
            // Scale the entire rows, including off-centre projection terms, so
            // perspective, orthographic and custom source projections all agree
            // with GetScaleOffset. Oblique clipping must use this wider matrix.
            Matrix4x4 projection = source.projectionMatrix;
            for (int column = 0; column < 4; column++)
            {
                projection[0, column] /= CaptureOverscan;
                projection[1, column] /= CaptureOverscan;
            }
            reflection.projectionMatrix = projection;
            reflection.projectionMatrix = reflection.CalculateObliqueMatrix(clipPlane);
            reflection.cullingMatrix = reflection.projectionMatrix * reflection.worldToCameraMatrix;

            bool oldCulling = GL.invertCulling;
            bool oldVisibility = surface.forceRenderingOff;
            bool hideMirrorRenderer = mirrorRenderer != null && mirrorRenderer != surface;
            bool oldMirrorVisibility = hideMirrorRenderer && mirrorRenderer.forceRenderingOff;
            bool oldFog = RenderSettings.fog;
            var oldActive = RenderTexture.active;
            var volumeManager = VolumeManager.instance;
            var oldStack = volumeManager.stack;
            try
            {
                IsRendering = true;
                surface.forceRenderingOff = true;
                if (hideMirrorRenderer) mirrorRenderer.forceRenderingOff = true;
                GL.invertCulling = !oldCulling;
                RenderSettings.fog = reflectFog && oldFog;
                if (applyPostProcessing)
                {
                    // RenderSingleCamera skips URP's normal volume-update stage.
                    if (view.volumeStack == null) view.volumeStack = volumeManager.CreateStack();
                    volumeManager.Update(view.volumeStack, dataForReflection.volumeTrigger, dataForReflection.volumeLayerMask);
                    volumeManager.stack = view.volumeStack;
                }
                if (keepUnprocessed) dataForReflection.scriptableRenderer.EnqueuePass(view.coveragePass);
                UniversalRenderPipeline.RenderSingleCamera(context, reflection);
            }
            finally
            {
                volumeManager.stack = oldStack;
                RenderSettings.fog = oldFog;
                GL.invertCulling = oldCulling;
                surface.forceRenderingOff = oldVisibility;
                if (hideMirrorRenderer && mirrorRenderer != null) mirrorRenderer.forceRenderingOff = oldMirrorVisibility;
                RenderTexture.active = oldActive;
                IsRendering = false;
            }
        }

        static Matrix4x4 ReflectionMatrix(Vector3 point, Vector3 normal)
        {
            Vector4 plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
            Matrix4x4 matrix = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 4; column++)
                    matrix[row, column] -= 2f * normal[row] * plane[column];
            return matrix;
        }

        sealed class CoveragePass : ScriptableRenderPass
        {
            readonly View view;
            RenderTargetIdentifier colorTarget;
            public CoveragePass(View view)
            {
                this.view = view;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Color);
            }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                colorTarget = renderingData.cameraData.renderer.cameraColorTarget;
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (renderingData.cameraData.camera != view.camera || view.unprocessed == null) return;
                var cmd = CommandBufferPool.Get("Shuimian Mirror Before Post Processing");
                try
                {
                    cmd.Blit(colorTarget, view.unprocessed);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }
        }

        public void Dispose()
        {
            foreach (var view in views.Values) Release(view);
            views.Clear(); staleViews.Clear();
        }
        static RenderTexture CreateTexture(string name, int width, int height, int depth, RenderTextureFormat format)
        {
            var texture = new RenderTexture(width, height, depth, format)
            {
                name = name, hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }
        static void Release(View view)
        {
            if (view.camera != null) view.camera.targetTexture = null;
            ReleaseTexture(view.texture); ReleaseTexture(view.unprocessed);
            if (view.volumeStack != null) VolumeManager.instance.DestroyStack(view.volumeStack);
            if (view.camera != null) Destroy(view.camera.gameObject);
        }
        static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release(); Destroy(texture);
        }
        static void Destroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
