using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace ObscuredByClouds.Gameplay
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FogOfWarMap : MonoBehaviour
    {
        public static FogOfWarMap Active { get; private set; }
        [Header("探索")]
        public Transform target;
        [Min(0.1f)] public float revealRadius = 3f;
        [Min(0.05f)] public float edgeSoftness = 0.6f;
        [Range(256, 4096)] public int resolution = 2048;
        [Range(0.02f, 0.5f)] public float updateInterval = 0.1f;
        [Header("地图范围（X / Z 世界坐标）")]
        public bool useTerrainBounds = true;
        public Rect mapBounds = new Rect(0, 0, 512, 512);
        // Kept only for serialization compatibility; appearance now belongs to the material.
        [HideInInspector]
        public Color shadowColor = new Color(0.23f, 0.28f, 0.36f, 1f);
        [HideInInspector] public Color cloudColor = new Color(0.72f, 0.78f, 0.84f, 1f);
        [HideInInspector] public float cloudScale = 12f;
        [HideInInspector] public float cloudSpeed = 0.25f;
        [Header("探索存档")]
        public bool saveExploration = true;
        [Tooltip("同一场景使用相同编号会续用探索记录；更换编号可开始独立记录。")]
        public string saveId = "main-map-v1";
        [Min(1f)] public float autoSaveSeconds = 10f;

        public Texture2D Mask { get; private set; }
        public Rect Bounds => _bounds;
        byte[] _pixels;
        int _size;
        Rect _bounds;
        Vector3 _last;
        bool _hasLast, _dirty, _initialized;
        float _nextUpdate, _nextSave;
        string _savePath;

        void Start() { Initialize(); }
        bool _runtime;
        Vector3 _previewPoint;
        float _previewRadius = -1, _previewSoftness;
        void OnEnable() { Active = this; }

        public void PrepareForRendering()
        {
            bool running = Application.IsPlaying(gameObject);
            if (_initialized && (_runtime != running || _size != Mathf.Clamp(resolution,256,4096) || CalculateBounds() != _bounds))
            { SaveExploration(); ReleaseMask(); }
            if (!_initialized) Initialize();
            if (!running && target != null && (_previewPoint != target.position || _previewRadius != revealRadius || _previewSoftness != edgeSoftness))
            {
                Array.Clear(_pixels,0,_pixels.Length); _hasLast = false;
                RevealAt(target.position,false); Upload();
                _previewPoint = target.position; _previewRadius = revealRadius; _previewSoftness = edgeSoftness;
            }
        }
        public void Initialize()
        {
            if (_initialized) return;
            if (target == null)
            {
                var actor = FindObjectOfType<ClickToMoveCharacter>();
                if (actor != null) target = actor.transform;
            }
            _runtime = Application.IsPlaying(gameObject);
            _bounds = CalculateBounds();
            if (_bounds.width <= 0 || _bounds.height <= 0)
                throw new InvalidOperationException("Fog Of War: 地图范围宽高必须大于零。");
            _size = Mathf.Clamp(resolution, 256, 4096);
            _pixels = new byte[_size * _size];
            Mask = new Texture2D(_size, _size, TextureFormat.R8, false, true)
            { name = "Permanent Exploration Mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            string sceneKey = gameObject.scene.path + "|" + saveId;
            _savePath = Path.Combine(Application.persistentDataPath,
                "fog-" + Hash128.Compute(sceneKey) + ".bin");
            _initialized = true;
            Active = this;
            if (saveExploration && _runtime) LoadExploration();
            // First rendered frame already has the spawn area's reveal.
            if (target != null) RevealAt(target.position, false);
            Upload();
            _nextSave = Time.unscaledTime + autoSaveSeconds;
        }

        Rect CalculateBounds()
        {
            Rect result = mapBounds;
            if (useTerrainBounds && Terrain.activeTerrains.Length > 0)
            {
                bool first = true;
                Bounds bounds = default;
                foreach (var terrain in Terrain.activeTerrains)
                {
                    if (terrain.terrainData == null) continue;
                    Vector3 size = terrain.terrainData.size;
                    var b = new Bounds(terrain.transform.position + size * 0.5f, size);
                    if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
                }
                if (!first) result = new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);
            }
            return result;
        }

        void LateUpdate()
        {
            PrepareForRendering();
            if (!_runtime) return;
            if (target != null && Time.unscaledTime >= _nextUpdate)
            {
                Vector3 p = target.position;
                if (!_hasLast || (new Vector2(p.x - _last.x, p.z - _last.z)).sqrMagnitude > 0.0025f)
                { RevealAt(p, true); Upload(); }
                _nextUpdate = Time.unscaledTime + updateInterval;
            }
            if (saveExploration && Time.unscaledTime >= _nextSave)
            { SaveExploration(); _nextSave = Time.unscaledTime + autoSaveSeconds; }
        }

        // Rasterize a swept disk, so fast movement has no gaps. Only max() updates are allowed.
        public void RevealAt(Vector3 point, bool connectToPrevious = true)
        {
            if (!_initialized) Initialize();
            Vector2 end = new Vector2(point.x, point.z);
            Vector2 start = _hasLast && connectToPrevious ? new Vector2(_last.x, _last.z) : end;
            // Teleports reveal the destination, rather than an unexplored corridor across the map.
            if (Vector2.Distance(start, end) > Mathf.Max(20f, revealRadius * 4f)) start = end;
            float cellX = _bounds.width / _size, cellZ = _bounds.height / _size;
            float radius = Mathf.Max(0.1f, revealRadius) + Mathf.Sqrt(cellX * cellX + cellZ * cellZ);
            float feather = Mathf.Max(edgeSoftness, Mathf.Max(cellX, cellZ));
            float outer = radius + feather;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(start.x, end.x) - outer - _bounds.x) / cellX), 0, _size - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(start.x, end.x) + outer - _bounds.x) / cellX), 0, _size - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(start.y, end.y) - outer - _bounds.y) / cellZ), 0, _size - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(start.y, end.y) + outer - _bounds.y) / cellZ), 0, _size - 1);
            Vector2 segment = end - start;
            float lengthSq = segment.sqrMagnitude;
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 cell = new Vector2(_bounds.x + (x + 0.5f) * cellX, _bounds.y + (z + 0.5f) * cellZ);
                float t = lengthSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(cell - start, segment) / lengthSq) : 0f;
                float distance = Vector2.Distance(cell, start + segment * t);
                byte value = (byte)Mathf.RoundToInt(255f * (1f - Mathf.SmoothStep(0f, 1f, (distance - radius) / feather)));
                int i = z * _size + x;
                if (value > _pixels[i]) { _pixels[i] = value; _dirty = true; }
            }
            _last = point; _hasLast = true;
        }

        public void Upload()
        {
            if (Mask == null || _pixels == null) return;
            Mask.SetPixelData(_pixels, 0); Mask.Apply(false, false);
        }

        public float ExplorationAt(Vector3 world)
        {
            if (!_initialized) return 0f;
            float u = (world.x - _bounds.x) / _bounds.width, v = (world.z - _bounds.y) / _bounds.height;
            if (u < 0 || v < 0 || u >= 1 || v >= 1) return 0f;
            return _pixels[Mathf.FloorToInt(v * _size) * _size + Mathf.FloorToInt(u * _size)] / 255f;
        }

        public void SaveExploration()
        {
            if (!_initialized || !_runtime || !_dirty || !saveExploration) return;
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temporary = _savePath + ".tmp";
                using (var file = File.Create(temporary))
                using (var zip = new GZipStream(file, CompressionMode.Compress))
                using (var writer = new BinaryWriter(zip))
                {
                    writer.Write(0x464F5731); writer.Write(_size);
                    writer.Write(_bounds.x); writer.Write(_bounds.y); writer.Write(_bounds.width); writer.Write(_bounds.height);
                    writer.Write(_pixels);
                }
                if (File.Exists(_savePath)) File.Replace(temporary, _savePath, null);
                else File.Move(temporary, _savePath);
                _dirty = false;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException)
            { Debug.LogWarning("Fog Of War: 无法保存探索记录: " + ex.Message, this); }
        }

        void LoadExploration()
        {
            if (!File.Exists(_savePath)) return;
            try
            {
                using (var zip = new GZipStream(File.OpenRead(_savePath), CompressionMode.Decompress))
                using (var reader = new BinaryReader(zip))
                {
                    if (reader.ReadInt32() != 0x464F5731 || reader.ReadInt32() != _size) return;
                    var rect = new Rect(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    if (rect != _bounds) return;
                    byte[] data = reader.ReadBytes(_pixels.Length);
                    if (data.Length == _pixels.Length) _pixels = data;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException)
            { Debug.LogWarning("Fog Of War: 忽略无效探索记录: " + ex.Message, this); }
        }

        [ContextMenu("重置本地图探索记录")]
        public void ResetExploration()
        {
            if (!_initialized) Initialize();
            Array.Clear(_pixels, 0, _pixels.Length); _hasLast = false; _dirty = true;
            if (target != null) RevealAt(target.position, false);
            Upload(); SaveExploration();
        }
        void OnApplicationPause(bool paused) { if (paused) SaveExploration(); }
        void OnApplicationQuit() { SaveExploration(); }
        void OnDisable() { SaveExploration(); ReleaseMask(); if (Active == this) Active = null; }
        void OnDestroy() { ReleaseMask(); if (Active == this) Active = null; }
        void ReleaseMask()
        {
            if (Mask != null) { if (Application.isPlaying) Destroy(Mask); else DestroyImmediate(Mask); }
            Mask = null; _pixels = null; _initialized = false; _hasLast = false; _previewRadius = -1;
        }
        void OnDrawGizmosSelected()
        {
            if (target == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI / 32f, b = (i + 1) * Mathf.PI / 32f;
                Gizmos.DrawLine(target.position + new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)) * revealRadius,
                    target.position + new Vector3(Mathf.Cos(b), 0.1f, Mathf.Sin(b)) * revealRadius);
            }
        }
    }
}
