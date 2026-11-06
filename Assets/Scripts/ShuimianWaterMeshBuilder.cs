using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shuimian.Gameplay
{
    /// <summary>Concentric square grids. Each ring doubles cell width; shared seam vertices prevent cracks.</summary>
    public static class ShuimianWaterMeshBuilder
    {
        public static Mesh Build(int subdivisions, float cellSize, int lodCount, float verticalBounds)
        {
            int n = Mathf.Clamp(subdivisions / 4 * 4, 8, 128);
            int levels = Mathf.Clamp(lodCount, 1, 9);
            float step = Mathf.Max(.05f, cellSize);
            var points = new List<Vector3>();
            var indices = new List<int>();
            var ringIndices = new List<int>[levels];
            var lookup = new Dictionary<long, int>();
            int Vertex(int x, int z)
            {
                long key = ((long)x << 32) | (uint)z;
                if (lookup.TryGetValue(key, out int index)) return index;
                index = points.Count;
                points.Add(new Vector3(x * step, 0f, z * step));
                lookup.Add(key, index);
                return index;
            }
            void Triangle(int a, int b, int c)
            {
                indices.Add(a); indices.Add(b); indices.Add(c);
            }
            for (int level = 0; level < levels; level++)
            {
                indices = new List<int>();
                ringIndices[level] = indices;
                int unit = 1 << level, quarter = n / 4;
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        bool inHole = x >= quarter && x < 3 * quarter && z >= quarter && z < 3 * quarter;
                        if (level > 0 && inHole) continue;
                        int px = (x - n / 2) * unit, pz = (z - n / 2) * unit;
                        int bl = Vertex(px, pz), tl = Vertex(px, pz + unit);
                        int tr = Vertex(px + unit, pz + unit), br = Vertex(px + unit, pz);
                        // Split the edge beside the hole at the finer ring's midpoint.
                        int edge = -1;
                        if (level > 0)
                        {
                            if (x == quarter - 1 && z >= quarter && z < 3 * quarter) edge = 2;
                            else if (x == 3 * quarter && z >= quarter && z < 3 * quarter) edge = 0;
                            else if (z == quarter - 1 && x >= quarter && x < 3 * quarter) edge = 1;
                            else if (z == 3 * quarter && x >= quarter && x < 3 * quarter) edge = 3;
                        }
                        if (edge < 0)
                        {
                            Triangle(bl, tl, br); Triangle(br, tl, tr);
                        }
                        else
                        {
                            int center = Vertex(px + unit / 2, pz + unit / 2);
                            int midpoint = edge == 0 ? Vertex(px, pz + unit / 2)
                                : edge == 1 ? Vertex(px + unit / 2, pz + unit)
                                : edge == 2 ? Vertex(px + unit, pz + unit / 2)
                                : Vertex(px + unit / 2, pz);
                            int[] corners = { bl, tl, tr, br };
                            for (int side = 0; side < 4; side++)
                            {
                                int a = corners[side], b = corners[(side + 1) % 4];
                                if (side == edge)
                                {
                                    Triangle(center, a, midpoint); Triangle(center, midpoint, b);
                                }
                                else Triangle(center, a, b);
                            }
                        }
                    }
            }
            var mesh = new Mesh
            {
                name = "Shuimian Square LOD Water",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.SetVertices(points);
            mesh.subMeshCount = levels;
            for (int level = 0; level < levels; level++) mesh.SetTriangles(ringIndices[level], level);
            var normals = new Vector3[points.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(new Vector3(verticalBounds * 2f, verticalBounds * 2f, verticalBounds * 2f));
            mesh.bounds = bounds;
            return mesh;
        }
    }
}
