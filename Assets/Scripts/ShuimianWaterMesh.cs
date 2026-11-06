using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shuimian.Gameplay
{
    /// <summary>Independent square LOD water mesh and FFT waves, using Water's surface material.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public class ShuimianWaterMesh : MonoBehaviour
    {
        [Header("原水面材质")]
        public Material sourceMaterial;
        [Header("方形分级网格")]
        [Tooltip("中心区域单元格的世界尺寸（米）；每向外一层翻倍。")]
        [Min(.05f)] public float nearCellSize = 1f / 3f;
        [Tooltip("中心方形每边的格子数，自动取 4 的倍数。")]
        [Range(8, 128)] public int subdivisions = 96;
        [Range(1, 9)] public int lodCount = 7;
        public bool followCamera = true;
        [Header("FFT 海浪（来自 Water 的波谱参数）")]
        public bool enableWaves = true;
        [Range(0f, 3f)] public float waveAmplitude = 1f;
        [Range(0f, 2f)] public float waveChop = .821f;
        [Min(0f)] public float windSpeedKmh = 150f;
        [Range(0f, 1f)] public float windTurbulence = .145f;
        public float waveDirectionDegrees;
        [Range(0f, 3f)] public float waveTimeScale = 1f;
        [Range(128, 1024)] public int waveTextureResolution = 512;
        public bool previewWavesInEditor = true;
        [Header("浅水波浪衰减")]
        public bool enableShallowAttenuation = true;
        [Tooltip("浅水区按波长减弱波浪的程度，Water 场景为 0.95。")]
        [Range(0f, 1f)] public float shallowAttenuation = .95f;
        [Tooltip("超过此水深恢复完整波强；1000 沿用 Water 的默认波长衰减方式。")]
        [Range(1f, 1000f)] public float shallowAttenuationMaxDepth = 1000f;
        [Header("独立平面反射")]
        public bool enablePlanarReflection = true;
        [Range(128, 2048)] public int reflectionTextureSize = 512;
        public LayerMask reflectionLayers = ~0;
        [Range(0f, .5f)] public float reflectionClipOffset = .07f;
        [Header("水下表现")]
        [Tooltip("启用水面背面的折射与内部反射，以及沿波浪水线裁切的水下深度雾。")]
        public bool enableUnderwater = true;
        [Tooltip("复用水面材质中的焦散贴图、强度、尺寸和运动参数。")]
        public bool enableCaustics = true;
        [Header("浪尖与岸边泡沫")]
        public bool enableFoam = true;
        [Min(0f)] public float foamWaveStrength = 3.27f;
        [Range(0f, 1f)] public float foamWaveCoverage = .76f;
        [Min(.001f)] public float foamFadeRate = .8f;
        [Min(.01f)] public float foamShoreDepth = 1f;
        [Min(0f)] public float foamShoreStrength = 2f;
        [Header("独立波浪计算资源")]
        public ComputeShader spectrumShader;
        public ComputeShader fftShader;
        public ComputeShader combineShader;

        // Binary layouts required by the original surface shader; no Crest C# dependency.
        [StructLayout(LayoutKind.Sequential)]
        struct CascadeData
        {
            public Vector2 position;
            public float scale, resolution, inverseResolution, texelWidth, weight, maxWavelength;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct InstanceData
        {
            public float meshScaleLerp, farNormalsWeight, gridWidth;
            public Vector2 normalScrollSpeeds;
            public Vector3 padding;
        }
        static readonly string[] SimulationKeywords =
        {
            "_FOAM_ON", "_FOAM3DLIGHTING_ON", "_FLOW_ON", "_SHADOWS_ON", "_CAUSTICS_ON",
            "_PLANARREFLECTIONS_ON", "_UNDERWATER_ON", "_CLIPSURFACE_ON", "_CLIPUNDERTERRAIN_ON", "_ALBEDO_ON"
        };

        GameObject surface;
        Mesh mesh;
        ShuimianWaterHorizon horizon;
        Material surfaceMaterial, materialSource;
        Material[] ringMaterials;
        Texture2D normalSource, filteredNormals;
        MeshRenderer waterRenderer;
        ComputeBuffer cascadeBuffer, instanceBuffer;
        Texture2DArray seaFloorDepth;
        ShuimianWaterDepth waterDepth;
        ShuimianPlanarReflection reflections;
        ShuimianUnderwaterRenderer underwaterRenderer;
        ShuimianWaterViewState waterViewState;
        ShuimianWaveSimulation waves;
        ComputeShader activeSpectrum, activeFft, activeCombine;
        MaterialPropertyBlock properties;
        CascadeData[] cascades;
        InstanceData[] instances;
        bool needsRebuild = true, warnedAboutWaves;
        double editorStartTime, nextEditorUpdate;

        /// <summary>Raised after a camera's FFT field has been prepared, before its surface is drawn.</summary>
        public event System.Action<Camera> SurfacePrepared;
        public Vector2 WaveFieldCenter { get; private set; }
        public Vector2 WaveFieldDiameters { get; private set; }
        public int WaveFieldCascadeCount { get; private set; }

        public Mesh SurfaceMesh => mesh;
        public MeshRenderer SurfaceRenderer => waterRenderer;
        public RenderTexture WaveDisplacement => waves?.Displacement;
        public RenderTexture FoamTexture => waves?.Foam;
        public Texture GetReflectionTexture(Camera camera) => reflections?.GetTexture(camera);
        public float OuterHalfExtent => Mathf.Clamp(subdivisions / 4 * 4, 8, 128)
            * Mathf.Max(.05f, nearCellSize) * .5f * (1 << (Mathf.Clamp(lodCount, 1, 9) - 1));

        void OnEnable()
        {
            needsRebuild = true;
            // Remove the legacy uniform plane from rendering after script reloads.
            var legacyRenderer = GetComponent<MeshRenderer>();
            if (legacyRenderer != null) legacyRenderer.enabled = false;
            var legacyFilter = GetComponent<MeshFilter>();
            if (legacyFilter != null) legacyFilter.sharedMesh = null;
            RenderPipelineManager.beginCameraRendering += PrepareCamera;
#if UNITY_EDITOR
            editorStartTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorTick;
#endif
        }
        void OnValidate() => needsRebuild = true;
        void Update()
        {
            EnsureSurface();
            EnsureMaterial();
        }
#if UNITY_EDITOR
        void EditorTick()
        {
            if (Application.isPlaying || !previewWavesInEditor || !enableWaves) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < nextEditorUpdate) return;
            nextEditorUpdate = now + 1.0 / 30.0;
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif
        void EnsureSurface()
        {
            if (surface == null)
            {
                surface = new GameObject("Shuimian Water Surface (LOD)") { hideFlags = HideFlags.HideAndDontSave };
                surface.transform.SetParent(transform, false);
                surface.layer = gameObject.layer;
                surface.AddComponent<MeshFilter>();
                waterRenderer = surface.AddComponent<MeshRenderer>();
                waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
                waterRenderer.receiveShadows = false;
                needsRebuild = true;
            }
            if (!needsRebuild && mesh != null) return;
            needsRebuild = false;
            var previous = mesh;
            mesh = ShuimianWaterMeshBuilder.Build(subdivisions, nearCellSize, lodCount, 30f * Mathf.Max(1f, waveAmplitude));
            horizon = new ShuimianWaterHorizon(mesh, OuterHalfExtent, 30f * Mathf.Max(1f, waveAmplitude));
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            ReleaseObject(previous);
        }
        void EnsureMaterial()
        {
            if (waterRenderer == null || sourceMaterial == null)
            {
                ReleaseMaterial();
                return;
            }
            if (surfaceMaterial == null || materialSource != sourceMaterial)
            {
                ReleaseMaterial();
                materialSource = sourceMaterial;
                surfaceMaterial = new Material(sourceMaterial)
                {
                    name = sourceMaterial.name + " (Shuimian Surface)", hideFlags = HideFlags.HideAndDontSave
                };
            }
            surfaceMaterial.CopyPropertiesFromMaterial(sourceMaterial);
            foreach (string keyword in SimulationKeywords) surfaceMaterial.DisableKeyword(keyword);
            SetKeyword("_UNDERWATER_ON", enableUnderwater);
            SetKeyword("_CAUSTICS_ON", enableCaustics && sourceMaterial.IsKeywordEnabled("_CAUSTICS_ON")
                && sourceMaterial.GetTexture("_CausticsTexture") != null);
            // A submerged view needs the actual back faces and the shader's
            // per-pixel underwater reflection/refraction branch.
            if (enableUnderwater) surfaceMaterial.SetInt("_CullMode", (int)CullMode.Off);
            // Each stitched geometry ring supplies its own shader LOD index.
            int levels = mesh != null ? mesh.subMeshCount : Mathf.Clamp(lodCount, 1, 9);
            if (ringMaterials == null || ringMaterials.Length != levels || ringMaterials[0] != surfaceMaterial)
            {
                ringMaterials = new Material[levels];
                for (int i = 0; i < levels; i++) ringMaterials[i] = surfaceMaterial;
                waterRenderer.sharedMaterials = ringMaterials;
            }
            // Keep filtering local to this water instance; the shared source
            // normal-map importer and both scene material assets are unchanged.
            var normal = sourceMaterial.GetTexture("_Normals") as Texture2D;
            if (normal != normalSource)
            {
                ReleaseObject(filteredNormals); filteredNormals = null;
                normalSource = normal;
                if (normal != null)
                {
                    // Imported normal maps are intentionally not CPU-readable.
                    // Allocate matching GPU storage and copy its mip levels without
                    // Instantiate/GetPixels or changing the shared importer.
                    filteredNormals = new Texture2D(normal.width, normal.height, normal.format,
                        normal.mipmapCount, true);
                    Graphics.CopyTexture(normal, filteredNormals);
                    filteredNormals.name = normal.name + " (Shuimian Filtered)";
                    filteredNormals.hideFlags = HideFlags.HideAndDontSave;
                    filteredNormals.filterMode = FilterMode.Trilinear;
                    filteredNormals.anisoLevel = 8;
                    filteredNormals.mipMapBias = 0f;
                    filteredNormals.wrapModeU = normal.wrapModeU;
                    filteredNormals.wrapModeV = normal.wrapModeV;
                }
            }
            if (filteredNormals != null) surfaceMaterial.SetTexture("_Normals", filteredNormals);
        }
        void EnsureWaves()
        {
            bool available = enableWaves && spectrumShader != null && fftShader != null && combineShader != null
                && SystemInfo.supportsComputeShaders && SystemInfo.supports2DArrayTextures;
            if (!available)
            {
                waves?.Dispose(); waves = null;
                if (enableWaves && !warnedAboutWaves)
                {
                    Debug.LogWarning("Shuimian 海浪需要指定三个独立计算资源，并使用支持 Compute Shader 的图形设备。", this);
                    warnedAboutWaves = true;
                }
                return;
            }
            if (waves != null && activeSpectrum == spectrumShader && activeFft == fftShader && activeCombine == combineShader) return;
            waves?.Dispose();
            waves = new ShuimianWaveSimulation(spectrumShader, fftShader, combineShader);
            activeSpectrum = spectrumShader; activeFft = fftShader; activeCombine = combineShader;
            warnedAboutWaves = false;
        }
        float CurrentTime
        {
            get
            {
                if (Application.isPlaying) return Time.time * waveTimeScale;
#if UNITY_EDITOR
                return previewWavesInEditor ? (float)(EditorApplication.timeSinceStartup - editorStartTime) * waveTimeScale : 0f;
#else
                return 0f;
#endif
            }
        }
        void PrepareCamera(ScriptableRenderContext context, Camera camera)
        {
            if (ShuimianPlanarReflection.IsRendering || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
            if (enablePlanarReflection)
            {
                EnsureSurface();
                if (reflections == null) reflections = new ShuimianPlanarReflection();
                reflections.Render(context, camera, waterRenderer, transform.position.y,
                    reflectionTextureSize, reflectionLayers, reflectionClipOffset);
            }
            else if (reflections != null) { reflections.Dispose(); reflections = null; }
            PrepareSurface(camera, CurrentTime);
            // Enqueue only after reflection capture; its nested camera uses the
            // same URP renderer and must not consume this view's water passes.
            if (enableUnderwater && surfaceMaterial != null)
            {
                if (underwaterRenderer == null) underwaterRenderer = new ShuimianUnderwaterRenderer(combineShader);
                underwaterRenderer.Enqueue(camera, surfaceMaterial, waterRenderer, transform.position.y);
            }
            else if (underwaterRenderer != null) { underwaterRenderer.Dispose(); underwaterRenderer = null; }
        }

        // Explicit time also supports deterministic rendering/verification without a game loop.
        public void PrepareSurface(Camera camera, float time)
        {
            EnsureSurface(); EnsureMaterial(); EnsureWaves();
            if (surfaceMaterial == null) return;
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.requiresDepthTexture = true;
                cameraData.requiresColorTexture = true;
            }
            float snap = Mathf.Max(.05f, nearCellSize) * 2f;
            Vector3 center = transform.position;
            if (followCamera)
            {
                center.x = Mathf.Round(camera.transform.position.x / snap) * snap;
                center.z = Mathf.Round(camera.transform.position.z / snap) * snap;
            }
            surface.transform.position = center;
            surface.transform.rotation = Quaternion.identity;
            var scale = transform.lossyScale;
            surface.transform.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            horizon.EnsureCoverage(camera, center);

            int levels = mesh.subMeshCount;
            int cascadeCount = levels + 1;
            // Ring i extends from baseWidth*2^(i-2) to baseWidth*2^(i-1).
            // Its texture covers that outer boundary and blends into slice i+1.
            float nearDiameter = Mathf.Clamp(subdivisions / 4 * 4, 8, 128) * Mathf.Max(.05f, nearCellSize);
            float farDiameter = nearDiameter * 2f;
            // Waves, foam and surface rendering must use the same camera's
            // depth field, including the very first prepared frame.
            if (waterDepth == null) waterDepth = new ShuimianWaterDepth();
            seaFloorDepth = waterDepth.Update(new Vector2(center.x, center.z), nearDiameter, cascadeCount, center.y);
            if (waves != null)
                waves.Update(new Vector2(center.x, center.z), nearDiameter, farDiameter, waveTextureResolution,
                    time, windSpeedKmh, windTurbulence, waveChop, waveAmplitude, waveDirectionDegrees, cascadeCount,
                    seaFloorDepth, center.y, enableShallowAttenuation ? shallowAttenuation : 0f, shallowAttenuationMaxDepth);
            int resolution = waves?.Displacement.width ?? Mathf.Clamp(waveTextureResolution, 16, 1024);
            if (enableFoam && waves != null)
                waves.UpdateFoam(new Vector2(center.x, center.z), nearDiameter, farDiameter, cascadeCount,
                    time, foamWaveStrength, foamWaveCoverage, foamFadeRate, seaFloorDepth, center.y,
                    foamShoreDepth, foamShoreStrength);
            SetKeyword("_FOAM_ON", enableFoam && waves?.Foam != null);
            SetKeyword("_FOAM3DLIGHTING_ON", enableFoam && waves?.Foam != null && sourceMaterial.IsKeywordEnabled("_FOAM3DLIGHTING_ON"));
            Texture reflectionTexture = enablePlanarReflection ? reflections?.GetTexture(camera) : null;
            SetKeyword("_PLANARREFLECTIONS_ON", reflectionTexture != null && camera.transform.position.y >= center.y);
            if (cascadeBuffer == null || cascadeBuffer.count != cascadeCount)
            {
                ClearProperties();
                cascadeBuffer?.Release(); instanceBuffer?.Release();
                cascadeBuffer = new ComputeBuffer(cascadeCount, 32);
                instanceBuffer = new ComputeBuffer(cascadeCount, 32);
                cascades = new CascadeData[cascadeCount];
                instances = new InstanceData[cascadeCount];
            }
            for (int i = 0; i < cascadeCount; i++)
            {
                float diameter = nearDiameter * (1 << i);
                float texel = diameter / resolution;
                cascades[i] = new CascadeData
                {
                    position = new Vector2(center.x, center.z), scale = diameter * .25f,
                    resolution = resolution, inverseResolution = 1f / resolution,
                    texelWidth = texel, weight = waves != null ? 1f : 0f, maxWavelength = diameter
                };
                instances[i] = new InstanceData
                {
                    // Our stitched topology owns the seam, not Crest tile snapping.
                    gridWidth = .0001f, farNormalsWeight = 1f,
                    normalScrollSpeeds = new Vector2(
                        Mathf.Pow(Mathf.Log(1f + 2f * texel) * 1.875f, 1.4f),
                        Mathf.Pow(Mathf.Log(1f + 4f * texel) * 1.875f, 1.4f))
                };
            }
            cascadeBuffer.SetData(cascades);
            instanceBuffer.SetData(instances);
            float forceUnderwater = 0f;
            if (enableUnderwater)
            {
                if (waterViewState == null) waterViewState = new ShuimianWaterViewState();
                forceUnderwater = waterViewState.Update(camera, combineShader, waves?.Displacement,
                    new Vector2(center.x, center.z), new Vector2(nearDiameter, farDiameter), center.y, cascadeCount, time);
            }
            int msaa = camera.allowMSAA
                ? (camera.targetTexture != null ? camera.targetTexture.antiAliasing : UniversalRenderPipeline.asset.msaaSampleCount) : 1;
            if (properties == null) properties = new MaterialPropertyBlock();
            waterRenderer.SetPropertyBlock(null);
            for (int level = 0; level < levels; level++)
            {
                properties.Clear();
                properties.SetBuffer("_CrestCascadeData", cascadeBuffer);
                properties.SetBuffer("_CrestPerCascadeInstanceData", instanceBuffer);
                properties.SetInt("_LD_SliceIndex", level);
                properties.SetFloat("_SliceCount", levels);
                properties.SetFloat("_CrestLodAlphaBlackPointFade", 0f);
                properties.SetFloat("_CrestLodAlphaBlackPointWhitePointFade", 1f);
                properties.SetVector("_OceanCenterPosWorld", center);
                properties.SetVector("_CameraForward", camera.transform.forward);
                properties.SetVector("_CrestFloatingOriginOffset", Vector3.zero);
                properties.SetFloat("_CrestTime", time);
                properties.SetInt("_CrestDepthTextureOffset", msaa > 1 ? 1 : 0);
                properties.SetFloat("_CrestForceUnderwater", forceUnderwater);
                properties.SetTexture("_LD_TexArray_SeaFloorDepth", seaFloorDepth);
                if (waves != null) properties.SetTexture("_LD_TexArray_AnimatedWaves", waves.Displacement);
                if (enableFoam && waves?.Foam != null) properties.SetTexture("_LD_TexArray_Foam", waves.Foam);
                if (reflectionTexture != null) properties.SetTexture("_ReflectionTex", reflectionTexture);
                waterRenderer.SetPropertyBlock(properties, level);
            }
            WaveFieldCenter = new Vector2(center.x, center.z);
            WaveFieldDiameters = new Vector2(nearDiameter, farDiameter);
            WaveFieldCascadeCount = cascadeCount;
            SurfacePrepared?.Invoke(camera);
        }
        void SetKeyword(string keyword, bool enabled)
        {
            if (enabled) surfaceMaterial.EnableKeyword(keyword); else surfaceMaterial.DisableKeyword(keyword);
        }
        void ClearProperties()
        {
            if (waterRenderer == null) return;
            waterRenderer.SetPropertyBlock(null);
            int count = ringMaterials != null ? ringMaterials.Length : 0;
            for (int i = 0; i < count; i++) waterRenderer.SetPropertyBlock(null, i);
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= PrepareCamera;
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            ClearProperties();
            waves?.Dispose(); waves = null;
            cascadeBuffer?.Release(); cascadeBuffer = null;
            instanceBuffer?.Release(); instanceBuffer = null;
            waterDepth?.Dispose(); waterDepth = null; seaFloorDepth = null;
            reflections?.Dispose(); reflections = null;
            underwaterRenderer?.Dispose(); underwaterRenderer = null;
            waterViewState?.Dispose(); waterViewState = null;
            ReleaseMaterial();
            ReleaseObject(surface); surface = null; waterRenderer = null;
            ReleaseObject(mesh); mesh = null;
            horizon = null;
        }
        void ReleaseMaterial()
        {
            if (waterRenderer != null) waterRenderer.sharedMaterials = new Material[0];
            ringMaterials = null;
            ReleaseObject(surfaceMaterial); surfaceMaterial = null; materialSource = null;
            ReleaseObject(filteredNormals); filteredNormals = null; normalSource = null;
        }
        static void ReleaseObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
