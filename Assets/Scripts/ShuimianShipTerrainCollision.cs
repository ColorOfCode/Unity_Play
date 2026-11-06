using UnityEngine;

namespace Shuimian.Gameplay
{
    /// <summary>
    /// Swept hull queries plus shoreline occupancy for a transform-driven boat.
    /// Only X/Z is constrained; the FFT float controller continues to own Y.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Shuimian/船体地形阻挡")]
    public class ShuimianShipTerrainCollision : MonoBehaviour
    {
        [Header("水面与船体范围")]
        public ShuimianWaterMesh water;
        [Tooltip("根据模型自动计算船体的水平范围及船底位置。")]
        public bool autoFitHull = true;
        [Min(.1f)] public float hullHeight = 2.5f;
        [Tooltip("关闭自动匹配时使用，相对移动目标的局部坐标。")]
        public Vector3 hullCenter = new Vector3(0f, -.25f, 0f);
        public Vector3 hullSize = new Vector3(3f, 2.5f, 8f);
        [Header("地形阻挡")]
        [Tooltip("包含 Terrain 所在层；默认包含全部层，包括场景地形所在的第 31 层。")]
        public LayerMask blockingLayers = ~0;
        [Min(.001f)] public float skin = .05f;
        [Tooltip("沿移动路径检查岸线的最大间距（米）。")]
        [Min(.05f)] public float maxStepDistance = .5f;
        [Tooltip("船体内部高度采样的最大间距（米）。")]
        [Min(.25f)] public float terrainSampleSpacing = 1f;

        TerrainCollider[] terrains;
        float nextTerrainRefresh;
        Transform fittedTarget;
        Vector3 fittedCenter, fittedSize;
        readonly RaycastHit[] hits = new RaycastHit[32];

        void OnEnable()
        {
            fittedTarget = null; terrains = null; nextTerrainRefresh = -1f;
        }
        void OnValidate() => fittedTarget = null;

        void GetHull(Transform target, out Vector3 center, out Vector3 size)
        {
            center = hullCenter; size = hullSize;
            if (autoFitHull)
            {
                if (fittedTarget != target)
                {
                    var renderers = target.GetComponentsInChildren<Renderer>();
                    Bounds bounds = default;
                    bool found = false;
                    foreach (var renderer in renderers)
                    {
                        // Mesh bounds transformed into the ship's own axes avoid
                        // fitting a large world AABB when the ship is already yawed.
                        Matrix4x4 matrix = target.worldToLocalMatrix * renderer.localToWorldMatrix;
                        Bounds local = renderer.localBounds;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            var sign = new Vector3((corner & 1) == 0 ? -1 : 1,
                                (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                            Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                            if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                            else bounds.Encapsulate(point);
                        }
                    }
                    fittedSize = found ? new Vector3(bounds.size.x, Mathf.Max(.1f, hullHeight), bounds.size.z) : hullSize;
                    fittedCenter = found ? new Vector3(bounds.center.x, bounds.min.y + fittedSize.y * .5f, bounds.center.z) : hullCenter;
                    fittedTarget = target;
                }
                center = fittedCenter; size = fittedSize;
            }
            size = new Vector3(Mathf.Max(.1f, size.x), Mathf.Max(.1f, size.y), Mathf.Max(.1f, size.z));
        }
        void RefreshTerrains()
        {
            if (terrains != null && Time.realtimeSinceStartup < nextTerrainRefresh) return;
            terrains = FindObjectsOfType<TerrainCollider>();
            nextTerrainRefresh = Time.realtimeSinceStartup + .5f;
        }
        bool Solid(TerrainCollider terrain)
        {
            return terrain != null && terrain.enabled && terrain.gameObject.activeInHierarchy
                && !terrain.isTrigger && terrain.terrainData != null
                && (blockingLayers.value & (1 << terrain.gameObject.layer)) != 0;
        }
        float SeaLevel(Transform target)
        {
            if (water == null)
            {
                var floating = GetComponent<ShuimianShipFloat>();
                water = floating != null ? floating.water : FindObjectOfType<ShuimianWaterMesh>();
            }
            return water != null ? water.transform.position.y : target.position.y;
        }

        void WorldHull(Transform target, out Vector3 center, out Vector3 half, out Quaternion yaw)
        {
            GetHull(target, out var localCenter, out var localSize);
            Vector3 scale = target.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            center = Vector3.Scale(localCenter, scale);
            half = Vector3.Scale(localSize, scale) * .5f;
            yaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
        }

        /// <summary>Prevent wave troughs from lowering the hull through the sea floor.</summary>
        public float ClampFloatHeight(Transform target, float desiredHeight)
        {
            if (target == null) return desiredHeight;
            RefreshTerrains();
            WorldHull(target, out var center, out var half, out var yaw);
            float minimumHeight = Penetration(target.position, center, half, yaw, 0f, out _);
            return Mathf.Max(desiredHeight, minimumHeight);
        }

        public Vector3 ConstrainMove(Transform target, Vector3 requested)
        {
            requested.y = 0f;
            if (target == null || requested.sqrMagnitude < .00000001f) return Vector3.zero;
            RefreshTerrains();
            int terrainLayers = 0;
            foreach (var terrain in terrains) if (Solid(terrain)) terrainLayers |= 1 << terrain.gameObject.layer;
            if (terrainLayers == 0) return requested;

            WorldHull(target, out var center, out var half, out var yaw);
            float seaLevel = Mathf.Min(SeaLevel(target), target.position.y);
            Vector3 position = target.position, original = position, remaining = requested;
            // Crests cannot lift the navigation footprint over land. Troughs use
            // the actual lower hull position so it cannot climb through a bank.
            for (int iteration = 0; iteration < 3 && remaining.sqrMagnitude > .00000001f; iteration++)
            {
                float distance = remaining.magnitude;
                Vector3 direction = remaining / distance;
                float allowed = distance;
                Vector3 normal = Vector3.zero;
                float startPenetration = Penetration(position, center, half, yaw, seaLevel, out _, out float startOccupation);
                if (startPenetration <= .0001f)
                {
                    Vector3 boxCenter = position + yaw * new Vector3(center.x, 0f, center.z);
                    boxCenter.y = seaLevel + center.y;
                    int count = Physics.BoxCastNonAlloc(boxCenter, half, direction, hits, yaw,
                        distance + skin, terrainLayers, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                    {
                        if (!(hits[i].collider is TerrainCollider terrain) || !Solid(terrain)) continue;
                        Vector3 horizontalNormal = hits[i].normal; horizontalNormal.y = 0f;
                        if (horizontalNormal.sqrMagnitude < .00001f) continue;
                        horizontalNormal.Normalize();
                        if (Vector3.Dot(direction, horizontalNormal) >= -.0001f) continue;
                        float stop = Mathf.Max(0f, hits[i].distance - skin);
                        if (stop < allowed) { allowed = stop; normal = horizontalNormal; }
                    }
                }
                // Heightfield casts do not report a starting overlap. Sweep the
                // whole footprint as well, including low FPS / very large moves.
                float step = Mathf.Max(.05f, maxStepDistance);
                int steps = Mathf.Max(1, Mathf.CeilToInt(allowed / step));
                float previous = 0f;
                float permittedPenetration = Mathf.Max(0f, startPenetration);
                float permittedOccupation = startOccupation;
                bool escaping = startPenetration > .001f;
                bool Blocked(float penetration, float occupation)
                {
                    if (penetration <= 0f) return false;
                    // Turning an elongated hull into a bank can create a starting
                    // overlap. Allow retreat, but never let equal-height land be
                    // traversed merely because its maximum height stays constant.
                    return escaping
                        ? penetration > permittedPenetration + .00001f || occupation >= permittedOccupation - .00001f
                        : penetration > permittedPenetration;
                }
                for (int sample = 1; sample <= steps; sample++)
                {
                    float at = allowed * sample / steps;
                    float penetration = Penetration(position + direction * at, center, half, yaw, seaLevel, out var terrainNormal, out float occupation);
                    if (Blocked(penetration, occupation))
                    {
                        float low = previous, high = at;
                        for (int search = 0; search < 10; search++)
                        {
                            float mid = (low + high) * .5f;
                            float test = Penetration(position + direction * mid, center, half, yaw, seaLevel, out _, out float testOccupation);
                            if (Blocked(test, testOccupation)) high = mid;
                            else low = mid;
                        }
                        allowed = low;
                        terrainNormal.y = 0f;
                        normal = terrainNormal.sqrMagnitude > .00001f ? terrainNormal.normalized : -direction;
                        break;
                    }
                    previous = at;
                    permittedPenetration = Mathf.Max(0f, penetration);
                    permittedOccupation = occupation;
                    escaping = penetration > .001f;
                }
                position += direction * allowed;
                if (allowed >= distance - .00001f) break;
                remaining -= direction * allowed;
                if (normal.sqrMagnitude < .00001f) break;
                // Slide only in X/Z; never climb a slope or replace the wave Y.
                float into = Vector3.Dot(remaining, normal);
                if (into < 0f) remaining -= normal * into;
                else break;
                remaining.y = 0f;
            }
            Vector3 actual = position - original; actual.y = 0f;
            return actual;
        }

        float Penetration(Vector3 position, Vector3 center, Vector3 half, Quaternion yaw, float seaLevel, out Vector3 normal)
            => Penetration(position, center, half, yaw, seaLevel, out normal, out _);

        float Penetration(Vector3 position, Vector3 center, Vector3 half, Quaternion yaw, float seaLevel, out Vector3 normal, out float occupation)
        {
            float worst = float.NegativeInfinity;
            normal = Vector3.zero;
            occupation = 0f;
            float bottom = seaLevel + center.y - half.y;
            int across = Mathf.Clamp(Mathf.CeilToInt(half.x * 2f / Mathf.Max(.25f, terrainSampleSpacing)), 2, 16);
            int length = Mathf.Clamp(Mathf.CeilToInt(half.z * 2f / Mathf.Max(.25f, terrainSampleSpacing)), 2, 32);
            for (int z = 0; z <= length; z++)
                for (int x = 0; x <= across; x++)
                {
                    Vector3 point = position + yaw * new Vector3(center.x + Mathf.Lerp(-half.x, half.x, x / (float)across),
                        0f, center.z + Mathf.Lerp(-half.z, half.z, z / (float)length));
                    float pointPenetration = float.NegativeInfinity;
                    foreach (var terrain in terrains)
                    {
                        if (!Solid(terrain)) continue;
                        var data = terrain.terrainData;
                        Vector3 local = terrain.transform.InverseTransformPoint(new Vector3(point.x, terrain.transform.position.y, point.z));
                        float u = local.x / data.size.x, v = local.z / data.size.z;
                        if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                        int holeX = Mathf.Clamp((int)(u * data.holesResolution), 0, data.holesResolution - 1);
                        int holeZ = Mathf.Clamp((int)(v * data.holesResolution), 0, data.holesResolution - 1);
                        if (data.IsHole(holeX, holeZ)) continue;
                        float height = terrain.transform.TransformPoint(new Vector3(local.x, data.GetInterpolatedHeight(u, v), local.z)).y;
                        float penetration = height - bottom + skin;
                        pointPenetration = Mathf.Max(pointPenetration, penetration);
                        if (penetration > worst)
                        {
                            worst = penetration;
                            normal = terrain.transform.TransformDirection(data.GetInterpolatedNormal(u, v));
                        }
                    }
                    occupation += Mathf.Max(0f, pointPenetration);
                }
            return worst;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var controller = GetComponent<ShipController>();
            Transform target = controller != null ? controller.Target : transform;
            GetHull(target, out var center, out var size);
            var previous = Gizmos.matrix;
            var position = target.position;
            if (water != null) position.y = water.transform.position.y;
            Gizmos.matrix = Matrix4x4.TRS(position, Quaternion.Euler(0f, target.eulerAngles.y, 0f), target.lossyScale);
            Gizmos.color = Color.yellow; Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = previous;
        }
#endif
    }
}
