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
        [Header("镜面反射（R1_MirrorPlane3）")]
        [Tooltip("镜面反射总开关。")]
        public bool enablePlanarReflection = true;
        [Tooltip("0 完全关闭镜像，1 完整混合镜像。")]
        [Range(0f, 1f)] public float mirrorReflectionStrength = 1f;
        [Tooltip("水面法线对镜像扭曲的影响；同时受水材质 Overall Normal Strength 影响。0 为平面镜像，1 为完整水面扰动。")]
        [Range(0f, 1f)] public float mirrorNormalInfluence = .2f;
        [Tooltip("天空是否出现在镜面反射中。")]
        public bool reflectionSky = true;
        [Tooltip("是否在镜像中渲染实时阴影。")]
        public bool reflectionShadows;
        [Tooltip("是否在镜像中渲染场景雾效。")]
        public bool reflectionFog = true;
        [Tooltip("是否对镜像应用场景后处理。")]
        public bool reflectionPostProcessing;
        [Tooltip("反射后处理的混合强度：0 为原始镜像，1 为完整后处理；只在上方开关启用时生效。")]
        [Range(0f, 1f)] public float reflectionPostProcessingStrength = .5f;
        [Range(128, 2048)] public int reflectionTextureSize = 512;
        [Tooltip("参与镜像的模型和特效所在 Layer，默认 Everything。")]
        public LayerMask reflectionLayers = ~0;
        [Range(0f, .5f)] public float reflectionClipOffset = .07f;
        [Header("镜面表面与距离淡出")]
        [Tooltip("参与反射判断的镜面 Renderer；留空时自动使用生成的水面 LOD 模型。")]
        public MeshRenderer MyMeshRenderer;
        [Tooltip("镜面平面的位置和方向，负 Z 轴是镜面正面；留空时自动使用水平海面。")]
        public Transform MyForwardTransform;
        [Tooltip("相机到镜面最近位置的最大捕获距离（米），同时作为下方 Alpha 曲线的最远距离。")]
        [Min(.01f)] public float maxRenderingDistance = 100f;
        [Tooltip("沿用 R1：最大距离末尾的淡出比例，相机远离镜面时将镜像颜色过渡到 Fade Color。")]
        [Range(0f, 1f)] public float fadeDistance = .735f;
        [Tooltip("镜像 Alpha 的上限，最终还会乘 Mirror Reflection Strength 与距离 Alpha 曲线。")]
        [Range(0f, 1f)] public float maxBlend = 1f;
        [Tooltip("距离淡出的颜色；Alpha=0 时淡回水体本色，Alpha=1 时采用此颜色。")]
        public Color FadeColor = new Color(.06759523f, .2789998f, .2924528f, 1f);
        [Tooltip("仅从第二层镜像开始按递归曲线暗化；普通单层水面不受影响。")]
        public bool useRecursiveDarkening;
        public AnimationCurve recursiveDarkeningCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 3.2204275f, 3.2204275f), new Keyframe(1f, 1f, 0f, 0f));
        [Tooltip("X=水面像素到相机的距离 / Max Rendering Distance（0最近、1最远）；Y=镜像 Alpha。")]
        public AnimationCurve reflectionDistanceCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [Header("水下表现")]
        [Tooltip("启用水面背面的折射与内部反射，以及沿波浪水线裁切的水下深度雾。")]
        public bool enableUnderwater = true;
        [Tooltip("水下雾、散射颜色与焦散的独立材质；留空自动使用默认 ShuimianUnderwater 材质。水面材质保持独立。")]
        public Material underwaterMaterial;
        [Tooltip("复用水面材质中的焦散贴图、强度、尺寸和运动参数。")]
        public bool enableCaustics = true;
        [Header("浪尖与岸边泡沫")]
        public bool enableFoam = true;
        [Min(0f)] public float foamWaveStrength = 3.27f;
        [Range(0f, 1f)] public float foamWaveCoverage = .76f;
        [Min(.001f)] public float foamFadeRate = .8f;
        [Min(.01f)] public float foamShoreDepth = 1f;
        [Min(0f)] public float foamShoreStrength = 2f;
        [Header("模型岸线深度")]
        [Tooltip("将当前场景中与水面相交的静态不透明模型写入岸线水深，使岩石周围生成泡沫。")]
        public bool enableMeshShoreline = true;
        public LayerMask shorelineGeometryLayers = ~0;
        [Range(256, 2048)] public int shorelineGeometryResolution = 1024;
        [Tooltip("默认只捕获标记为 Static 的岸边模型，避免人物和船体写入静态岸线缓存。")]
        public bool shorelineStaticOnly = true;
        [Header("船与水面交互")]
        [Tooltip("船体的真实水面起伏与独立泡沫尾迹，按实际水平移动生成。")]
        public bool enableShipInteraction = true;
        public ComputeShader shipWakeShader;
        [Range(128, 1024)] public int shipWakeResolution = 512;
        [Tooltip("围绕船和尾迹的局部交互区域（米），与观察相机无关。")]
        [Min(32f)] public float shipWakeAreaSize = 192f;
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
        Texture2D mirrorDistanceLut, mirrorRecursiveLut;
        int mirrorDistanceCurveHash, mirrorRecursiveCurveHash;
        MeshRenderer waterRenderer;
        ComputeBuffer cascadeBuffer, instanceBuffer;
        Texture seaFloorDepth;
        ShuimianWaterDepth waterDepth;
        ShuimianMeshShoreDepth meshShoreDepth;
        ShuimianPlanarReflection reflections;
        ShuimianUnderwaterRenderer underwaterRenderer;
        ShuimianWaterViewState waterViewState;
        ShuimianWaveSimulation waves;
        ShuimianWakeSimulation shipWakes;
        ComputeShader activeShipWake;
        RenderTexture shipFoamTexture, effectiveFoamTexture;
        float shipInteractionTimeOverride = float.NaN;
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
        public RenderTexture FoamTexture => effectiveFoamTexture;
        public RenderTexture NativeFoamTexture => waves?.Foam;
        public RenderTexture ShipWakeField => shipWakes?.Field;
        public RenderTexture ShipFoamTexture => shipFoamTexture;
        public Texture GetReflectionTexture(Camera camera) => reflections?.GetTexture(camera);
        public Texture GetReflectionCoverageTexture(Camera camera) => reflections?.GetCoverageTexture(camera);
        public Texture GetUnprocessedReflectionTexture(Camera camera) => reflections?.GetUnprocessedTexture(camera);
        public MeshRenderer EffectiveMirrorRenderer => MyMeshRenderer != null ? MyMeshRenderer : waterRenderer;
        Vector3 MirrorPoint => MyForwardTransform != null ? MyForwardTransform.position : transform.position;
        Vector3 MirrorNormal => MyForwardTransform != null ? -MyForwardTransform.forward : Vector3.up;
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
            if (Application.isPlaying || (!(previewWavesInEditor && enableWaves) && !ShipInteractionConfigured)) return;
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
            surfaceMaterial.SetFloat("_ShuimianMirrorStrength", enablePlanarReflection ? Mathf.Clamp01(mirrorReflectionStrength) : 0f);
            surfaceMaterial.SetFloat("_ShuimianReflectSky", reflectionSky ? 1f : 0f);
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
            bool available = (enableWaves || ShipInteractionConfigured) && spectrumShader != null && fftShader != null && combineShader != null
                && SystemInfo.supportsComputeShaders && SystemInfo.supports2DArrayTextures;
            if (!available)
            {
                waves?.Dispose(); waves = null;
                if ((enableWaves || ShipInteractionConfigured) && !warnedAboutWaves)
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
        bool ShipInteractionConfigured => enableShipInteraction && shipWakeShader != null;
        float ShipInteractionTime
        {
            get
            {
                if (!float.IsNaN(shipInteractionTimeOverride)) return shipInteractionTimeOverride;
                // All views in the same simulation tick share one clock. This
                // deliberately does not use the natural FFT wave-time multiplier.
                double now = Time.time;
#if UNITY_EDITOR
                if (!Application.isPlaying) now = EditorApplication.timeSinceStartup - editorStartTime;
#endif
                return (float)(System.Math.Floor(now * 30.0) / 30.0);
            }
        }
        /// <summary>Drive wakes from an explicit timeline until ClearShipInteractionTimeOverride is called.</summary>
        public void PrepareShipInteraction(float time)
        {
            shipInteractionTimeOverride = time;
            UpdateShipInteraction(time);
        }
        public void ClearShipInteractionTimeOverride() => shipInteractionTimeOverride = float.NaN;
        void UpdateShipInteraction(float time)
        {
            if (!ShipInteractionConfigured || !SystemInfo.supportsComputeShaders || !SystemInfo.supports2DArrayTextures)
            {
                shipWakes?.Dispose(); shipWakes = null; activeShipWake = null;
                shipFoamTexture = null;
                return;
            }
            if (shipWakes == null || activeShipWake != shipWakeShader)
            {
                shipWakes?.Dispose();
                shipWakes = new ShuimianWakeSimulation(shipWakeShader);
                activeShipWake = shipWakeShader;
            }
            shipWakes.Update(this, time, shipWakeResolution, shipWakeAreaSize);
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
            if (enablePlanarReflection && mirrorReflectionStrength > 0f && maxBlend > 0f)
            {
                EnsureSurface();
                if (reflections == null) reflections = new ShuimianPlanarReflection();
                if (MirrorVisible(camera))
                    reflections.Render(context, camera, waterRenderer, transform.position.y,
                        reflectionTextureSize, reflectionLayers, reflectionClipOffset,
                        reflectionSky, reflectionShadows, reflectionFog, reflectionPostProcessing,
                        MyForwardTransform, EffectiveMirrorRenderer, Mathf.Clamp01(reflectionPostProcessingStrength));
                else reflections.ReleaseView(camera);
            }
            else if (reflections != null) { reflections.Dispose(); reflections = null; }
            PrepareSurface(camera, CurrentTime);
            // Enqueue only after reflection capture; its nested camera uses the
            // same URP renderer and must not consume this view's water passes.
            if (enableUnderwater && surfaceMaterial != null)
            {
                if (underwaterMaterial == null) underwaterMaterial = Resources.Load<Material>("ShuimianUnderwater");
                if (underwaterRenderer == null) underwaterRenderer = new ShuimianUnderwaterRenderer(combineShader);
                underwaterRenderer.Enqueue(camera, surfaceMaterial, waterRenderer, transform.position.y, underwaterMaterial);
            }
            else if (underwaterRenderer != null) { underwaterRenderer.Dispose(); underwaterRenderer = null; }
        }

        // Explicit time also supports deterministic rendering/verification without a game loop.
        public void PrepareSurface(Camera camera, float time, int reflectionDepth = 1, int reflectionRecursions = 2)
        {
            EnsureSurface(); EnsureMaterial(); EnsureWaves();
            if (surfaceMaterial == null) return;
            UpdateMirrorCurve(ref mirrorDistanceLut, ref mirrorDistanceCurveHash, reflectionDistanceCurve, true);
            UpdateMirrorCurve(ref mirrorRecursiveLut, ref mirrorRecursiveCurveHash, recursiveDarkeningCurve, false);
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
            var terrainDepth = waterDepth.Update(new Vector2(center.x, center.z), nearDiameter, cascadeCount, center.y);
            if (enableMeshShoreline)
            {
                if (meshShoreDepth == null) meshShoreDepth = new ShuimianMeshShoreDepth(gameObject.scene);
                seaFloorDepth = meshShoreDepth.Update(terrainDepth, new Vector2(center.x, center.z), nearDiameter,
                    cascadeCount, center.y, foamShoreDepth, shorelineGeometryLayers, shorelineGeometryResolution, shorelineStaticOnly);
            }
            else seaFloorDepth = terrainDepth;
            if (waves != null)
                waves.Update(new Vector2(center.x, center.z), nearDiameter, farDiameter, waveTextureResolution,
                    time, windSpeedKmh, windTurbulence, waveChop, enableWaves ? waveAmplitude : 0f, waveDirectionDegrees, cascadeCount,
                    seaFloorDepth, center.y, enableShallowAttenuation ? shallowAttenuation : 0f, shallowAttenuationMaxDepth);
            UpdateShipInteraction(ShipInteractionTime);
            bool hasShipField = shipWakes?.Field != null && shipWakes.SampleCount > 0;
            if (hasShipField && waves?.Displacement != null)
                shipWakes.ApplyDisplacement(waves.Displacement, new Vector2(center.x, center.z), nearDiameter, farDiameter, cascadeCount);
            int resolution = waves?.Displacement.width ?? Mathf.Clamp(waveTextureResolution, 16, 1024);
            if (enableFoam && waves != null)
                waves.UpdateFoam(new Vector2(center.x, center.z), nearDiameter, farDiameter, cascadeCount,
                    time, foamWaveStrength, foamWaveCoverage, foamFadeRate, seaFloorDepth, center.y,
                    foamShoreDepth, foamShoreStrength);
            // Keep wake coverage out of the native whitecap history, otherwise
            // repeated camera rendering would re-inject and prolong the trail.
            Texture nativeFoam = enableFoam ? waves?.Foam : null;
            shipFoamTexture = hasShipField && waves?.Displacement != null
                ? shipWakes.CompositeFoam(nativeFoam, waves.Displacement, new Vector2(center.x, center.z), nearDiameter, farDiameter, cascadeCount)
                : null;
            effectiveFoamTexture = shipFoamTexture != null ? shipFoamTexture : nativeFoam as RenderTexture;
            SetKeyword("_FOAM_ON", effectiveFoamTexture != null);
            SetKeyword("_FOAM3DLIGHTING_ON", effectiveFoamTexture != null && sourceMaterial.IsKeywordEnabled("_FOAM3DLIGHTING_ON"));
            bool mirrorVisible = MirrorVisible(camera);
            Texture reflectionTexture = mirrorVisible ? reflections?.GetTexture(camera) : null;
            Texture reflectionCoverage = reflectionTexture != null ? reflections?.GetCoverageTexture(camera) : null;
            Texture unprocessedReflection = reflectionTexture != null ? reflections?.GetUnprocessedTexture(camera) : null;
            SetKeyword("_PLANARREFLECTIONS_ON", reflectionTexture != null);
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
                properties.SetFloat("_ShuimianMirrorStrength", mirrorVisible ? Mathf.Clamp01(mirrorReflectionStrength) : 0f);
                properties.SetFloat("_ShuimianReflectSky", reflectionSky ? 1f : 0f);
                properties.SetFloat("_ShuimianMirrorNormalInfluence", Mathf.Clamp01(mirrorNormalInfluence));
                properties.SetVector("_ShuimianMirrorUVScaleOffset", reflections != null
                    ? reflections.GetScaleOffset(camera) : new Vector4(1f, 1f, 0f, 0f));
                properties.SetVector("_ShuimianMirrorDistanceParams", new Vector4(Mathf.Max(.01f, maxRenderingDistance),
                    Mathf.Clamp01(fadeDistance), Mathf.Clamp01(maxBlend), MirrorCameraDistance(camera)));
                properties.SetColor("_ShuimianMirrorFadeColor", FadeColor);
                properties.SetTexture("_ShuimianMirrorDistanceCurve", mirrorDistanceLut);
                properties.SetTexture("_ShuimianMirrorRecursiveCurve", mirrorRecursiveLut);
                properties.SetFloat("_ShuimianMirrorRecursiveDarkening", useRecursiveDarkening ? 1f : 0f);
                properties.SetVector("_ShuimianMirrorRecursionData", new Vector4(Mathf.Max(1, reflectionDepth),
                    Mathf.Max(1, reflectionRecursions), 0f, 0f));
                properties.SetTexture("_LD_TexArray_SeaFloorDepth", seaFloorDepth);
                if (waves != null) properties.SetTexture("_LD_TexArray_AnimatedWaves", waves.Displacement);
                if (effectiveFoamTexture != null) properties.SetTexture("_LD_TexArray_Foam", effectiveFoamTexture);
                if (reflectionTexture != null) properties.SetTexture("_ReflectionTex", reflectionTexture);
                properties.SetFloat("_ShuimianUseReflectionCoverage", reflectionCoverage != null ? 1f : 0f);
                if (reflectionCoverage != null) properties.SetTexture("_ShuimianReflectionCoverage", reflectionCoverage);
                properties.SetFloat("_ShuimianReflectionPostProcessingStrength", unprocessedReflection != null
                    ? Mathf.Clamp01(reflectionPostProcessingStrength) : 1f);
                if (unprocessedReflection != null) properties.SetTexture("_ShuimianReflectionUnprocessed", unprocessedReflection);
                waterRenderer.SetPropertyBlock(properties, level);
            }
            WaveFieldCenter = new Vector2(center.x, center.z);
            WaveFieldDiameters = new Vector2(nearDiameter, farDiameter);
            WaveFieldCascadeCount = cascadeCount;
            SurfacePrepared?.Invoke(camera);
        }
        float MirrorCameraDistance(Camera camera)
        {
            var target = EffectiveMirrorRenderer;
            if (target != null && target != waterRenderer)
                return Vector3.Distance(camera.transform.position, target.bounds.ClosestPoint(camera.transform.position));
            // Generated ocean bounds include the horizon and wave-height margin.
            // Its mean reflection plane gives the meaningful closest distance.
            return Mathf.Abs(Vector3.Dot(MirrorNormal, camera.transform.position - MirrorPoint));
        }
        bool MirrorVisible(Camera camera)
        {
            if (camera == null || !enablePlanarReflection || mirrorReflectionStrength <= 0f || maxBlend <= 0f) return false;
            var target = EffectiveMirrorRenderer;
            if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) return false;
            if (Vector3.Dot(MirrorNormal, camera.transform.position - MirrorPoint) < 0f
                || MirrorCameraDistance(camera) >= Mathf.Max(.01f, maxRenderingDistance)) return false;
            return target == waterRenderer || GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), target.bounds);
        }
        /// <summary>R1 distance/recursive blend with a safe zero-width fade and a common blend cap.</summary>
        public static float EvaluateMirrorBlend(float distance, float maxDistance, float fadeFraction, float blendLimit,
            bool recursive, AnimationCurve recursionCurve, int depth = 1, int recursions = 2)
        {
            maxDistance = Mathf.Max(.01f, maxDistance);
            float blend = fadeFraction <= 0f ? (distance < maxDistance ? 1f : 0f)
                : Mathf.Clamp01((maxDistance - distance) / (maxDistance * Mathf.Clamp01(fadeFraction)));
            if (depth > Mathf.Max(1, recursions)) blend = 0f;
            else if (recursive && depth > 1)
                blend = Mathf.Clamp01(recursionCurve != null ? recursionCurve.Evaluate(1f - (depth - 1f) / Mathf.Max(1, recursions))
                    : 1f - (depth - 1f) / Mathf.Max(1, recursions));
            return blend * Mathf.Clamp01(blendLimit);
        }
        static void UpdateMirrorCurve(ref Texture2D texture, ref int previousHash, AnimationCurve curve, bool falling)
        {
            int hash = falling ? 1 : 2;
            unchecked
            {
                if (curve != null)
                {
                    hash = hash * 31 + (int)curve.preWrapMode; hash = hash * 31 + (int)curve.postWrapMode;
                    foreach (var key in curve.keys)
                    {
                        hash = hash * 31 + key.time.GetHashCode(); hash = hash * 31 + key.value.GetHashCode();
                        hash = hash * 31 + key.inTangent.GetHashCode(); hash = hash * 31 + key.outTangent.GetHashCode();
                        hash = hash * 31 + key.inWeight.GetHashCode(); hash = hash * 31 + key.outWeight.GetHashCode();
                        hash = hash * 31 + (int)key.weightedMode;
                    }
                }
            }
            if (texture != null && previousHash == hash) return;
            const int count = 256;
            if (texture == null)
                texture = new Texture2D(count, 1, TextureFormat.RFloat, false, true)
                {
                    name = falling ? "Shuimian Mirror Distance Alpha" : "Shuimian Mirror Recursive Darkening",
                    hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
            var values = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (count - 1f);
                float value = curve != null && curve.length > 0 ? curve.Evaluate(t) : (falling ? 1f - t : t);
                values[i] = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
            }
            texture.SetPixelData(values, 0); texture.Apply(false, false); previousHash = hash;
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
            shipWakes?.Dispose(); shipWakes = null; activeShipWake = null;
            shipFoamTexture = null; effectiveFoamTexture = null; shipInteractionTimeOverride = float.NaN;
            cascadeBuffer?.Release(); cascadeBuffer = null;
            instanceBuffer?.Release(); instanceBuffer = null;
            waterDepth?.Dispose(); waterDepth = null; seaFloorDepth = null;
            meshShoreDepth?.Dispose(); meshShoreDepth = null;
            reflections?.Dispose(); reflections = null;
            underwaterRenderer?.Dispose(); underwaterRenderer = null;
            waterViewState?.Dispose(); waterViewState = null;
            ReleaseObject(mirrorDistanceLut); mirrorDistanceLut = null;
            ReleaseObject(mirrorRecursiveLut); mirrorRecursiveLut = null;
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
