// Underwater mask/fog rendering follows Crest Ocean System's shader contract.
// Shader algorithms are Copyright 2021-2022 Wave Harmonic Ltd.
// This resource owner has no dependency on Crest's C# components or singleton.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shuimian.Gameplay
{
    /// <summary>Renders the existing water shader's underwater fog and waterline for each view.</summary>
    public sealed class ShuimianUnderwaterRenderer : IDisposable
    {
        static readonly int OceanMask = Shader.PropertyToID("_CrestOceanMaskTexture");
        static readonly int OceanDepth = Shader.PropertyToID("_CrestOceanMaskDepthTexture");
        static readonly int CameraColor = Shader.PropertyToID("_CrestCameraColorTexture");
        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        readonly List<int> staleViews = new List<int>();
        Shader maskShader, effectShader;
        readonly ComputeShader maskCorrection;
        readonly int correctionKernel, arrayCorrectionKernel;
        bool warned;

        public ShuimianUnderwaterRenderer(ComputeShader combineShader = null)
        {
            if (combineShader == null || !SystemInfo.supportsComputeShaders) return;
            maskCorrection = UnityEngine.Object.Instantiate(combineShader);
            maskCorrection.hideFlags = HideFlags.HideAndDontSave;
            correctionKernel = maskCorrection.FindKernel("FixUnderwaterMask");
            arrayCorrectionKernel = maskCorrection.FindKernel("FixUnderwaterMaskArray");
        }

        sealed class View : IDisposable
        {
            public readonly Camera camera;
            public readonly Material maskMaterial, effectMaterial;
            public readonly MaskPass maskPass;
            public readonly EffectPass effectPass;
            public readonly int maskId, depthId, colorId;
            public readonly ComputeShader maskCorrection;
            public readonly int correctionKernel, arrayCorrectionKernel;
            public readonly MaterialPropertyBlock effectProperties = new MaterialPropertyBlock();
            public MaterialPropertyBlock[] rings;
            public Mesh mesh;
            public Matrix4x4 localToWorld;
            public readonly Vector3[] ambientDirections = { Vector3.zero };
            public readonly Color[] ambient = new Color[1];

            public View(Camera camera, Shader maskShader, Shader effectShader,
                ComputeShader maskCorrection, int correctionKernel, int arrayCorrectionKernel)
            {
                this.camera = camera;
                this.maskCorrection = maskCorrection;
                this.correctionKernel = correctionKernel;
                this.arrayCorrectionKernel = arrayCorrectionKernel;
                int id = camera.GetInstanceID();
                maskId = Shader.PropertyToID("_ShuimianUnderwaterMask" + id);
                depthId = Shader.PropertyToID("_ShuimianUnderwaterDepth" + id);
                colorId = Shader.PropertyToID("_ShuimianUnderwaterColor" + id);
                maskMaterial = new Material(maskShader) { name = "Shuimian Underwater Mask", hideFlags = HideFlags.HideAndDontSave };
                effectMaterial = new Material(effectShader) { name = "Shuimian Underwater Effect", hideFlags = HideFlags.HideAndDontSave };
                maskPass = new MaskPass(this);
                effectPass = new EffectPass(this);
            }

            public void Prepare(Material surfaceMaterial, MeshRenderer surface, Mesh surfaceMesh, float seaLevel, Material underwaterMaterial)
            {
                mesh = surfaceMesh;
                localToWorld = surface.localToWorldMatrix;
                if (rings == null || rings.Length != mesh.subMeshCount)
                {
                    rings = new MaterialPropertyBlock[mesh.subMeshCount];
                    for (int i = 0; i < rings.Length; i++) rings[i] = new MaterialPropertyBlock();
                }
                for (int i = 0; i < rings.Length; i++)
                {
                    surface.GetPropertyBlock(rings[i], i);
                    rings[i].SetFloat("_MaskBelowSurface", -1f);
                }
                surface.GetPropertyBlock(effectProperties, 0);

                // Keep camera/LOD state per view; only optical settings come
                // from the exposed asset. Never write to the user's material.
                if (underwaterMaterial != null) effectMaterial.shader = underwaterMaterial.shader;
                effectMaterial.CopyPropertiesFromMaterial(underwaterMaterial != null ? underwaterMaterial : surfaceMaterial);
                // Meniscus belongs to the nearby waterline. A forced air/deep
                // view must not add a dark rim to the distant horizon.
                SetKeyword(effectMaterial, "CREST_MENISCUS", Mathf.Abs(effectProperties.GetFloat("_CrestForceUnderwater")) < .5f);
                SetKeyword(effectMaterial, "_FULL_SCREEN_EFFECT", false);
                SetKeyword(effectMaterial, "_DEBUG_VIEW_OCEAN_MASK", false);
                SetKeyword(effectMaterial, "_DEBUG_VIEW_STENCIL", false);
                SetKeyword(effectMaterial, "_PROJECTION_ORTHOGRAPHIC", camera.orthographic);
                SetKeyword(effectMaterial, "_PROJECTION_PERSPECTIVE", !camera.orthographic);
                // Share the surface's caustics texture/settings. The original
                // underwater shader projects them only onto submerged geometry.
                SetKeyword(effectMaterial, "_CAUSTICS_ON", surfaceMaterial.IsKeywordEnabled("_CAUSTICS_ON")
                    && (underwaterMaterial == null || underwaterMaterial.IsKeywordEnabled("_CAUSTICS_ON")));
                SetKeyword(effectMaterial, "_SHADOWS_ON", false);
                maskMaterial.SetInt("_StencilRef", 0);
                SetKeyword(maskMaterial, "CREST_WATER_VOLUME", false);

                int levels = mesh.subMeshCount;
                effectProperties.SetInt("_LD_SliceIndex", 0);
                effectProperties.SetInt("_CrestDataSliceOffset", Mathf.Clamp(4, 0, levels - 2));
                effectProperties.SetVector("_HorizonNormal", new Vector4(
                    Vector3.Dot(Vector3.up, camera.transform.right),
                    Vector3.Dot(Vector3.up, camera.transform.up), 0f, 0f));
                LightProbes.GetInterpolatedProbe(camera.transform.position, null, out var harmonics);
                harmonics.Evaluate(ambientDirections, ambient);
                effectProperties.SetVector("_CrestAmbientLighting", ambient[0]);

                // Original horizon pass reconstructs a point near the far plane.
                // This fills pixels outside the finite generated surface as air/water.
                float near = Mathf.Max(.00001f, camera.nearClipPlane);
                float far = Mathf.Max(near + .001f, camera.farClipPlane);
                float offset;
                if (camera.orthographic)
                    offset = SystemInfo.usesReversedZBuffer ? 1f - .68f : .68f;
                else
                {
                    float zy = SystemInfo.usesReversedZBuffer ? 1f : far / near;
                    float zx = SystemInfo.usesReversedZBuffer ? far / near - 1f : 1f - zy;
                    offset = (1f - zy * .68f) / (zx * .68f);
                }
                effectProperties.SetFloat("_FarPlaneOffset", offset);
                effectProperties.SetVector("_OceanCenterPosWorld", new Vector3(surface.transform.position.x, seaLevel, surface.transform.position.z));
            }

            public void Dispose()
            {
                Destroy(maskMaterial);
                Destroy(effectMaterial);
            }
        }

        sealed class MaskPass : ScriptableRenderPass
        {
            readonly View view;
            int width, height, slices;
            bool isArray;
            public MaskPass(View view)
            {
                this.view = view;
                // The ocean mask has its own depth buffer. Matrices are available here.
                renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
            }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.depthBufferBits = 0;
                descriptor.colorFormat = RenderTextureFormat.RFloat;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                descriptor.enableRandomWrite = false;
                width = descriptor.width; height = descriptor.height;
                isArray = descriptor.dimension == TextureDimension.Tex2DArray;
                slices = isArray ? descriptor.volumeDepth : 1;
                descriptor.enableRandomWrite = view.maskCorrection != null;
                cmd.GetTemporaryRT(view.maskId, descriptor, FilterMode.Point);
                descriptor.colorFormat = RenderTextureFormat.Depth;
                descriptor.depthBufferBits = 24;
                descriptor.enableRandomWrite = false;
                cmd.GetTemporaryRT(view.depthId, descriptor, FilterMode.Point);
                ConfigureTarget(new RenderTargetIdentifier(view.maskId), new RenderTargetIdentifier(view.depthId));
                ConfigureClear(ClearFlag.All, Color.black);
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (renderingData.cameraData.camera != view.camera || view.mesh == null) return;
                var cmd = CommandBufferPool.Get("Shuimian Underwater Mask");
                try
                {
                    cmd.SetGlobalTexture(OceanMask, new RenderTargetIdentifier(view.maskId));
                    cmd.SetGlobalTexture(OceanDepth, new RenderTargetIdentifier(view.depthId));
                    // Horizon first, then the exact animated surface from both sides.
                    cmd.DrawProcedural(Matrix4x4.identity, view.maskMaterial, 1, MeshTopology.Triangles, 3, 1, view.effectProperties);
                    for (int i = 0; i < view.rings.Length; i++)
                        cmd.DrawMesh(view.mesh, view.localToWorld, view.maskMaterial, i, 0, view.rings[i]);
                    if (view.maskCorrection != null)
                    {
                        int kernel = isArray ? view.arrayCorrectionKernel : view.correctionKernel;
                        cmd.SetComputeTextureParam(view.maskCorrection, kernel,
                            isArray ? "_UnderwaterMaskArray" : "_UnderwaterMask", new RenderTargetIdentifier(view.maskId));
                        cmd.DispatchCompute(view.maskCorrection, kernel, (width + 7) / 8, (height + 7) / 8, slices);
                    }
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                // Cleanup runs after all camera passes, so the fullscreen effect has consumed these.
                cmd.ReleaseTemporaryRT(view.maskId);
                cmd.ReleaseTemporaryRT(view.depthId);
            }
        }

        sealed class EffectPass : ScriptableRenderPass
        {
            readonly View view;
            RenderTargetIdentifier colorTarget;
            public EffectPass(View view)
            {
                this.view = view;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
            }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                colorTarget = renderingData.cameraData.renderer.cameraColorTarget;
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.depthBufferBits = 0;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                descriptor.enableRandomWrite = false;
                cmd.GetTemporaryRT(view.colorId, descriptor, FilterMode.Point);
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (renderingData.cameraData.camera != view.camera) return;
                var cmd = CommandBufferPool.Get("Shuimian Underwater Fog");
                try
                {
                    // A separate resolved color texture prevents reading and writing the same target.
                    cmd.Blit(colorTarget, new RenderTargetIdentifier(view.colorId));
                    cmd.SetGlobalTexture(CameraColor, new RenderTargetIdentifier(view.colorId));
                    cmd.SetGlobalTexture(OceanMask, new RenderTargetIdentifier(view.maskId));
                    cmd.SetGlobalTexture(OceanDepth, new RenderTargetIdentifier(view.depthId));
                    cmd.SetRenderTarget(colorTarget);
                    cmd.DrawProcedural(Matrix4x4.identity, view.effectMaterial, 0, MeshTopology.Triangles, 3, 1, view.effectProperties);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }
            public override void OnCameraCleanup(CommandBuffer cmd) => cmd.ReleaseTemporaryRT(view.colorId);
        }

        /// <summary>Call after the surface's per-camera LOD data has been prepared.</summary>
        public void Enqueue(Camera camera, Material surfaceMaterial, MeshRenderer surfaceRenderer, float seaLevel, Material underwaterMaterial = null)
        {
            if (camera == null || surfaceMaterial == null || surfaceRenderer == null ||
                (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView && camera.cameraType != CameraType.VR) ||
                (camera.cullingMask & (1 << surfaceRenderer.gameObject.layer)) == 0) return;
            var filter = surfaceRenderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            // Conservative bound: a camera well above every possible crest needs no underwater passes.
            if (camera.transform.position.y > surfaceRenderer.bounds.max.y + camera.nearClipPlane * 2f) return;
            if (maskShader == null) maskShader = Resources.Load<Shader>("UnderwaterMaskURP");
            if (effectShader == null) effectShader = Resources.Load<Shader>("UnderwaterEffectURP");
            if (maskShader == null || effectShader == null)
            {
                if (!warned) Debug.LogWarning("Shuimian 水下效果缺少 UnderwaterMaskURP / UnderwaterEffectURP 着色资源。");
                warned = true;
                return;
            }
            var pipeline = UniversalRenderPipeline.asset;
            if (pipeline == null) return;
            ScriptableRenderer renderer = pipeline.scriptableRenderer;
            if (camera.cameraType != CameraType.SceneView && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                renderer = data.scriptableRenderer;
            if (renderer == null) return;
            staleViews.Clear();
            foreach (var pair in views) if (pair.Value.camera == null) staleViews.Add(pair.Key);
            foreach (int key in staleViews) { views[key].Dispose(); views.Remove(key); }
            int id = camera.GetInstanceID();
            if (!views.TryGetValue(id, out var view))
            {
                view = new View(camera, maskShader, effectShader, maskCorrection, correctionKernel, arrayCorrectionKernel);
                views.Add(id, view);
            }
            if (underwaterMaterial == null) view.effectMaterial.shader = effectShader;
            view.Prepare(surfaceMaterial, surfaceRenderer, filter.sharedMesh, seaLevel, underwaterMaterial);
            renderer.EnqueuePass(view.maskPass);
            renderer.EnqueuePass(view.effectPass);
        }
        public void Dispose()
        {
            foreach (var view in views.Values) view.Dispose();
            views.Clear();
            staleViews.Clear();
            Destroy(maskCorrection);
        }
        static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }
        static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
