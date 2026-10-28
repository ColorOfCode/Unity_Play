using UnityEngine;

namespace Shuimian.VFX
{
    /// <summary>
    /// 自适应无限平面：
    /// - 一个以相机为中心的极坐标网格（同心环 + 圆周分段）
    /// - 离相机越近环间距越小（面数越密），越远环间距越大（面数越稀）
    /// - 近处最小格尺寸 nearCellSize、远处最大格尺寸 farCellSize 均可调
    /// - ExecuteAlways：编辑模式下拖动参数立即生效
    /// - 网格自动跟随相机（编辑模式跟 SceneView，运行时跟 Camera.main）
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Shuimian/自适应无限平面")]
    public class AdaptiveGridPlane : MonoBehaviour
    {
        [Header("平面范围")]
        [Tooltip("平面半径（米）。值越大越接近“无限大”，但面数越多")]
        [Min(10f)] public float planeRadius = 5000f;

        [Header("密度控制（编辑模式可直接拖动）")]
        [Tooltip("近处最小单元格尺寸（米）：越小近处面数越多")]
        [Min(0.001f)] public float nearCellSize = 0.1f;
        [Tooltip("远处最大单元格尺寸（米）：即远处的最低密度上限，越大远处面数越少")]
        [Min(0.01f)] public float farCellSize = 50f;
        [Tooltip("圆周方向分段数：固定分段，远处弧长变大后角度方向自动变稀")]
        [Range(8, 512)] public int angularSegments = 192;
        [Tooltip("环间距增长系数（每环 ×此值），1.05~1.3 合理；越大过渡越快、面数越少")]
        [Range(1.01f, 2f)] public float ringGrowth = 1.15f;

        [Header("跟随设置")]
        [Tooltip("是否跟随相机移动")]
        public bool followCamera = true;
        [Tooltip("相机移动超过近处格尺寸的一半时才重建网格（防抖动）")]
        [Min(1f)] public float rebuildThresholdFactor = 0.5f;

        [Header("调试")]
        [Tooltip("是否绘制网格线框 Gizmo（仅编辑模式）")]
        public bool drawGizmos = false;

        Mesh _mesh;
        bool _needsRebuild = true;

        void OnEnable()
        {
            _needsRebuild = true;
        }

        void OnValidate()
        {
            nearCellSize = Mathf.Max(nearCellSize, 0.001f);
            farCellSize = Mathf.Max(farCellSize, nearCellSize);
            planeRadius = Mathf.Max(planeRadius, nearCellSize * 10f);
            _needsRebuild = true;
        }

        void Update()
        {
            if (followCamera)
            {
                Camera cam = GetFollowCamera();
                if (cam != null)
                {
                    float snap = Mathf.Max(nearCellSize, 0.001f);
                    Vector3 target = new Vector3(
                        Mathf.Round(cam.transform.position.x / snap) * snap,
                        transform.position.y,
                        Mathf.Round(cam.transform.position.z / snap) * snap);

                    // 相机移动超过 snap*factor 才跟随重建，避免每帧重建
                    if ((target - transform.position).sqrMagnitude >
                        snap * snap * rebuildThresholdFactor * rebuildThresholdFactor)
                    {
                        transform.position = target;
                        _needsRebuild = true;
                    }
                }
            }

            if (_needsRebuild)
            {
                _needsRebuild = false;
                Rebuild();
            }
        }

        /// <summary>编辑模式跟随 SceneView 相机，运行时跟随 Camera.main</summary>
        Camera GetFollowCamera()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var sv = UnityEditor.SceneView.lastActiveSceneView;
                return sv != null ? sv.camera : null;
            }
#endif
            return Camera.main;
        }

        /// <summary>生成极坐标自适应网格</summary>
        public void Rebuild()
        {
            var mf = GetComponent<MeshFilter>();
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "AdaptiveGridPlane" };
            }
            _mesh.Clear();

            // ---- 计算每环半径：间距从 nearCellSize 几何增长，封顶 farCellSize ----
            var radii = new System.Collections.Generic.List<float>();
            float spacing = Mathf.Max(nearCellSize, 0.001f);
            float r = 0f;
            while (r < planeRadius)
            {
                r += spacing;
                radii.Add(r);
                spacing = Mathf.Min(spacing * ringGrowth, farCellSize);
            }

            int rings = radii.Count;
            int segs = Mathf.Max(angularSegments, 3);
            int vertCount = 1 + rings * segs; // 中心点 + 各环

            if (vertCount > 300000)
            {
                Debug.LogWarning($"[AdaptiveGridPlane] 顶点数过多({vertCount})，请增大 nearCellSize / farCellSize 或减小 planeRadius", this);
            }

            // ---- 顶点：中心 + 同心环 ----
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            verts[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = Vector2.zero;

            float invRadius = 1f / planeRadius;
            float step = 2f * Mathf.PI / segs;
            for (int k = 0; k < rings; k++)
            {
                float rk = radii[k];
                for (int j = 0; j < segs; j++)
                {
                    float ang = j * step;
                    float x = Mathf.Cos(ang) * rk;
                    float z = Mathf.Sin(ang) * rk;
                    int idx = 1 + k * segs + j;
                    verts[idx] = new Vector3(x, 0f, z);
                    normals[idx] = Vector3.up;
                    uvs[idx] = new Vector2(x * invRadius, z * invRadius);
                }
            }

            // ---- 三角形：中心扇形 segs 个 + 环间四边形 (rings-1)*segs*2 个 ----
            int totalTris = segs + 2 * segs * Mathf.Max(rings - 1, 0);
            var tris = new int[totalTris * 3];
            int t = 0;

            // 中心扇形：center(0) -> 第一环
            for (int j = 0; j < segs; j++)
            {
                int jn = (j + 1) % segs;
                // (center, ring[j+1], ring[j]) 保证法线 +Y
                tris[t++] = 0;
                tris[t++] = 1 + jn;
                tris[t++] = 1 + j;
            }

            // 环间四边形
            for (int k = 0; k < rings - 1; k++)
            {
                int baseK = 1 + k * segs;
                int baseK1 = baseK + segs;
                for (int j = 0; j < segs; j++)
                {
                    int jn = (j + 1) % segs;
                    int a = baseK + j;   // 内环 j
                    int b = baseK + jn;  // 内环 j+1
                    int c = baseK1 + j;  // 外环 j
                    int d = baseK1 + jn; // 外环 j+1
                    tris[t++] = a; tris[t++] = b; tris[t++] = c;
                    tris[t++] = b; tris[t++] = d; tris[t++] = c;
                }
            }

            _mesh.vertices = verts;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateBounds();
            mf.sharedMesh = _mesh;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>右键菜单：立即重建网格</summary>
        [ContextMenu("重建网格")]
        void RebuildMenu() => Rebuild();

        /// <summary>右键菜单：打印当前网格统计</summary>
        [ContextMenu("打印统计信息")]
        void LogStats()
        {
            int rings = CountRings(out float _, out int verts, out int tris);
            Debug.Log($"[AdaptiveGridPlane] 环数={rings}, 顶点={verts}, 三角面={tris}, 近处格={nearCellSize}m, 远处格={farCellSize}m, 半径={planeRadius}m", this);
        }

        int CountRings(out float lastSpacing, out int verts, out int tris)
        {
            int rings = 0;
            float spacing = Mathf.Max(nearCellSize, 0.001f);
            float r = 0f;
            while (r < planeRadius)
            {
                r += spacing;
                rings++;
                spacing = Mathf.Min(spacing * ringGrowth, farCellSize);
            }
            lastSpacing = spacing;
            int segs = Mathf.Max(angularSegments, 3);
            verts = 1 + rings * segs;
            tris = segs + (rings - 1) * segs * 2;
            return rings;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 2f);
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawLine(transform.position - Vector3.right * planeRadius, transform.position + Vector3.right * planeRadius);
            Gizmos.DrawLine(transform.position - Vector3.forward * planeRadius, transform.position + Vector3.forward * planeRadius);
        }
#endif
    }
}
