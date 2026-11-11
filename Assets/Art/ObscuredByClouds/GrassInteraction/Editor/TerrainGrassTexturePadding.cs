#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ObscuredByClouds.EditorTools
{
    /// <summary>Prepares each grass prefab's own material, alpha padding and bend-height range.</summary>
    public static class TerrainGrassTexturePadding
    {
        const string Root = "Assets/Art/ObscuredByClouds/GrassInteraction/";
        const string Generated = Root + "Prepared/";
        const string ShaderName = "ObscuredByClouds/Terrain Grass Interactive";

        [MenuItem("Tools/ObscuredByClouds/Prepare Terrain Grass Materials")]
        public static void PrepareCurrentScene()
        {
            int count = 0;
            foreach (var terrain in Terrain.activeTerrains.Where(t => t.gameObject.scene == SceneManager.GetActiveScene()))
                count += PrepareTerrain(terrain);
            Debug.Log("Prepared " + count + " Terrain grass prototypes. Original atlas and shared source materials were preserved.");
        }

        // Retain the previous menu entry, now usable with every prepared grass species.
        [MenuItem("Tools/ObscuredByClouds/Rebuild Grass Texture Padding")]
        public static void Rebuild() { PrepareCurrentScene(); }

        [MenuItem("Assets/ObscuredByClouds/Prepare Interactive Grass", false, 2000)]
        public static void PrepareSelected()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Select a grass .prefab in the Project window.");
            PreparePrefab(path);
            foreach (var terrain in Terrain.activeTerrains)
                if (terrain.terrainData.detailPrototypes.Any(p => AssetDatabase.GetAssetPath(p.prototype) == path)) PrepareTerrain(terrain);
        }

        public static int PrepareTerrain(Terrain terrain)
        {
            var prototypes = terrain.terrainData.detailPrototypes;
            int count = 0;
            for (int i = 0; i < prototypes.Length; i++)
            {
                var prefab = prototypes[i].prototype;
                if (!prefab || !prefab.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.sharedMaterials.Any(IsInteractive))) continue;
                if (!PreparePrefab(AssetDatabase.GetAssetPath(prefab))) continue;
                prototypes[i].usePrototypeMesh = true;
                prototypes[i].useInstancing = true;
                prototypes[i].renderMode = DetailRenderMode.VertexLit;
                count++;
            }
            if (count > 0)
            {
                // Rebuild native detail rendering without touching any painted density layer.
                terrain.terrainData.detailPrototypes = prototypes;
                terrain.terrainData.RefreshPrototypes();
                terrain.Flush();
                EditorUtility.SetDirty(terrain.terrainData);
                AssetDatabase.SaveAssets();
            }
            SceneView.RepaintAll();
            return count;
        }

        static bool IsInteractive(Material material) { return material && material.shader && material.shader.name == ShaderName; }

        public static bool PreparePrefab(string path)
        {
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("Create a .prefab from this model first, then prepare it for interaction: " + path);
                return false;
            }
            EnsureFolder(Generated.TrimEnd('/'));
            var contents = PrefabUtility.LoadPrefabContents(path);
            bool prepared = false;
            try
            {
                int slot = 0;
                foreach (var renderer in contents.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!filter || !filter.sharedMesh) continue;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++, slot++)
                    {
                        var source = materials[i];
                        if (!IsInteractive(source)) continue;
                        Material material = source;
                        string sourcePath = AssetDatabase.GetAssetPath(source);
                        // A dedicated material is necessary: different meshes need different bend heights.
                        if (!sourcePath.StartsWith(Root, StringComparison.Ordinal)
                            || Mathf.Abs(source.GetFloat("_OBCGrassMeshBase") - filter.sharedMesh.bounds.min.y) > 0.001f
                            || Mathf.Abs(source.GetFloat("_OBCGrassMeshHeight") - filter.sharedMesh.bounds.size.y) > 0.001f)
                        {
                            string id = AssetDatabase.AssetPathToGUID(path).Substring(0, 8);
                            string targetPath = Generated + Safe(Path.GetFileNameWithoutExtension(path)) + "_" + id + "_" + slot + ".mat";
                            material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                            if (!material)
                            {
                                material = new Material(source) { name = Path.GetFileNameWithoutExtension(targetPath) };
                                material.SetFloat("_GrassSpecularStrength", 0f);
                                material.SetFloat("_Smoothness", 0.18f);
                                material.SetFloat("_SpecularHighlights", 0f);
                                material.SetFloat("_EnvironmentReflections", 0f);
                                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                                AssetDatabase.CreateAsset(material, targetPath);
                            }
                        }
                        var albedo = material.GetTexture("_MainTex") as Texture2D;
                        if (!albedo) throw new InvalidOperationException("Grass material has no _MainTex: " + source.name);
                        var padded = GetPaddedTexture(albedo, material.GetFloat("_Cutoff"));
                        material.SetTexture("_MainTex", padded);
                        material.SetFloat("_OBCGrassMeshBase", filter.sharedMesh.bounds.min.y);
                        material.SetFloat("_OBCGrassMeshHeight", filter.sharedMesh.bounds.size.y);
                        material.enableInstancing = true;
                        EditorUtility.SetDirty(material);
                        materials[i] = material;
                        prepared = true;
                    }
                    renderer.sharedMaterials = materials;
                }
                if (prepared) PrefabUtility.SaveAsPrefabAsset(contents, path);
                AssetDatabase.SaveAssets();
                return prepared;
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        static Texture2D GetPaddedTexture(Texture2D source, float cutoff)
        {
            string path = AssetDatabase.GetAssetPath(source);
            if (path.StartsWith(Generated + "Textures/", StringComparison.Ordinal) || path == Root + "Reeds_Interactive_BC.png") return source;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                throw new InvalidOperationException("Use a PNG/JPG source atlas for automatic padding: " + path);
            cutoff = Mathf.Clamp(cutoff, 0.01f, 0.99f);
            string id = AssetDatabase.AssetPathToGUID(path).Substring(0, 8);
            string destination = Generated + "Textures/" + Safe(Path.GetFileNameWithoutExtension(path)) + "_" + id + "_" + Mathf.RoundToInt(cutoff * 255) + "_Padded.png";
            var cached = AssetDatabase.LoadAssetAtPath<Texture2D>(destination);
            if (cached) return cached;
            EnsureFolder(Generated + "Textures");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read grass atlas: " + path);
                int width = texture.width, height = texture.height;
                var pixels = texture.GetPixels32();
                var visited = new bool[pixels.Length]; var queue = new int[pixels.Length];
                int head = 0, tail = 0, threshold = Mathf.RoundToInt(cutoff * 255);
                for (int i = 0; i < pixels.Length; i++) if (pixels[i].a >= threshold) { visited[i] = true; queue[tail++] = i; }
                if (tail == 0) throw new InvalidOperationException("Grass atlas contains no opaque pixels: " + path);
                while (head < tail)
                {
                    int i = queue[head++], x = i % width, y = i / width;
                    if (x > 0) Extend(i - 1, i, pixels, visited, queue, ref tail);
                    if (x + 1 < width) Extend(i + 1, i, pixels, visited, queue, ref tail);
                    if (y > 0) Extend(i - width, i, pixels, visited, queue, ref tail);
                    if (y + 1 < height) Extend(i + width, i, pixels, visited, queue, ref tail);
                }
                texture.SetPixels32(pixels); texture.Apply(false);
                File.WriteAllBytes(destination, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true; importer.mipMapsPreserveCoverage = true; importer.alphaTestReferenceValue = cutoff;
                importer.mipmapFilter = TextureImporterMipFilter.BoxFilter; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8;
                importer.wrapMode = ((TextureImporter)AssetImporter.GetAtPath(path)).wrapMode;
                importer.maxTextureSize = Mathf.Max(2048, Mathf.Max(width, height));
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.userData = "OBC padded atlas; source=" + AssetDatabase.AssetPathToGUID(path);
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(destination);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        static void Extend(int to, int from, Color32[] pixels, bool[] visited, int[] queue, ref int tail)
        { if (visited[to]) return; var color = pixels[from]; color.a = pixels[to].a; pixels[to] = color; visited[to] = true; queue[tail++] = to; }
        static string Safe(string name) { foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_'); return name; }
        static void EnsureFolder(string path)
        { path = path.TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return; string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    }
}
#endif
