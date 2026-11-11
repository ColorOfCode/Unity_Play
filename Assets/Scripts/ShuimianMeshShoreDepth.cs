using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Shuimian.Gameplay
{
    /// <summary>Cached world-space mesh heights, merged with the existing Terrain depth cascades.</summary>
    public sealed class ShuimianMeshShoreDepth : IDisposable
    {
        readonly Scene scene;
        readonly List<MeshRenderer> sources = new List<MeshRenderer>();
        Material material;
        RenderTexture atlas, merged;
        Vector4 rect;
        float nextScan, cachedSea = float.NaN, cachedBand, cachedDiameter;
        float mergedShoreDepth;
        int cachedMask, cachedResolution, geometryHash, revision, mergedRevision;
        Texture2DArray previousTerrain;
        uint previousTerrainVersion;
        Vector2 cachedCenter;
        bool staticOnly;
        public int SourceCount => sources.Count;
        public Texture GeometryAtlas => atlas;
        public Vector4 WorldRect => rect;

        public ShuimianMeshShoreDepth(Scene ownerScene) { scene = ownerScene; }

        public Texture Update(Texture2DArray terrain, Vector2 center, float nearDiameter,
            int cascades, float seaLevel, float shoreDepth, int layers, int resolution, bool onlyStatic)
        {
            resolution = Mathf.Clamp(Mathf.NextPowerOfTwo(resolution), 256, 2048);
            float band = Mathf.Max(0.5f, shoreDepth);
            bool settingsChanged = cachedSea != seaLevel || cachedBand != band || cachedMask != layers
                || cachedResolution != resolution || staticOnly != onlyStatic;
            if (settingsChanged || Time.realtimeSinceStartup >= nextScan)
            {
                RefreshSources(seaLevel, band, layers, resolution, onlyStatic, settingsChanged);
                nextScan = Time.realtimeSinceStartup + 1f;
            }
            if (sources.Count == 0 || !atlas) return terrain;
            if (!material) return terrain;
            int mergeSize = Mathf.Max(512, terrain.width);
            bool recreate = !merged || merged.width != mergeSize || merged.volumeDepth != cascades;
            if (recreate)
            {
                Release(ref merged);
                merged = new RenderTexture(mergeSize, mergeSize, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                {
                    name = "Shuimian Terrain + Mesh Depth", hideFlags = HideFlags.HideAndDontSave,
                    dimension = TextureDimension.Tex2DArray, volumeDepth = cascades,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                merged.Create();
            }
            if (!recreate && previousTerrain == terrain && previousTerrainVersion == terrain.updateCount
                && mergedRevision == revision && cachedCenter == center && cachedDiameter == nearDiameter && mergedShoreDepth == shoreDepth) return merged;
            var commands = new CommandBuffer { name = "Shuimian merge cached mesh shoreline depth" };
            try
            {
                for (int layer = 0; layer < cascades; layer++)
                {
                    var properties = new MaterialPropertyBlock();
                    properties.SetTexture("_ShuimianTerrainDepth", terrain);
                    properties.SetTexture("_ShuimianMeshDepth", atlas);
                    properties.SetVector("_ShuimianDepthRect", rect);
                    properties.SetVector("_ShuimianDepthCascade", new Vector4(center.x, center.y, nearDiameter * (1 << layer), layer));
                    properties.SetVector("_ShuimianDepthSettings", new Vector4(seaLevel - 1000f, seaLevel, shoreDepth, 1f / atlas.width));
                    commands.SetRenderTarget(merged, 0, CubemapFace.Unknown, layer);
                    commands.SetViewport(new Rect(0, 0, merged.width, merged.height));
                    commands.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3, 1, properties);
                }
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally { commands.Release(); }
            previousTerrain = terrain; previousTerrainVersion = terrain.updateCount;
            mergedRevision = revision; cachedCenter = center; cachedDiameter = nearDiameter; mergedShoreDepth = shoreDepth;
            return merged;
        }

        void RefreshSources(float seaLevel, float band, int layers, int resolution, bool onlyStatic, bool force)
        {
            sources.Clear();
            int hash = 17;
            Bounds bounds = default;
            foreach (var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.gameObject.scene != scene
                    || (onlyStatic && !renderer.gameObject.isStatic) || (layers & (1 << renderer.gameObject.layer)) == 0) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || renderer.bounds.min.y > seaLevel + 0.5f || renderer.bounds.max.y < seaLevel - band * 2f) continue;
                bool opaque = false;
                foreach (var mat in renderer.sharedMaterials)
                    if (mat && mat.renderQueue <= 2500 && mat.GetTag("RenderType", false, "Opaque") == "Opaque") { opaque = true; break; }
                if (!opaque) continue;
                if (sources.Count == 0) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                sources.Add(renderer);
                unchecked { hash = hash * 31 + renderer.GetInstanceID(); hash = hash * 31 + renderer.localToWorldMatrix.GetHashCode(); hash = hash * 31 + filter.sharedMesh.GetInstanceID(); }
            }
            cachedSea = seaLevel; cachedBand = band; cachedMask = layers; cachedResolution = resolution; staticOnly = onlyStatic;
            if (sources.Count == 0) { if (geometryHash != hash) revision++; geometryHash = hash; return; }
            if (!force && geometryHash == hash && atlas) return;
            if (!material)
            {
                var shader = Resources.Load<Shader>("Shuimian/ShoreGeometryDepth");
                if (!shader || !shader.isSupported) return;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            float size = Mathf.Max(8f, Mathf.Max(bounds.size.x, bounds.size.z) + 8f);
            rect = new Vector4(bounds.center.x - size * .5f, bounds.center.z - size * .5f, size, size);
            if (!atlas || atlas.width != resolution)
            {
                Release(ref atlas);
                atlas = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                {
                    name = "Shuimian static shoreline geometry heights", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                atlas.Create();
            }
            material.SetVector("_ShuimianDepthRect", rect);
            material.SetFloat("_ShuimianHeightBase", seaLevel - 1000f);
            var commands = new CommandBuffer { name = "Shuimian cache static shoreline mesh geometry" };
            try
            {
                commands.SetRenderTarget(atlas); commands.SetViewport(new Rect(0, 0, resolution, resolution));
                commands.ClearRenderTarget(false, true, Color.clear);
                foreach (var renderer in sources)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    var materials = renderer.sharedMaterials;
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        var sourceMaterial = materials[Mathf.Min(submesh, materials.Length - 1)];
                        if (sourceMaterial && sourceMaterial.renderQueue <= 2500 && sourceMaterial.GetTag("RenderType", false, "Opaque") == "Opaque")
                            commands.DrawRenderer(renderer, material, submesh, 0);
                    }
                }
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally { commands.Release(); }
            geometryHash = hash; revision++;
        }

        static void Release(ref RenderTexture texture)
        {
            if (!texture) return;
            texture.Release();
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture); else UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }
        public void Dispose()
        {
            Release(ref atlas); Release(ref merged);
            if (material) { if (Application.isPlaying) UnityEngine.Object.Destroy(material); else UnityEngine.Object.DestroyImmediate(material); }
            material = null; sources.Clear();
        }
    }
}
