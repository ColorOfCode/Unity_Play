using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ObscuredByClouds.Gameplay
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(FogOfWarMap))]
    public sealed class FogOfWarSurface : MonoBehaviour
    {
        [Header("材质（颜色、Noise 纹理和 UV 流动）")]
        public Material fogMaterial;
        [Header("云层高度与接触深度（类似浅水区效果）")]
        [InspectorName("云雾高度（世界 Y）")]
        [Tooltip("水平云层的世界坐标高度；超过该高度的模型直接露出。编辑和运行时修改都立即生效。")]
        public float fogHeight = 32f;
        [InspectorName("接触渐变深度（向下，米）")]
        [Tooltip("从云层平面向下计算：接触处最淡，低于平面这个距离后达到完整浓度。") ]
        [Min(0.001f)] public float contactFadeDepth = 2f;
        [Header("水平网格范围（世界 X / Z）")]
        public bool useMapBounds = true;
        public Rect surfaceBounds = new Rect(0, 0, 512, 512);
        [Header("自适应网格密度（米）")]
        [Min(0.2f)] public float nearCellSize = 1f;
        [Min(1f)] public float farCellSize = 8f;
        [Min(1f)] public float nearDistance = 15f;
        [Min(1f)] public float transitionDistance = 40f;
        public bool showInSceneView = true;
        public bool animateInEditMode = true;
        public static FogOfWarSurface Active { get; private set; }
        public float SurfaceHeight => fogHeight;
        public int LastVertexCount { get; private set; }
        FogOfWarMap _map;
        MaterialPropertyBlock _properties;
        readonly Dictionary<int, Cache> _cache = new Dictionary<int, Cache>();
        sealed class Cache { public Mesh mesh; public Vector2 focus; public int settings; }

        void OnEnable()
        {
            _map = GetComponent<FogOfWarMap>();
            _properties = new MaterialPropertyBlock();
            Active = this;
            RenderPipelineManager.beginCameraRendering += RenderCamera;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorUpdate;
#endif
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= RenderCamera;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorUpdate;
#endif
            foreach (var item in _cache.Values) Release(item.mesh);
            _cache.Clear();
            if (Active == this) Active = null;
        }
        static void Release(Object value)
        { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }

#if UNITY_EDITOR
        double _nextEditorFrame;
        void EditorUpdate()
        {
            if (Application.IsPlaying(gameObject) || !animateInEditMode || fogMaterial == null) return;
            if (UnityEditor.EditorApplication.timeSinceStartup < _nextEditorFrame) return;
            _nextEditorFrame = UnityEditor.EditorApplication.timeSinceStartup + 1.0 / 20.0;
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
        void RenderCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!isActiveAndEnabled || _map == null || !_map.isActiveAndEnabled || fogMaterial == null) return;
            if (camera.cameraType != CameraType.Game && !(camera.cameraType == CameraType.SceneView && showInSceneView)) return;
#if UNITY_EDITOR
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null) return;
#endif
            _map.PrepareForRendering();
            if (_map.Mask == null) return;
            Rect area = useMapBounds ? _map.Bounds : surfaceBounds;
            if (area.width <= 0 || area.height <= 0) return;
            float height = SurfaceHeight;
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            var plane = new Plane(Vector3.up, Vector3.up * height);
            Vector3 point = plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : camera.transform.position;
            float snap = Mathf.Max(1f, nearCellSize * 2f);
            Vector2 focus = new Vector2(Mathf.Round(point.x / snap) * snap, Mathf.Round(point.z / snap) * snap);
            int settings = area.GetHashCode() ^ nearCellSize.GetHashCode() ^ (farCellSize.GetHashCode() * 17)
                ^ (nearDistance.GetHashCode() * 37) ^ (transitionDistance.GetHashCode() * 67);
            int id = camera.GetInstanceID();
            if (!_cache.TryGetValue(id, out Cache cache))
            {
                // Bound editor camera churn and release all native meshes when disabled.
                if (_cache.Count >= 8) { foreach (var old in _cache.Values) Release(old.mesh); _cache.Clear(); }
                cache = new Cache(); _cache.Add(id, cache);
            }
            if (cache.mesh == null || cache.focus != focus || cache.settings != settings)
            {
                Release(cache.mesh);
                cache.mesh = BuildMesh(area, focus, nearCellSize, farCellSize, nearDistance, transitionDistance);
                cache.focus = focus; cache.settings = settings;
            }
            LastVertexCount = cache.mesh.vertexCount;
            Rect map = _map.Bounds;
            _properties.Clear();
            _properties.SetFloat("_FogHeight", fogHeight);
            _properties.SetFloat("_HeightFade", Mathf.Max(0.001f, contactFadeDepth));
            _properties.SetTexture("_ExploredMask", _map.Mask);
            _properties.SetVector("_MapBounds", new Vector4(map.x, map.y, map.width, map.height));
            _properties.SetFloat("_FogTime", Application.IsPlaying(gameObject) ? Time.time : (float)Time.realtimeSinceStartupAsDouble);
            Graphics.DrawMesh(cache.mesh, Matrix4x4.Translate(Vector3.up * height), fogMaterial, gameObject.layer,
                camera, 0, _properties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }

        public static Mesh BuildMesh(Rect area, Vector2 focus, float nearSize, float farSize, float nearRange, float transition)
        {
            nearSize = Mathf.Max(0.2f, nearSize); farSize = Mathf.Max(nearSize, farSize);
            var xs = Axis(area.xMin, area.xMax, focus.x, nearSize, farSize, nearRange, transition);
            var zs = Axis(area.yMin, area.yMax, focus.y, nearSize, farSize, nearRange, transition);
            if ((long)xs.Count * zs.Count > 200000)
            {
                float multiplier = Mathf.Sqrt(xs.Count * (float)zs.Count / 100000f);
                xs = Axis(area.xMin, area.xMax, focus.x, nearSize * multiplier, farSize * multiplier, nearRange, transition);
                zs = Axis(area.yMin, area.yMax, focus.y, nearSize * multiplier, farSize * multiplier, nearRange, transition);
            }
            var vertices = new Vector3[xs.Count * zs.Count];
            for (int z = 0; z < zs.Count; z++) for (int x = 0; x < xs.Count; x++)
                vertices[z * xs.Count + x] = new Vector3(xs[x], 0, zs[z]);
            var indices = new int[(xs.Count - 1) * (zs.Count - 1) * 6];
            int at = 0;
            for (int z = 0; z < zs.Count - 1; z++) for (int x = 0; x < xs.Count - 1; x++)
            {
                int i = z * xs.Count + x;
                indices[at++] = i; indices[at++] = i + xs.Count; indices[at++] = i + 1;
                indices[at++] = i + 1; indices[at++] = i + xs.Count; indices[at++] = i + xs.Count + 1;
            }
            var mesh = new Mesh { name = "Fog Surface (near dense / far sparse)", hideFlags = HideFlags.HideAndDontSave };
            mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices; mesh.triangles = indices; mesh.RecalculateBounds();
            return mesh;
        }
        static List<float> Axis(float minimum, float maximum, float focus, float nearSize, float farSize, float nearRange, float transition)
        {
            focus = Mathf.Clamp(focus, minimum, maximum);
            var values = new List<float> { focus };
            for (int sign = -1; sign <= 1; sign += 2)
            {
                float p = focus;
                for (int i = 0; i < 2048; i++)
                {
                    float t = Mathf.Clamp01((Mathf.Abs(p - focus) - Mathf.Max(1, nearRange)) / Mathf.Max(1, transition));
                    p += sign * Mathf.Lerp(nearSize, farSize, t);
                    if (p <= minimum || p >= maximum) break;
                    values.Add(p);
                }
            }
            values.Add(minimum); values.Add(maximum); values.Sort();
            for (int i = values.Count - 1; i > 0; i--) if (values[i] - values[i - 1] < 0.001f) values.RemoveAt(i);
            return values;
        }
    }
}
