using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Snowfield
{
    /// <summary>Opt-in terrain snow matching. Never replaces or edits shared materials.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Snowfield/地形雪材质匹配")]
    public sealed class TerrainSnowMaterialMatcher : MonoBehaviour
    {
        [InspectorName("指定地形（可留空）"), Tooltip("留空时自动选择模型所在的 Terrain。")]
        public Terrain terrainOverride;
        [InspectorName("指定渲染器（可留空）"), Tooltip("留空时处理本模型下所有支持匹配的 Renderer，包括未显示的 LOD。")]
        public Renderer[] targetRenderers = new Renderer[0];
        [InspectorName("雪层白名单（可留空）"), Tooltip("可选白名单。留空时优先采用 Terrain 上 SnowDepthSurface 的 Snow Layers；没有显式配置才根据雪层名称识别。")]
        public TerrainLayer[] snowLayers = new TerrainLayer[0];
        [InspectorName("最低雪层覆盖率"), Range(0.05f, 1)] public float minimumSnowCoverage = .5f;
        [Tooltip("在模型底部中心附近采样，单位米；不会搜索远处的雪。")]
        [InspectorName("脚下采样半径（米）"), Min(0)] public float sampleRadius = .5f;
        [Tooltip("允许模型底部与 Terrain 的最大高度差，单位米。")]
        [InspectorName("最大离地距离（米）"), Min(0)] public float maximumGroundDistance = 2;
        [Tooltip("相对地形层法线强度的倍率。0.35 保持目前雪帽较柔和的外观。")]
        [InspectorName("雪层法线倍率"), Range(0, 2)] public float normalStrengthMultiplier = .35f;
        [InspectorName("检查间隔（秒）"), Range(.1f, 5)] public float updateInterval = .35f;

        public string Status { get; private set; } = "等待检查";
        public Terrain CurrentTerrain { get; private set; }
        public TerrainLayer MatchedLayer { get; private set; }
        public float SnowCoverage { get; private set; }
        public bool IsMatching { get; private set; }
        public int CompatibleMaterialCount => bindings.Count;

        sealed class Binding
        {
            public Renderer renderer;
            public int slot;
            public Material source;
            public readonly MaterialPropertyBlock baseline = new MaterialPropertyBlock();
            public readonly MaterialPropertyBlock working = new MaterialPropertyBlock();
            public bool applied;
            public int appliedSignature;
        }
        readonly List<Binding> bindings = new List<Binding>();
        Renderer[] renderers = new Renderer[0];
        int bindingSignature;
        bool bindingsDirty = true, sampleDirty = true;
        double nextUpdate;
        Vector3 previousProbe;
        Terrain sampledTerrain;
        TerrainData sampledData;
        int sampledLayersSignature;
        float[] sampledWeights;
        bool hasSample;
        float lastSampleTime;

        static readonly int DetailAlbedo = Shader.PropertyToID("_DetailAlbedo");
        static readonly int DetailNormal = Shader.PropertyToID("_DetailNormalMap");
        static readonly int DetailMask = Shader.PropertyToID("_DetailMetallicGlossMap");
        static readonly int DetailColor = Shader.PropertyToID("_2ndColor");
        static readonly int DetailNormalPower = Shader.PropertyToID("_2ndNormalPower");
        static readonly int Tiling = Shader.PropertyToID("_Tiling");
        static readonly int Match = Shader.PropertyToID("_SnowTerrainMatch");
        static readonly int UV = Shader.PropertyToID("_SnowTerrainUV");
        static readonly int Origin = Shader.PropertyToID("_SnowTerrainOrigin");
        static readonly int MaskScale = Shader.PropertyToID("_SnowTerrainMaskScale");
        static readonly int MaskOffset = Shader.PropertyToID("_SnowTerrainMaskOffset");

        public static bool Supports(Material material)
        {
            return material && material.shader && string.Equals(material.GetTag("SnowTerrainMatch", false, ""), "True", StringComparison.OrdinalIgnoreCase)
                && material.HasProperty(DetailAlbedo) && material.HasProperty(DetailNormal)
                && material.HasProperty(DetailMask) && material.HasProperty(Match)
                && material.HasProperty(UV) && material.HasProperty(Origin)
                && material.HasProperty(MaskScale) && material.HasProperty(MaskOffset);
        }
        void OnEnable()
        {
            bindingsDirty = sampleDirty = true; nextUpdate = 0;
            TerrainCallbacks.textureChanged += OnTerrainTextureChanged;
            TerrainCallbacks.heightmapChanged += OnTerrainHeightChanged;
#if UNITY_EDITOR
            EditorApplication.update += EditorTick;
            Undo.undoRedoPerformed += Invalidate;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.playModeStateChanged += PlayModeChanged;
#endif
        }
        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
            Undo.undoRedoPerformed -= Invalidate;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
#endif
            TerrainCallbacks.textureChanged -= OnTerrainTextureChanged;
            TerrainCallbacks.heightmapChanged -= OnTerrainHeightChanged;
            RestoreAll(); IsMatching = false; MatchedLayer = null;
            Status = "已停用，恢复原显示参数";
        }
        void OnDestroy() { RestoreAll(); }
        void OnValidate() { bindingsDirty = sampleDirty = true; nextUpdate = 0; }
        void OnTransformChildrenChanged() { bindingsDirty = true; nextUpdate = 0; }
        public void Invalidate() { sampleDirty = true; nextUpdate = 0; }
        void OnTerrainTextureChanged(Terrain terrain, string name, RectInt region, bool synched)
        { if (terrain == CurrentTerrain || terrain == sampledTerrain) Invalidate(); }
        void OnTerrainHeightChanged(Terrain terrain, RectInt region, bool synched)
        { if (terrain == CurrentTerrain || terrain == sampledTerrain) Invalidate(); }
        void Update()
        {
            if (Application.IsPlaying(gameObject) && Time.realtimeSinceStartup >= nextUpdate)
            { nextUpdate = Time.realtimeSinceStartup + updateInterval; Refresh(false); }
        }
#if UNITY_EDITOR
        void BeforeReload() { RestoreAll(); }
        void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            { RestoreAll(); IsMatching = false; MatchedLayer = null; }
            Invalidate();
        }
        void EditorTick()
        {
            if (!this || !isActiveAndEnabled || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = EditorApplication.timeSinceStartup + updateInterval;
            if (Refresh(false)) SceneView.RepaintAll();
        }
#endif
        [ContextMenu("立即匹配地形雪材质")]
        public void RefreshNow() { if (isActiveAndEnabled) Refresh(true); }
        bool CanEvaluate()
        {
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return false;
#if UNITY_EDITOR
            if (AssetDatabase.IsAssetImportWorkerProcess() || EditorUtility.IsPersistent(this)
                || PrefabStageUtility.GetPrefabStage(gameObject) != null) return false;
#endif
            return true;
        }
        bool Refresh(bool force)
        {
            if (!CanEvaluate()) return StopMatching("放入场景后才匹配地形");
            bool changed = UpdateBindings();
            if (bindings.Count == 0) return StopMatching("未匹配：没有支持的雪模型 Shader") || changed;
            var bounds = WorldBounds(bindings[0].renderer);
            foreach (var b in bindings) if (b.renderer) bounds.Encapsulate(WorldBounds(b.renderer));
            var probe = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            CurrentTerrain = ResolveTerrain(probe);
            if (!CurrentTerrain || !CurrentTerrain.terrainData) return StopMatching("未匹配：模型下方没有 Terrain") || changed;
            float groundHeight = CurrentTerrain.SampleHeight(probe) + CurrentTerrain.transform.position.y;
            // Rocks are often partially buried. An intersection with their
            // vertical bounds counts as contact, rather than measuring only minY.
            float groundDistance = groundHeight < bounds.min.y ? bounds.min.y - groundHeight
                : groundHeight > bounds.max.y ? groundHeight - bounds.max.y : 0;
            if (groundDistance > maximumGroundDistance)
                return StopMatching("未匹配：模型离地形过远") || changed;
            var data = CurrentTerrain.terrainData;
            var layers = data.terrainLayers;
            int layersSignature = LayersSignature(layers);
            if (force || sampleDirty || !hasSample || sampledTerrain != CurrentTerrain || sampledData != data
                || sampledLayersSignature != layersSignature || (previousProbe - probe).sqrMagnitude > .0001f
                || Time.realtimeSinceStartup - lastSampleTime >= 1)
            {
                sampledWeights = SampleWeights(CurrentTerrain, probe, layers.Length);
                sampledTerrain = CurrentTerrain; sampledData = data; sampledLayersSignature = layersSignature;
                previousProbe = probe; sampleDirty = false; hasSample = true;
                lastSampleTime = Time.realtimeSinceStartup;
            }
            SnowCoverage = 0; int best = -1; float bestWeight = 0, previousWeight = 0;
            var surface = CurrentTerrain.GetComponent<SnowDepthSurface>();
            for (int i = 0; i < layers.Length && i < sampledWeights.Length; ++i)
            {
                if (!IsSnowLayer(layers[i], surface)) continue;
                float weight = sampledWeights[i]; SnowCoverage += weight;
                if (layers[i] == MatchedLayer) previousWeight = weight;
                if (weight > bestWeight) { best = i; bestWeight = weight; }
            }
            float threshold = IsMatching ? Mathf.Max(0, minimumSnowCoverage - .05f) : minimumSnowCoverage;
            if (best < 0 || SnowCoverage < threshold)
                return StopMatching("未匹配：脚下雪层覆盖 " + SnowCoverage.ToString("P0") + "，低于阈值") || changed;
            // A narrow hysteresis avoids flicker on a border between two snow layers.
            if (MatchedLayer && previousWeight > 0 && previousWeight >= bestWeight - .05f)
                for (int i = 0; i < layers.Length; ++i) if (layers[i] == MatchedLayer) { best = i; break; }
            MatchedLayer = layers[best]; IsMatching = true;
            Status = "已匹配：" + MatchedLayer.name + "（雪层覆盖 " + SnowCoverage.ToString("P0") + "）";
            foreach (var b in bindings) changed |= Apply(b, CurrentTerrain, MatchedLayer, force);
            return changed;
        }
        bool UpdateBindings()
        {
            renderers = targetRenderers != null && targetRenderers.Length > 0
                ? targetRenderers : GetComponentsInChildren<Renderer>(true);
            int signature = 17;
            unchecked
            {
                foreach (var r in renderers)
                {
                    if (!r || r.GetComponentInParent<TerrainSnowMaterialMatcher>() != this) continue;
                    signature = signature * 31 + r.GetInstanceID();
                    foreach (var m in r.sharedMaterials)
                        signature = signature * 31 + (m ? m.GetInstanceID() ^ (Supports(m) ? 1231 : 1237) : 0);
                }
            }
            if (!bindingsDirty && signature == bindingSignature) return false;
            RestoreAll(); bindings.Clear(); bindingSignature = signature; bindingsDirty = false;
            foreach (var r in renderers)
            {
                if (!r || r.GetComponentInParent<TerrainSnowMaterialMatcher>() != this) continue;
                var materials = r.sharedMaterials;
                for (int slot = 0; slot < materials.Length; ++slot)
                    if (Supports(materials[slot])) bindings.Add(new Binding { renderer = r, slot = slot, source = materials[slot] });
            }
            return true;
        }
        static bool Contains(Terrain terrain, Vector3 p)
        {
            if (!terrain || !terrain.terrainData || !terrain.isActiveAndEnabled) return false;
            p -= terrain.transform.position; var size = terrain.terrainData.size;
            return p.x >= 0 && p.z >= 0 && p.x <= size.x && p.z <= size.z;
        }
        static Bounds WorldBounds(Renderer renderer)
        {
            // Static batching can replace MeshFilter geometry with combined
            // vertices; its native renderer bounds are already in world space.
            if (renderer.isPartOfStaticBatch) return renderer.bounds;
            // Disabled LOD renderers can retain stale native world bounds while
            // editing. Derive these from the mesh and current transform instead.
            var mesh = renderer.GetComponent<MeshFilter>();
            var skinned = renderer as SkinnedMeshRenderer;
            if ((!mesh || !mesh.sharedMesh) && !skinned) return renderer.bounds;
            Bounds local = skinned ? skinned.localBounds : mesh.sharedMesh.bounds;
            var t = renderer.transform;
            Vector3 x = t.TransformVector(new Vector3(local.extents.x, 0, 0));
            Vector3 y = t.TransformVector(new Vector3(0, local.extents.y, 0));
            Vector3 z = t.TransformVector(new Vector3(0, 0, local.extents.z));
            var extent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(t.TransformPoint(local.center), extent * 2);
        }
        Terrain ResolveTerrain(Vector3 p)
        {
            if (terrainOverride) return terrainOverride.gameObject.scene == gameObject.scene && Contains(terrainOverride, p) ? terrainOverride : null;
            foreach (var t in Terrain.activeTerrains) if (t.gameObject.scene == gameObject.scene && Contains(t, p)) return t;
            return null;
        }
        bool IsSnowLayer(TerrainLayer layer, SnowDepthSurface surface)
        {
            if (!layer) return false;
            if (snowLayers != null && snowLayers.Length > 0) return Array.IndexOf(snowLayers, layer) >= 0;
            if (surface && surface.useExplicitLayers)
            {
                if (surface.snowLayers != null) foreach (var item in surface.snowLayers)
                    if (item != null && item.layer == layer && item.depth > .001f) return true;
                return false;
            }
            string name = layer.name.ToLowerInvariant().Replace("_", "").Replace(" ", "");
            return (name.Contains("snow") || name.Contains("雪")) && !name.Contains("nosnow") && !name.Contains("无雪");
        }
        static int LayersSignature(TerrainLayer[] layers)
        {
            int result = layers.Length;
            unchecked { foreach (var layer in layers) result = result * 31 + (layer ? layer.GetInstanceID() : 0); }
            return result;
        }
        float[] SampleWeights(Terrain terrain, Vector3 probe, int count)
        {
            var data = terrain.terrainData; var p = probe - terrain.transform.position; var size = data.size;
            float rx = sampleRadius / size.x * (data.alphamapWidth - 1), rz = sampleRadius / size.z * (data.alphamapHeight - 1);
            float cx = p.x / size.x * (data.alphamapWidth - 1), cz = p.z / size.z * (data.alphamapHeight - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(cx - rx), 0, data.alphamapWidth - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(cz - rz), 0, data.alphamapHeight - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(cx + rx), 0, data.alphamapWidth - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt(cz + rz), 0, data.alphamapHeight - 1);
            var map = data.GetAlphamaps(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            var weights = new float[count];
            for (int sample = 0; sample < 5; ++sample)
            {
                float x = cx, z = cz;
                if (sample > 0) { x += ((sample & 1) == 0 ? rx : -rx); z += (sample < 3 ? -rz : rz); }
                x = Mathf.Clamp(x - x0, 0, map.GetLength(1) - 1); z = Mathf.Clamp(z - z0, 0, map.GetLength(0) - 1);
                int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
                int nx = Mathf.Min(ix + 1, map.GetLength(1) - 1), nz = Mathf.Min(iz + 1, map.GetLength(0) - 1);
                float factor = sample == 0 ? 2f / 6f : 1f / 6f;
                for (int layer = 0; layer < count; ++layer)
                    weights[layer] += Mathf.Lerp(Mathf.Lerp(map[iz, ix, layer], map[iz, nx, layer], x - ix),
                        Mathf.Lerp(map[nz, ix, layer], map[nz, nx, layer], x - ix), z - iz) * factor;
            }
            return weights;
        }
        int AppearanceSignature(Binding b, Terrain terrain, TerrainLayer layer)
        {
            unchecked
            {
                int hash = layer.GetInstanceID();
                hash = hash * 31 + (layer.diffuseTexture ? layer.diffuseTexture.GetInstanceID() : 0);
                hash = hash * 31 + (layer.normalMapTexture ? layer.normalMapTexture.GetInstanceID() : 0);
                hash = hash * 31 + (layer.maskMapTexture ? layer.maskMapTexture.GetInstanceID() : 0);
                hash = hash * 31 + layer.tileSize.GetHashCode(); hash = hash * 31 + layer.tileOffset.GetHashCode();
                hash = hash * 31 + layer.maskMapRemapMin.GetHashCode(); hash = hash * 31 + layer.maskMapRemapMax.GetHashCode();
                hash = hash * 31 + layer.diffuseRemapMin.GetHashCode(); hash = hash * 31 + layer.diffuseRemapMax.GetHashCode();
                hash = hash * 31 + layer.normalScale.GetHashCode(); hash = hash * 31 + layer.metallic.GetHashCode(); hash = hash * 31 + layer.smoothness.GetHashCode();
                hash = hash * 31 + terrain.transform.position.GetHashCode(); hash = hash * 31 + normalStrengthMultiplier.GetHashCode();
                return hash * 31 + b.source.GetColor(DetailColor).GetHashCode();
            }
        }
        bool Apply(Binding b, Terrain terrain, TerrainLayer layer, bool force)
        {
            if (!b.renderer || !b.source) return false;
            int signature = AppearanceSignature(b, terrain, layer);
            if (b.applied && b.appliedSignature == signature && !force) return false;
            if (!b.applied) b.renderer.GetPropertyBlock(b.baseline, b.slot);
            b.renderer.GetPropertyBlock(b.working, b.slot);
            if (b.working.isEmpty) b.renderer.GetPropertyBlock(b.working); // preserve renderer-wide overrides
            b.working.SetTexture(DetailAlbedo, layer.diffuseTexture ? layer.diffuseTexture : Texture2D.whiteTexture);
            b.working.SetTexture(DetailNormal, layer.normalMapTexture ? layer.normalMapTexture : Texture2D.grayTexture);
            b.working.SetTexture(DetailMask, layer.maskMapTexture ? layer.maskMapTexture : Texture2D.whiteTexture);
            var tint = layer.diffuseRemapMax - layer.diffuseRemapMin;
            var color = b.source.GetColor(DetailColor);
            b.working.SetColor(DetailColor, new Color(color.r * tint.x, color.g * tint.y, color.b * tint.z, color.a));
            b.working.SetFloat(DetailNormalPower, layer.normalMapTexture ? layer.normalScale * normalStrengthMultiplier : 0);
            b.working.SetFloat(Tiling, 1f / Mathf.Max(.001f, Mathf.Abs(layer.tileSize.x)));
            b.working.SetFloat(Match, 1);
            b.working.SetVector(UV, new Vector4(1f / Mathf.Max(.001f, Mathf.Abs(layer.tileSize.x)),
                1f / Mathf.Max(.001f, Mathf.Abs(layer.tileSize.y)), layer.tileOffset.x, layer.tileOffset.y));
            b.working.SetVector(Origin, terrain.transform.position);
            b.working.SetVector(MaskScale, layer.maskMapTexture ? layer.maskMapRemapMax - layer.maskMapRemapMin : Vector4.zero);
            b.working.SetVector(MaskOffset, layer.maskMapTexture ? layer.maskMapRemapMin : new Vector4(layer.metallic, 1, 0, layer.smoothness));
            b.renderer.SetPropertyBlock(b.working, b.slot); b.applied = true; b.appliedSignature = signature;
            return true;
        }
        bool RestoreAll()
        {
            bool changed = false;
            foreach (var b in bindings) if (b.applied)
            {
                if (b.renderer && b.slot < b.renderer.sharedMaterials.Length)
                    b.renderer.SetPropertyBlock(b.baseline.isEmpty ? null : b.baseline, b.slot);
                b.applied = false; b.appliedSignature = 0; changed = true;
            }
            return changed;
        }
        bool StopMatching(string reason)
        {
            bool changed = RestoreAll(); IsMatching = false; MatchedLayer = null; Status = reason;
            return changed;
        }
    }
}
