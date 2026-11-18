using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMeshSimplifier;
using Autodesk.Fbx;
using Object = UnityEngine.Object;

namespace ProjectTools.ModelReduction
{
    public static class ModelReductionCore
    {
        public static long Triangles(Mesh mesh)
        {
            long count = 0;
            if (mesh != null)
                for (int s = 0; s < mesh.subMeshCount; s++)
                    if (mesh.GetTopology(s) == MeshTopology.Triangles) count += (long)mesh.GetIndexCount(s) / 3;
            return count;
        }

        public static Mesh[] Meshes(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Where(m => m.vertexCount > 0).ToArray();

        public static Mesh Simplify(Mesh original, float percent, bool preserveBorders)
        {
            if (percent <= 0 || percent > 100 || float.IsNaN(percent)) throw new ArgumentOutOfRangeException(nameof(percent));
            for (int s = 0; s < original.subMeshCount; s++)
                if (original.GetTopology(s) != MeshTopology.Triangles) throw new NotSupportedException("包含非三角形拓扑：" + original.name);
            if (percent >= 100) return Object.Instantiate(original);
            // GetBonesPerVertex returns a read-only view owned by Mesh; do not dispose it.
            var counts = original.GetBonesPerVertex();
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > 4) throw new NotSupportedException("单顶点超过 4 个骨骼权重，已跳过，避免丢失蒙皮数据。" );
            var simplifier = new MeshSimplifier();
            var options = SimplificationOptions.Default;
            options.PreserveBorderEdges = preserveBorders;
            options.PreserveUVSeamEdges = preserveBorders;
            options.PreserveUVFoldoverEdges = preserveBorders;
            simplifier.SimplificationOptions = options;
            simplifier.Initialize(original);
            simplifier.SimplifyMesh(percent / 100f);
            var result = simplifier.ToMesh();
            result.name = original.name;
            result.bounds = original.bounds; // Retain animated/culling bounds authored for this mesh.
            if (Triangles(result) == 0 || Triangles(result) > Triangles(original))
            { Object.DestroyImmediate(result); throw new InvalidOperationException("减面结果为空或面数异常。"); }
            return result;
        }

        public static string Replace(string path, float percent, bool preserveBorders)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !File.Exists(path)) throw new ArgumentException("仅支持 Assets 内的模型资产。");
            if (float.IsNaN(percent) || percent <= 0 || percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));
            if (percent >= 100) return Path.GetFileName(path) + "：100%，未修改";
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".fbx" && ext != ".obj" && ext != ".asset") throw new NotSupportedException("直接覆盖支持 FBX、OBJ 和独立 Mesh .asset；其他格式请先导出为 FBX。");
            byte[] originalFile = File.ReadAllBytes(path);
            byte[] originalMeta = File.ReadAllBytes(path + ".meta");
            string guid = AssetDatabase.AssetPathToGUID(path);
            string tempDirectory = Path.Combine("Library/ModelReductionTemp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var temporaryObjects = new List<Object>();
            bool success = false;
            long before = 0, after = 0;
            try
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null && !importer.isReadable)
                { importer.isReadable = true; importer.SaveAndReimport(); }
                Mesh[] source = Meshes(path);
                if (source.Length == 0) throw new InvalidOperationException("找不到网格。");
                before = source.Sum(Triangles);
                var identities = source.Select(m => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string g, out long id); return id; }).OrderBy(id => id).ToArray();
                if (ext == ".asset")
                {
                    if (source.Length != 1 || AssetDatabase.LoadMainAssetAtPath(path) != source[0])
                        throw new NotSupportedException("仅支持以 Mesh 为主资产的独立 .asset。");
                    var reduced = Simplify(source[0], percent, preserveBorders); temporaryObjects.Add(reduced);
                    EditorUtility.CopySerialized(reduced, source[0]);
                    EditorUtility.SetDirty(source[0]); AssetDatabase.SaveAssets();
                }
                else
                {
                    if (AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture>().Any())
                        throw new NotSupportedException("模型包含内嵌贴图，请先提取贴图后再直接覆盖，避免丢失贴图引用。");
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance = Object.Instantiate(asset); temporaryObjects.Add(instance);
                    instance.name = asset.name; instance.hideFlags = HideFlags.HideAndDontSave;
                    var snapshot = Capture(instance);
                    var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                        .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
                    var clipInfo = clips.Select(c => new KeyValuePair<string, float>(c.name, c.length)).ToArray();
                    if (clips.Any(c => c.isHumanMotion)) throw new NotSupportedException("包含 Humanoid 动画曲线，无法无损写回，已跳过。");
                    var reduced = new Dictionary<Mesh, Mesh>();
                    foreach (var m in source) { var r = Simplify(m, percent, preserveBorders); reduced[m] = r; temporaryObjects.Add(r); }
                    foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh != null) filter.sharedMesh = reduced[filter.sharedMesh];
                    foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (skin.sharedMesh != null) skin.sharedMesh = reduced[skin.sharedMesh];
                    string exported = Path.GetFullPath(Path.Combine(tempDirectory, Path.GetFileName(path)));
                    if (ext == ".fbx")
                    {
                        // Importers can replace 18 distinct FBX materials with the same default Lit.
                        // Restore the source slot names before export so the exporter cannot merge them.
                        var slotNames = ReadFbxMaterialSlots(path, out var sourceRoots);
                        var unityRoots = Enumerable.Range(0, instance.transform.childCount)
                            .Select(i => instance.transform.GetChild(i).name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                        // Multi-root FBX files receive a synthetic filename root in Unity. Export its
                        // children, not that synthetic root: otherwise a same-named mesh gets "_1".
                        bool exportChildren = instance.GetComponent<Renderer>() == null && sourceRoots.Length > 1
                            && sourceRoots.OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(unityRoots);
                        // Unity also replaces a single FBX wrapper's name with the filename.
                        // Restore that source wrapper name instead of exporting the filename.
                        if (sourceRoots.Length == 1 && instance.GetComponent<Renderer>() == null)
                            instance.name = sourceRoots[0];
                        Debug.Log("[模型减面] 源 FBX 根节点：" + string.Join(",", sourceRoots)
                            + "；导出根：" + instance.name + "；导出子节点：" + exportChildren);
                        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                        {
                            if (!slotNames.TryGetValue(renderer.name, out var names)) continue;
                            var slots = renderer.sharedMaterials;
                            if (names.Length == 0 && slots.Length == 1)
                                names = new[] {slots[0] != null ? slots[0].name : "DefaultMaterial"};
                            if (names.Length != slots.Length) throw new InvalidOperationException("源 FBX 材质槽与 Unity 不一致：" + renderer.name);
                            for (int i = 0; i < slots.Length; i++)
                            {
                                if (slots[i] == null) continue;
                                var copy = new Material(slots[i]) {name = names[i]};
                                temporaryObjects.Add(copy); slots[i] = copy;
                            }
                            renderer.sharedMaterials = slots;
                        }
                        // An in-memory legacy component exposes every embedded generic/legacy clip to the exporter.
                        if (clips.Length > 0)
                        {
                            foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
                            foreach (var oldAnimation in instance.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(oldAnimation);
                            var animation = instance.AddComponent<Animation>();
                            foreach (var clip in clips)
                            {
                                var copy = Object.Instantiate(clip); temporaryObjects.Add(copy); copy.name = clip.name; copy.legacy = true;
                                animation.AddClip(copy, clip.name);
                                if (animation.clip == null) animation.clip = copy;
                            }
                        }
                        ExportFbx(exported, instance, exportChildren);
                    }
                    else
                    {
                        if (clips.Length > 0 || snapshot.Any(s => s.Value.skinned)) throw new NotSupportedException("OBJ 不支持骨骼或动画。");
                        ExportObj(exported, instance, path);
                    }
                    File.WriteAllBytes(path, File.ReadAllBytes(exported));
                    File.WriteAllBytes(path + ".meta", originalMeta);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    Verify(snapshot, AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    var newClips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
                    foreach (var info in clipInfo)
                        if (!newClips.Any(c => c.name == info.Key && Mathf.Abs(c.length - info.Value) < .05f))
                            throw new InvalidOperationException("写回后动画片段缺失或时长改变：" + info.Key);
                }
                File.WriteAllBytes(path + ".meta", originalMeta);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var current = Meshes(path); after = current.Sum(Triangles);
                var newIdentities = current.Select(m => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string g, out long id); return id; }).OrderBy(id => id).ToArray();
                if (AssetDatabase.AssetPathToGUID(path) != guid || !identities.SequenceEqual(newIdentities))
                    throw new InvalidOperationException("写回改变了网格引用标识，已取消覆盖。");
                if (after <= 0 || after > before) throw new InvalidOperationException("写回后面数异常。");
                success = true;
                return Path.GetFileName(path) + "：" + before.ToString("N0") + " → " + after.ToString("N0")
                    + " 三角面（实际保留 " + (100.0 * after / before).ToString("F1") + "%）";
            }
            finally
            {
                foreach (var item in temporaryObjects) if (item != null) Object.DestroyImmediate(item);
                if (!success)
                {
                    File.WriteAllBytes(path, originalFile); File.WriteAllBytes(path + ".meta", originalMeta);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
                // Original bytes exist only for this transaction, never as a retained model backup.
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        class Shape
        {
            public bool skinned;
            public int submeshes;
            public string[] bones, blendshapes, materials;
            public Bounds bounds;
        }
        static Dictionary<string, Shape> Capture(GameObject root)
        {
            var result = new Dictionary<string, Shape>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var skin = renderer as SkinnedMeshRenderer;
                var filter = renderer.GetComponent<MeshFilter>();
                Mesh m = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
                if (m == null) continue;
                string key = AnimationUtility.CalculateTransformPath(renderer.transform, root.transform);
                result[key] = new Shape {
                    skinned = skin != null, submeshes = m.subMeshCount, bounds = ModelBounds(root.transform, renderer.transform, m.bounds),
                    bones = skin != null ? skin.bones.Select(b => b != null ? b.name : "").ToArray() : new string[0],
                    blendshapes = Enumerable.Range(0, m.blendShapeCount).Select(m.GetBlendShapeName).ToArray(),
                    materials = renderer.sharedMaterials.Select(t => t != null ? t.name : "").ToArray()
                };
            }
            return result;
        }
        static Bounds ModelBounds(Transform root, Transform node, Bounds bounds)
        {
            var matrix = root.worldToLocalMatrix * node.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, sign)));
            }
            return result;
        }
        static void Verify(Dictionary<string, Shape> before, GameObject after)
        {
            var current = Capture(after);
            if (!before.Keys.OrderBy(k => k).SequenceEqual(current.Keys.OrderBy(k => k)))
                throw new InvalidOperationException("写回改变了渲染节点层级，已取消覆盖。\n原节点：" + string.Join("; ", before.Keys)
                    + "\n新节点：" + string.Join("; ", current.Keys));
            foreach (var pair in before)
            {
                var a = pair.Value; var b = current[pair.Key];
                if (a.skinned != b.skinned || a.submeshes != b.submeshes || !a.bones.SequenceEqual(b.bones)
                    || !a.blendshapes.SequenceEqual(b.blendshapes) || !a.materials.SequenceEqual(b.materials))
                    throw new InvalidOperationException("写回改变了骨骼、BlendShape 或材质槽：" + pair.Key
                        + "\nsubmeshes " + a.submeshes + " → " + b.submeshes
                        + "\nbones " + string.Join(",", a.bones) + " → " + string.Join(",", b.bones)
                        + "\nshapes " + string.Join(",", a.blendshapes) + " → " + string.Join(",", b.blendshapes)
                        + "\nmaterials " + string.Join(",", a.materials) + " → " + string.Join(",", b.materials));
                float tolerance = Mathf.Max(.01f, a.bounds.size.magnitude * .1f);
                if (Vector3.Distance(a.bounds.center, b.bounds.center) > tolerance || Vector3.Distance(a.bounds.size, b.bounds.size) > tolerance)
                    throw new InvalidOperationException("写回后的模型尺寸/位置异常：" + pair.Key);
            }
        }

        public static void ExportFbx(string path, GameObject root, bool exportChildren = false)
        {
            // 4.1.3 exposes configurable export options internally. Keep reflection isolated here.
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter")).FirstOrDefault(t => t != null);
            if (type == null) throw new NotSupportedException("需要 Unity FBX Exporter 4.1.3。");
            var optionType = type.Assembly.GetType("UnityEditor.Formats.Fbx.Exporter.ExportModelSettingsSerialize");
            object options = Activator.CreateInstance(optionType, true);
            SetOption(options, "SetExportFormat", 1);
            SetOption(options, "SetModelAnimIncludeOption", 2);
            SetOption(options, "SetObjectPosition", 1);
            SetOption(options, "SetAnimatedSkinnedMesh", true);
            SetOption(options, "SetUseMayaCompatibleNames", false);
            SetOption(options, "SetPreserveImportSettings", true);
            string result;
            string originalName = root.name;
            bool renamedRoot = !exportChildren && root.GetComponentsInChildren<Transform>(true)
                .Any(t => t != root.transform && t.name == originalName);
            if (renamedRoot)
            {
                string unique = "__ModelReductionExportRoot_" + Guid.NewGuid().ToString("N");
                root.name = unique;
            }
            string exportedRootName = root.name;
            try
            {
            if (exportChildren)
            {
                var roots = Enumerable.Range(0, root.transform.childCount).Select(i => (Object)root.transform.GetChild(i).gameObject).ToArray();
                var method = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .First(m => m.Name == "ExportObjects" && m.GetParameters().Length == 4);
                result = (string)method.Invoke(null, new object[] {path, roots, options, null});
            }
            else
            {
                var method = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .First(m => m.Name == "ExportObject" && m.GetParameters().Length == 3);
                result = (string)method.Invoke(null, new[] {(object)path, root, options});
            }
            }
            finally { root.name = originalName; }
            if (string.IsNullOrEmpty(result)) throw new IOException("FBX 导出失败。");
            if (renamedRoot) RestoreFbxRootName(path, exportedRootName, originalName);
        }
        static void RestoreFbxRootName(string path, string exportedName, string sourceName)
        {
            // Unity's exporter forces globally unique node names, whereas FBX allows a parent
            // and child to share a name. Rename only the wrapper after exporting the geometry.
            using (var manager = FbxManager.Create())
            {
                var settings = FbxIOSettings.Create(manager, Globals.IOSROOT); manager.SetIOSettings(settings);
                var scene = FbxScene.Create(manager, "RestoreSourceRoot");
                using (var reader = FbxImporter.Create(manager, "RootReader"))
                    if (!reader.Initialize(path, -1, settings) || !reader.Import(scene)) throw new IOException("无法恢复 FBX 根节点名称。");
                var root = scene.GetRootNode();
                if (root.GetChildCount() != 1 || root.GetChild(0).GetName() != exportedName)
                    throw new InvalidOperationException("FBX 导出根节点不符合预期。");
                root.GetChild(0).SetName(sourceName);
                using (var writer = FbxExporter.Create(manager, "RootWriter"))
                {
                    int format = manager.GetIOPluginRegistry().FindWriterIDByDescription("FBX binary (*.fbx)");
                    if (!writer.Initialize(path, format, settings) || !writer.Export(scene)) throw new IOException("FBX 根节点名称恢复失败。");
                }
            }
        }
        static Dictionary<string, string[]> ReadFbxMaterialSlots(string path, out string[] rootNames)
        {
            var result = new Dictionary<string, string[]>();
            using (var manager = FbxManager.Create())
            {
                var settings = FbxIOSettings.Create(manager, Globals.IOSROOT); manager.SetIOSettings(settings);
                var scene = FbxScene.Create(manager, "SourceSlots");
                using (var importer = FbxImporter.Create(manager, "SlotReader"))
                {
                    if (!importer.Initialize(Path.GetFullPath(path), -1, settings) || !importer.Import(scene))
                        throw new IOException("无法读取源 FBX 材质槽。");
                }
                Action<FbxNode> visit = null;
                visit = node => {
                    if (node.GetMesh() != null)
                    {
                        var materialNames = new List<string>();
                        // This FBX SDK binding exposes GetMaterial but not GetMaterialCount.
                        for (int i = 0; i < node.GetSrcObjectCount(); i++)
                        {
                            var material = node.GetMaterial(i);
                            if (material == null) break;
                            materialNames.Add(material.GetName());
                        }
                        var names = materialNames.ToArray();
                        if (result.TryGetValue(node.GetName(), out var existing) && !existing.SequenceEqual(names))
                            throw new NotSupportedException("FBX 内存在同名但材质槽不同的网格节点，请先为节点命名。");
                        result[node.GetName()] = names;
                    }
                    for (int i = 0; i < node.GetChildCount(); i++) visit(node.GetChild(i));
                };
                var sourceRoot = scene.GetRootNode();
                rootNames = Enumerable.Range(0, sourceRoot.GetChildCount()).Select(i => sourceRoot.GetChild(i).GetName()).ToArray();
                visit(sourceRoot);
            }
            return result;
        }
        static void SetOption(object options, string name, object value)
        {
            var method = options.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            var parameter = method.GetParameters()[0].ParameterType;
            method.Invoke(options, new[] {parameter.IsEnum ? Enum.ToObject(parameter, value) : value});
        }
        public static void ExportObj(string path, GameObject root, string sourcePath)
        {
            var text = new StringBuilder("# Model Reduction Tool\n");
            foreach (string line in File.ReadLines(sourcePath)) if (line.StartsWith("mtllib ", StringComparison.Ordinal)) text.AppendLine(line);
            int offset = 1;
            Func<float, string> f = v => v.ToString("R", CultureInfo.InvariantCulture);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh; if (mesh == null) continue;
                text.AppendLine("g " + filter.name);
                var matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices) { var p = matrix.MultiplyPoint3x4(v); text.AppendLine("v " + f(-p.x) + " " + f(p.y) + " " + f(p.z)); }
                var uv = mesh.uv; var normals = mesh.normals;
                for (int i = 0; i < mesh.vertexCount; i++) { var u = uv.Length > i ? uv[i] : Vector2.zero; text.AppendLine("vt " + f(u.x) + " " + f(u.y)); }
                for (int i = 0; i < mesh.vertexCount; i++) { var n = normals.Length > i ? matrix.MultiplyVector(normals[i]).normalized : Vector3.up; text.AppendLine("vn " + f(-n.x) + " " + f(n.y) + " " + f(n.z)); }
                var renderer = filter.GetComponent<MeshRenderer>();
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (renderer != null && s < renderer.sharedMaterials.Length && renderer.sharedMaterials[s] != null) text.AppendLine("usemtl " + renderer.sharedMaterials[s].name);
                    int[] indices = mesh.GetTriangles(s);
                    for (int t = 0; t < indices.Length; t += 3)
                    {
                        text.Append("f");
                        foreach (int j in new[] {0, 2, 1}) { int k = offset + indices[t + j]; text.Append(" " + k + "/" + k + "/" + k); }
                        text.AppendLine();
                    }
                }
                offset += mesh.vertexCount;
            }
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }
    }
}
