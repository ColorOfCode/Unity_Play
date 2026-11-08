using System.Collections.Generic;
using UnityEngine;

namespace Shuimian.Gameplay
{
    /// <summary>Live mesh bounds shared by wakes, buoyancy and terrain collision. No transform-size cache.</summary>
    public static class ShuimianShipGeometry
    {
        static readonly List<Renderer> renderers = new List<Renderer>();
        static bool Valid(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;
            if ((renderer.hideFlags & HideFlags.DontSave) != 0 || (renderer.gameObject.hideFlags & HideFlags.DontSave) != 0) return false;
            if (renderer is SkinnedMeshRenderer skin) return skin.sharedMesh != null;
            var filter = renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>() : null;
            return filter != null && filter.sharedMesh != null;
        }
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z)
            && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
        public static bool TryLocalBounds(Transform target, out Bounds bounds)
            => Gather(target, target != null ? target.worldToLocalMatrix : Matrix4x4.identity, out bounds);
        static bool Gather(Transform target, Matrix4x4 worldToFrame, out Bounds bounds)
        {
            bounds = default;
            if (target == null) return false;
            target.GetComponentsInChildren<Renderer>(false, renderers);
            bool found = false;
            foreach (var renderer in renderers)
            {
                if (!Valid(renderer)) continue;
                var local = renderer.localBounds;
                var matrix = worldToFrame * renderer.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                    var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                    if (!Finite(point)) continue;
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            renderers.Clear();
            return found && bounds.size.sqrMagnitude > .000001f;
        }
        public struct Footprint
        {
            public Vector3 center, forward, right;
            public float width, length, minY, height;
            public float Waterline(float draftFraction) => minY + height * Mathf.Clamp01(draftFraction);
        }
        public static bool TryFootprint(Transform target, Vector3 forward, out Footprint footprint)
        {
            footprint = default;
            forward.y = 0;
            if (forward.sqrMagnitude < .00001f) forward = Vector3.forward;
            forward.Normalize();
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var frame = Matrix4x4.TRS(target != null ? target.position : Vector3.zero, rotation, Vector3.one);
            if (!Gather(target, frame.inverse, out var bounds)) return false;
            footprint = new Footprint
            {
                center = frame.MultiplyPoint3x4(bounds.center), forward = forward,
                right = Vector3.Cross(Vector3.up, forward), width = Mathf.Max(.2f, bounds.size.x),
                length = Mathf.Max(.5f, bounds.size.z), minY = target.position.y + bounds.min.y, height = bounds.size.y
            };
            return true;
        }
    }
}
