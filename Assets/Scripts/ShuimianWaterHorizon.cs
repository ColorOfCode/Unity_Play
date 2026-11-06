using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shuimian.Gameplay
{
    /// <summary>
    /// Extends the existing mesh's outer border to cover each camera's frustum.
    /// Interior LOD vertices and seam topology stay fixed. No extra plane or
    /// object is generated, and there is no dependency on Crest C# components.
    /// </summary>
    public sealed class ShuimianWaterHorizon
    {
        struct BorderVertex
        {
            public int index;
            public float xSign, zSign;
        }

        readonly Mesh mesh;
        readonly Vector3[] vertices;
        readonly BorderVertex[] border;
        readonly Vector3[] corners = new Vector3[4];
        readonly float horizontalPadding, coverageMargin;
        readonly float boundsCenterY, boundsHeight;
        float preparedHalfExtent;

        public float PreparedHalfExtent => preparedHalfExtent;

        /// <param name="nativeHalfExtent">Unstretched half-width of the generated square mesh.</param>
        /// <param name="verticalBounds">Conservative wave displacement bound, in world units.</param>
        public ShuimianWaterHorizon(Mesh mesh, float nativeHalfExtent, float verticalBounds)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (!Finite(nativeHalfExtent) || nativeHalfExtent <= 0f)
                throw new ArgumentOutOfRangeException(nameof(nativeHalfExtent));
            this.mesh = mesh;
            preparedHalfExtent = nativeHalfExtent;
            vertices = mesh.vertices;

            var originalBounds = mesh.bounds;
            float waveBound = Finite(verticalBounds) ? Mathf.Max(0f, verticalBounds) : 0f;
            horizontalPadding = Mathf.Max(waveBound,
                originalBounds.extents.x - nativeHalfExtent,
                originalBounds.extents.z - nativeHalfExtent);
            coverageMargin = Mathf.Max(2f, horizontalPadding + 2f);
            boundsCenterY = originalBounds.center.y;
            boundsHeight = Mathf.Max(originalBounds.size.y, 2f * waveBound, .01f);

            var outer = new List<BorderVertex>();
            float epsilon = Mathf.Max(.0001f, nativeHalfExtent * .00001f);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                bool xBorder = Mathf.Abs(Mathf.Abs(vertex.x) - nativeHalfExtent) <= epsilon;
                bool zBorder = Mathf.Abs(Mathf.Abs(vertex.z) - nativeHalfExtent) <= epsilon;
                if (!xBorder && !zBorder) continue;
                outer.Add(new BorderVertex
                {
                    index = i,
                    xSign = xBorder ? Mathf.Sign(vertex.x) : 0f,
                    zSign = zBorder ? Mathf.Sign(vertex.z) : 0f
                });
            }
            border = outer.ToArray();
        }

        /// <summary>
        /// Call after positioning the surface at center with unit world scale.
        /// Coverage only grows for this mesh's lifetime, so alternating Scene and
        /// Game views does not repeatedly upload vertices or move the far edge.
        /// </summary>
        public float EnsureCoverage(Camera camera, Vector3 center)
        {
            if (camera == null || mesh == null || border.Length == 0 || !Finite(center))
                return preparedHalfExtent;
            float near = camera.nearClipPlane, far = camera.farClipPlane;
            if (!Finite(near) || !Finite(far) || far <= 0f || near > far)
                return preparedHalfExtent;

            float required = 0f;
            // Both planes matter for orthographic cameras and for a camera that
            // is away from a fixed surface center. Reusing this array allocates
            // nothing during normal camera rendering.
            for (int plane = 0; plane < 2; plane++)
            {
                float distance = plane == 0 ? far : Mathf.Max(0f, near);
                camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), distance,
                    Camera.MonoOrStereoscopicEye.Mono, corners);
                for (int i = 0; i < corners.Length; i++)
                {
                    // Camera projection ignores Transform scale. Rotation plus
                    // position also works when a camera has a scaled parent.
                    Vector3 world = camera.transform.position + camera.transform.rotation * corners[i];
                    if (!Finite(world)) return preparedHalfExtent;
                    required = Mathf.Max(required, Mathf.Abs(world.x - center.x), Mathf.Abs(world.z - center.z));
                }
            }
            required += coverageMargin;
            if (!Finite(required) || required <= preparedHalfExtent) return preparedHalfExtent;

            // Doubling gives bounded headroom and avoids an upload for every tiny
            // change of an automatically adjusted Scene-view clipping distance.
            float extent = preparedHalfExtent;
            while (extent < required)
            {
                float next = extent * 2f;
                if (!Finite(next)) return preparedHalfExtent;
                extent = next;
            }
            float boundsWidth = 2f * (extent + horizontalPadding);
            if (!Finite(boundsWidth)) return preparedHalfExtent;
            for (int i = 0; i < border.Length; i++)
            {
                var edge = border[i];
                Vector3 vertex = vertices[edge.index];
                if (edge.xSign != 0f) vertex.x = edge.xSign * extent;
                if (edge.zSign != 0f) vertex.z = edge.zSign * extent;
                vertices[edge.index] = vertex;
            }
            mesh.SetVertices(vertices);
            var coverageBounds = new Bounds(new Vector3(0f, boundsCenterY, 0f),
                new Vector3(boundsWidth, boundsHeight, boundsWidth));
            mesh.bounds = coverageBounds;
            // Keep the outer submesh's bounds current as well as renderer bounds.
            // Its triangles now reach farther than the bounds calculated at build time.
            int last = mesh.subMeshCount - 1;
            if (last >= 0)
            {
                var descriptor = mesh.GetSubMesh(last);
                descriptor.bounds = coverageBounds;
                mesh.SetSubMesh(last, descriptor, MeshUpdateFlags.DontRecalculateBounds);
            }
            preparedHalfExtent = extent;
            return extent;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
