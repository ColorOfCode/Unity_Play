using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectTools.ModelReduction
{
    public sealed class ModelReductionTool : EditorWindow
    {
        public class Item
        {
            public string name, path, kind;
            public long triangles, bytes;
            public double MB => bytes / (1024.0 * 1024.0);
        }
        readonly List<Item> items = new List<Item>(), filtered = new List<Item>();
        readonly HashSet<string> selected = new HashSet<string>();
        string search = "", folder = "", message = "";
        float minMB, maxMB = 10000, percent = 100;
        int sort, type;
        bool preserveBorders, busy;
        Vector2 scroll;
        static readonly string[] Sorts = {"三角面数：高 → 低", "大小 MB：高 → 低"};
        static readonly string[] Types = {"全部类型", "静态网格体", "骨骼网格体", "混合网格体"};

        [MenuItem("工具/模型减面工具")]
        public static void OpenWindow()
        {
            var window = GetWindow<ModelReductionTool>();
            window.titleContent = new GUIContent("模型减面工具");
            window.minSize = new Vector2(840, 420);
            window.Show();
        }
        void OnEnable() { EditorApplication.delayCall += Refresh; }
        void OnDisable() { EditorApplication.delayCall -= Refresh; }

        public static List<Item> Scan()
        {
            var paths = new HashSet<string>(AssetDatabase.FindAssets("t:Model", new[] {"Assets"}).Select(AssetDatabase.GUIDToAssetPath));
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] {"Assets"}))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetExtension(path).Equals(".asset", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
            }
            var output = new List<Item>();
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                var meshes = ModelReductionCore.Meshes(path);
                if (meshes.Length == 0) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                bool skin = model != null ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s => s.sharedMesh != null)
                    : meshes.Any(m => m.bindposes.Length > 0);
                bool rigid = model != null ? model.GetComponentsInChildren<MeshFilter>(true).Any(f => f.sharedMesh != null) : !skin;
                output.Add(new Item {
                    name = Path.GetFileNameWithoutExtension(path), path = path,
                    triangles = meshes.Sum(ModelReductionCore.Triangles), bytes = new FileInfo(path).Length,
                    kind = skin && rigid ? "混合网格体" : skin ? "骨骼网格体" : "静态网格体"
                });
            }
            return output.OrderByDescending(i => i.triangles).ThenByDescending(i => i.bytes).ThenBy(i => i.path, StringComparer.Ordinal).ToList();
        }
        void Refresh()
        {
            if (this == null || busy) return;
            try
            {
                items.Clear(); items.AddRange(Scan());
                selected.RemoveWhere(p => !items.Any(i => i.path == p));
                Filter();
            }
            catch (Exception e) { message = "扫描失败：" + e.Message; Debug.LogException(e); }
            Repaint();
        }
        void Filter()
        {
            filtered.Clear();
            filtered.AddRange(items.Where(i => (search.Length == 0 || i.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                && (folder.Length == 0 || i.path.IndexOf(folder.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) >= 0)
                && i.MB >= Math.Max(0, minMB) && i.MB <= Math.Max(minMB, maxMB) && (type == 0 || i.kind == Types[type])));
            filtered.Sort((a, b) => { int compare = sort == 0 ? b.triangles.CompareTo(a.triangles) : b.bytes.CompareTo(a.bytes); return compare != 0 ? compare : string.CompareOrdinal(a.path, b.path); });
            scroll.y = Mathf.Clamp(scroll.y, 0, Mathf.Max(0, filtered.Count * 24 - 100));
        }
        public int VisibleCount => filtered.Count;
        public long FirstTriangleCount => filtered.Count == 0 ? 0 : filtered[0].triangles;
        public void SetFolderForValidation(string value) { folder = value; Refresh(); }

        void OnGUI()
        {
            using (new EditorGUI.DisabledScope(busy || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label("搜索名称", GUILayout.Width(55)); search = GUILayout.TextField(search, GUILayout.MinWidth(90));
                    GUILayout.Label("文件夹", GUILayout.Width(42)); folder = GUILayout.TextField(folder, GUILayout.MinWidth(100));
                    if (GUILayout.Button("选中文件夹", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    {
                        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                        if (!string.IsNullOrEmpty(path)) { folder = AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path).Replace('\\', '/'); Filter(); }
                    }
                    if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(45))) Refresh();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("大小 MB", GUILayout.Width(55)); minMB = EditorGUILayout.FloatField(minMB, GUILayout.Width(60));
                    GUILayout.Label("～", GUILayout.Width(15)); maxMB = EditorGUILayout.FloatField(maxMB, GUILayout.Width(70));
                    type = EditorGUILayout.Popup(type, Types, GUILayout.Width(120));
                    sort = EditorGUILayout.Popup(sort, Sorts, GUILayout.Width(180));
                    GUILayout.FlexibleSpace(); GUILayout.Label("共 " + filtered.Count + " 个模型", EditorStyles.miniLabel);
                }
                if (EditorGUI.EndChangeCheck()) Filter();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    bool all = filtered.Count > 0 && filtered.All(i => selected.Contains(i.path));
                    bool nextAll = GUILayout.Toggle(all, "全选当前列表", GUILayout.Width(110));
                    if (all != nextAll) foreach (var i in filtered) { if (nextAll) selected.Add(i.path); else selected.Remove(i.path); }
                    if (GUILayout.Button("清空", GUILayout.Width(45))) selected.Clear();
                    int count = filtered.Count(i => selected.Contains(i.path));
                    GUILayout.Label("已勾选 " + count + " 个", GUILayout.Width(95));
                    GUILayout.Label("保留比例", GUILayout.Width(55)); percent = EditorGUILayout.FloatField(percent, GUILayout.Width(60));
                    GUILayout.Label("%", GUILayout.Width(15));
                    preserveBorders = GUILayout.Toggle(preserveBorders, new GUIContent("保护边界", "保留开放边界及 UV 接缝；复杂边界可能使实际面数高于目标。"), GUILayout.Width(90));
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(count == 0 || percent <= 0 || percent > 100 || float.IsNaN(percent)))
                        if (GUILayout.Button("应用修改", GUILayout.Width(90))) Apply();
                }
            }
            EditorGUILayout.HelpBox("直接覆盖源模型，不保留原版本；50% 表示保留约一半三角面，100% 不改变。连续应用会继续减面。大小为源文件大小（不含贴图）；同一模型在所有场景/Prefab 中同步更新。", MessageType.Info);
            Rect header = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            GUI.Box(header, GUIContent.none, EditorStyles.toolbar);
            DrawCells(header, null, true);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Rect content = GUILayoutUtility.GetRect(0, filtered.Count * 24, GUILayout.ExpandWidth(true));
            int first = Mathf.Max(0, (int)(scroll.y / 24) - 1);
            int last = Mathf.Min(filtered.Count, first + Mathf.CeilToInt(position.height / 24) + 2);
            for (int index = first; index < last; index++)
            {
                Rect row = new Rect(content.x, content.y + index * 24, content.width, 24);
                if ((index & 1) == 1) EditorGUI.DrawRect(row, new Color(0, 0, 0, .07f));
                DrawCells(row, filtered[index], false);
            }
            EditorGUILayout.EndScrollView();
            if (filtered.Count == 0) GUILayout.Label("没有符合筛选条件的模型。", EditorStyles.centeredGreyMiniLabel);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.None);
            GUILayout.Label("直接覆盖支持 FBX、OBJ、独立 Mesh .asset；三角面数按文件内不重复网格汇总。", EditorStyles.miniLabel);
        }
        void DrawCells(Rect row, Item item, bool header)
        {
            float x = row.x + 4;
            var check = new Rect(x, row.y + 3, 22, 20); x += 24;
            float nameWidth = Mathf.Max(230, row.width - 505);
            var name = new Rect(x, row.y + 3, nameWidth, 20); x += nameWidth;
            var triangles = new Rect(x, row.y + 3, 120, 20); x += 120;
            var size = new Rect(x, row.y + 3, 100, 20); x += 100;
            var kind = new Rect(x, row.y + 3, 110, 20); x += 110;
            var estimate = new Rect(x, row.y + 3, 125, 20);
            if (header)
            {
                GUI.Label(name, "模型名称"); GUI.Label(triangles, "三角面数" + (sort == 0 ? " ▼" : ""));
                GUI.Label(size, "大小（MB）" + (sort == 1 ? " ▼" : "")); GUI.Label(kind, "网格类型"); GUI.Label(estimate, "预计保留三角面");
                return;
            }
            bool on = selected.Contains(item.path);
            bool next = GUI.Toggle(check, on, GUIContent.none);
            if (next != on) { if (next) selected.Add(item.path); else selected.Remove(item.path); }
            if (GUI.Button(name, new GUIContent(item.name, item.path), EditorStyles.linkLabel))
            { var asset = AssetDatabase.LoadMainAssetAtPath(item.path); EditorGUIUtility.PingObject(asset); Selection.activeObject = asset; }
            GUI.Label(triangles, item.triangles.ToString("N0"));
            GUI.Label(size, new GUIContent(item.MB < .01 ? item.MB.ToString("F4") : item.MB.ToString("F2"), "源文件大小，不包含外部贴图"));
            GUI.Label(kind, item.kind);
            GUI.Label(estimate, Math.Max(1, (long)(item.triangles * Mathf.Clamp(percent, 1, 100) / 100)).ToString("N0"));
        }
        void Apply()
        {
            var targets = filtered.Where(i => selected.Contains(i.path)).ToList();
            if (targets.Count == 0) return;
            if (percent >= 100) { message = "100%：模型保持不变。"; return; }
            string preview = string.Join("\n", targets.Take(8).Select(i => i.path));
            if (!EditorUtility.DisplayDialog("覆盖模型源文件", "将覆盖以下 " + targets.Count + " 个模型，保留比例 " + percent.ToString("0.##")
                + "%：\n\n" + preview + (targets.Count > 8 ? "\n……" : "")
                + "\n\n不生成副本、不保留原版本；此操作不能通过 Ctrl+Z 撤销。", "覆盖并减面", "取消")) return;
            busy = true;
            int done = 0, failed = 0;
            var lines = new List<string>();
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("模型减面", "(" + (i + 1) + "/" + targets.Count + ") " + targets[i].name, (float)i / targets.Count)) break;
                    try { string result = ModelReductionCore.Replace(targets[i].path, percent, preserveBorders); lines.Add(result); Debug.Log("[模型减面] " + result); done++; }
                    catch (Exception e) { failed++; lines.Add(targets[i].name + "：" + e.Message); Debug.LogWarning("[模型减面] " + targets[i].path + "\n" + e.Message); }
                }
                string report = "完成 " + done + " 个，跳过/失败 " + failed + " 个。\n" + string.Join("\n", lines);
                message = report.Length > 600 ? report.Substring(0, 600) + "\n完整记录见 Console。" : report;
            }
            finally { EditorUtility.ClearProgressBar(); busy = false; Refresh(); }
        }
    }
}
