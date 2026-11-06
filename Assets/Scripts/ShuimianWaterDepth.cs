using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shuimian.Gameplay
{
    /// <summary>
    /// Supplies world-space terrain elevations to the independent water cascades.
    /// Terrain heights are cached; rebuilding a moving cascade only reads managed arrays.
    /// </summary>
    public sealed class ShuimianWaterDepth : IDisposable
    {
        sealed class HeightField
        {
            public Terrain terrain;
            public TerrainData data;
            public Vector3 position, size;
            public float[,] heights;
            public int resolution;
            public uint heightVersion;
        }

        readonly List<HeightField> fields = new List<HeightField>();
        readonly List<HeightField> activeFields = new List<HeightField>();
        readonly int terrainResolution;
        Texture2DArray texture;
        Color[] pixels;
        int[] sampleX;
        float[] fractionX;
        Vector2 lastCenter;
        float lastDiameter, lastSeaLevel, nextTerrainRefresh;
        bool dirty = true;

        /// <param name="resolution">Terrain sampling resolution per cascade; independent of wave resolution.</param>
        public ShuimianWaterDepth(int resolution = 128)
        {
            terrainResolution = Mathf.Clamp(Mathf.NextPowerOfTwo(resolution), 16, 256);
            sampleX = new int[terrainResolution];
            fractionX = new float[terrainResolution];
        }

        public Texture2DArray Texture => texture;

        /// <summary>
        /// R is terrain world Y and G is a zero sea-level offset, matching the surface shader's depth layout.
        /// Every layer covers nearDiameter * 2^layer around center, as does the wave field.
        /// </summary>
        public Texture2DArray Update(Vector2 center, float nearDiameter, int cascadeCount, float seaLevel)
        {
            RefreshTerrains();
            int resolution = fields.Count == 0 ? 2 : terrainResolution;
            cascadeCount = Mathf.Max(1, cascadeCount);
            nearDiameter = Mathf.Max(.01f, nearDiameter);
            if (texture == null || texture.width != resolution || texture.depth != cascadeCount)
            {
                ReleaseTexture();
                texture = new Texture2DArray(resolution, resolution, cascadeCount, TextureFormat.RGBAFloat, false, true)
                {
                    name = "Shuimian Terrain Depth", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                pixels = new Color[resolution * resolution];
                dirty = true;
            }
            if (!dirty && lastCenter == center && lastDiameter == nearDiameter && lastSeaLevel == seaLevel)
                return texture;

            float fallback = seaLevel - 1000f;
            for (int layer = 0; layer < cascadeCount; layer++)
            {
                float diameter = nearDiameter * (1 << layer);
                float step = diameter / resolution;
                float startX = center.x - diameter * .5f + step * .5f;
                float startZ = center.y - diameter * .5f + step * .5f;
                Color clear = new Color(fallback, 0f, 0f, 0f);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
                for (int field = 0; field < fields.Count; field++)
                    Rasterize(fields[field], startX, startZ, step, resolution);
                texture.SetPixels(pixels, layer);
            }
            texture.Apply(false, false);
            lastCenter = center;
            lastDiameter = nearDiameter;
            lastSeaLevel = seaLevel;
            dirty = false;
            return texture;
        }

        void Rasterize(HeightField field, float startX, float startZ, float step, int resolution)
        {
            // Clip the whole layer against a terrain before entering the sampling loop.
            int minX = Math.Max(0, Mathf.CeilToInt((field.position.x - startX) / step));
            int maxX = Math.Min(resolution - 1, Mathf.FloorToInt((field.position.x + field.size.x - startX) / step));
            int minZ = Math.Max(0, Mathf.CeilToInt((field.position.z - startZ) / step));
            int maxZ = Math.Min(resolution - 1, Mathf.FloorToInt((field.position.z + field.size.z - startZ) / step));
            if (minX > maxX || minZ > maxZ) return;

            float scaleX = (field.resolution - 1) / field.size.x;
            float scaleZ = (field.resolution - 1) / field.size.z;
            for (int x = minX; x <= maxX; x++)
            {
                float px = Mathf.Clamp((startX + x * step - field.position.x) * scaleX, 0f, field.resolution - 1);
                sampleX[x] = (int)px;
                fractionX[x] = px - sampleX[x];
            }
            for (int z = minZ; z <= maxZ; z++)
            {
                float pz = Mathf.Clamp((startZ + z * step - field.position.z) * scaleZ, 0f, field.resolution - 1);
                int z0 = (int)pz, z1 = Math.Min(z0 + 1, field.resolution - 1);
                float tz = pz - z0;
                int row = z * resolution;
                for (int x = minX; x <= maxX; x++)
                {
                    int x0 = sampleX[x], x1 = Math.Min(x0 + 1, field.resolution - 1);
                    float a = Mathf.Lerp(field.heights[z0, x0], field.heights[z0, x1], fractionX[x]);
                    float b = Mathf.Lerp(field.heights[z1, x0], field.heights[z1, x1], fractionX[x]);
                    float height = field.position.y + Mathf.Lerp(a, b, tz) * field.size.y;
                    if (height > pixels[row + x].r) pixels[row + x].r = height;
                }
            }
        }

        void RefreshTerrains()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextTerrainRefresh) return;
            nextTerrainRefresh = now + 1f;
            activeFields.Clear();
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain == null || !terrain.isActiveAndEnabled) continue;
                TerrainData data = terrain.terrainData;
                if (data == null || data.heightmapResolution < 2 || data.size.x <= 0f || data.size.z <= 0f) continue;
                HeightField field = fields.Find(candidate => candidate.terrain == terrain);
                uint heightVersion = data.heightmapTexture.updateCount;
                Vector3 position = terrain.GetPosition(), size = data.size;
                if (field == null)
                {
                    field = new HeightField { terrain = terrain };
                    dirty = true;
                }
                if (field.position != position || field.size != size) dirty = true;
                field.position = position;
                field.size = size;
                if (field.heights == null || field.data != data || field.resolution != data.heightmapResolution
                    || field.heightVersion != heightVersion)
                {
                    field.data = data;
                    field.resolution = data.heightmapResolution;
                    field.heights = data.GetHeights(0, 0, field.resolution, field.resolution);
                    field.heightVersion = heightVersion;
                    dirty = true;
                }
                activeFields.Add(field);
            }
            if (activeFields.Count != fields.Count) dirty = true;
            fields.Clear();
            fields.AddRange(activeFields);
        }

        void ReleaseTexture()
        {
            if (texture != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
                texture = null;
            }
        }

        public void Dispose()
        {
            ReleaseTexture();
            fields.Clear();
            activeFields.Clear();
            pixels = null;
            sampleX = null;
            fractionX = null;
        }
    }
}
