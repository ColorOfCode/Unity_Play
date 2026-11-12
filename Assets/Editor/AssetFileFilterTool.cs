using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 工具菜单 -> 文件筛选
/// 扫描工程内资源，按 图片 / 静态模型 / 骨骼模型 / Prefabs / 粒子特效 分类列出，
/// 支持按文件本地大小(MB)筛选（默认 0 ~ 100MB），按大小由高到低排列。
/// 点击某一行的"名字"会在 Project 窗口中定位并选中该文件。
/// 注：Unity 2021.3 无 MultiColumnListView（2022.1 才加入），故表格用 IMGUI 实现。
/// </summary>
public class AssetFileFilterTool : EditorWindow
{
    // ---------------------------------------------------------------
    // 菜单项：显示在菜单栏顶部下拉菜单 "工具" 中（自定义菜单默认排在 Help 之后）
    // ---------------------------------------------------------------
    [MenuItem("工具/文件筛选")]
    public static void OpenWindow()
    {
        var win = GetWindow<AssetFileFilterTool>();
        win.titleContent = new GUIContent("文件筛选");
        win.minSize = new Vector2(560f, 320f);
    }

    // ---------------------------------------------------------------
    // 数据结构
    // ---------------------------------------------------------------
    private class AssetItem
    {
        public string Category;   // 筛选项目（分类）
        public string Name;       // 名字
        public string Path;       // 完整资源路径（用于 Ping / 选中）
        public double SizeMB;     // 文件本地大小（MB）
    }

    // 分类常量
    private const string CatAll = "所有";
    private const string CatTexture = "图片";
    private const string CatStaticModel = "静态模型";
    private const string CatSkinnedModel = "骨骼模型";
    private const string CatPrefab = "Prefabs";
    private const string CatParticle = "粒子特效";

    // 切换键顺序：所有 -> 各分类
    private static readonly string[] Categories =
    {
        CatAll, CatTexture, CatStaticModel, CatSkinnedModel, CatPrefab, CatParticle
    };

    // 图片扩展名
    private static readonly string[] TextureExts =
    {
        ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff",
        ".bmp", ".gif", ".exr", ".hdr", ".dds"
    };

    // 列宽
    private const float NameColWidth = 300f;
    private const float SizeColWidth = 150f;

    // 当前选中的分类切换键（默认"所有"）
    private string _selectedCategory = CatAll;

    // 筛选范围（MB），默认 0 ~ 100
    private double _minMB = 0.0;
    private double _maxMB = 100.0;

    private Vector2 _scroll;
    private string _minText = "0";
    private string _maxText = "100";

    private readonly List<AssetItem> _allItems = new List<AssetItem>();
    private readonly List<AssetItem> _filteredItems = new List<AssetItem>();

    // "名字"栏链接样式（蓝色，悬停变色，模拟超链接）
    private static GUIStyle _linkStyle;
    private static GUIStyle LinkStyle
    {
        get
        {
            if (_linkStyle == null)
            {
                _linkStyle = new GUIStyle(EditorStyles.label);
                _linkStyle.normal.textColor = new Color(0.25f, 0.5f, 0.9f);
                _linkStyle.hover.textColor = new Color(0.4f, 0.65f, 1f);
            }
            return _linkStyle;
        }
    }

    // ---------------------------------------------------------------
    // 生命周期
    // ---------------------------------------------------------------
    private void OnEnable()
    {
        ScanAssets();
        ApplyFilter();
    }

    // ---------------------------------------------------------------
    // UI（IMGUI）
    // ---------------------------------------------------------------
    private void OnGUI()
    {
        // ---- 分类切换键：所有 / 图片 / 静态模型 / 骨骼模型 / Prefabs / 粒子特效 ----
        var selectedIndex = System.Array.IndexOf(Categories, _selectedCategory);
        if (selectedIndex < 0) selectedIndex = 0;
        var newIndex = GUILayout.Toolbar(selectedIndex, Categories);
        if (newIndex != selectedIndex)
        {
            _selectedCategory = Categories[newIndex];
            ApplyFilter();
        }

        // ---- 工具栏：筛选范围 + 刷新 ----
        EditorGUILayout.BeginHorizontal(GUILayout.Height(22f));
        GUILayout.Label("筛选范围（MB）", GUILayout.Width(90f));

        _minText = EditorGUILayout.TextField(_minText, GUILayout.Width(55f));
        GUILayout.Label(" - ", GUILayout.Width(24f));
        _maxText = EditorGUILayout.TextField(_maxText, GUILayout.Width(55f));

        if (GUILayout.Button("刷新", EditorStyles.miniButton, GUILayout.Width(44f)))
        {
            ScanAssets();
            ApplyFilter();
        }

        GUILayout.Label($"共 {_filteredItems.Count} 项", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // 应用范围改动（数值变化时自动重新筛选）
        TryParseRange();

        // ---- 表头 ----
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("名字", EditorStyles.toolbarButton, GUILayout.Width(NameColWidth));
        GUILayout.Label("文件本地大小（MB）", EditorStyles.toolbarButton, GUILayout.Width(SizeColWidth));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // ---- 表格内容 ----
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        for (var i = 0; i < _filteredItems.Count; i++)
        {
            var item = _filteredItems[i];

            // 偶数行淡灰背景，增强可读性
            if (i % 2 == 1)
            {
                var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(18f));
                EditorGUI.DrawRect(rowRect, new Color(0f, 0f, 0f, 0.045f));
            }
            else
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(18f));
            }

            // 名字栏：点击在 Project 窗口定位，悬停显示完整路径
            var nameRect = GUILayoutUtility.GetRect(
                new GUIContent(item.Name, item.Path), LinkStyle,
                GUILayout.Width(NameColWidth), GUILayout.Height(16f));
            EditorGUIUtility.AddCursorRect(nameRect, MouseCursor.Link);
            if (GUI.Button(nameRect, new GUIContent(item.Name, item.Path), LinkStyle))
                PingAsset(item);

            GUILayout.Label(item.SizeMB.ToString("F2"), GUILayout.Width(SizeColWidth));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        // 悬停 tooltip（路径提示）需要 Repaint 才能显示
        if (GUI.tooltip != string.Empty)
            Repaint();
    }

    // ---------------------------------------------------------------
    // 扫描与分类
    // ---------------------------------------------------------------
    private void ScanAssets()
    {
        _allItems.Clear();

        // 1. 图片
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            AddItem(path, CatTexture);
        }

        // 2. 模型：含 SkinnedMeshRenderer 为骨骼模型，否则静态模型
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var category = go != null && go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
                ? CatSkinnedModel
                : CatStaticModel;
            AddItem(path, category);
        }

        // 3. Prefab：以粒子为主体（粒子系统数量 >= 普通渲染器数量）归入粒子特效，其余归入 Prefabs
        //    避免鹿/火把/喷泉这类"模型为主、粒子点缀"的 Prefab 被误判为粒子特效
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                AddItem(path, CatPrefab);
                continue;
            }

            var particleCount = go.GetComponentsInChildren<ParticleSystem>(true).Length;
            var rendererCount = go.GetComponentsInChildren<Renderer>(true)
                .Count(r => !(r is ParticleSystemRenderer));

            var category = particleCount > 0 && particleCount >= rendererCount
                ? CatParticle
                : CatPrefab;
            AddItem(path, category);
        }
    }

    private void AddItem(string path, string category)
    {
        if (string.IsNullOrEmpty(path))
            return;

        var ext = Path.GetExtension(path).ToLowerInvariant();

        // 排除 .asset 等非目标文件
        if (ext == ".asset" || path.EndsWith(".meta"))
            return;

        // 图片分类下排除模型文件（有些模型导入后 t:Texture2D 命中其内嵌贴图）
        if (category == CatTexture && !TextureExts.Contains(ext))
            return;

        var fullPath = Path.GetFullPath(path);
        long bytes = 0;
        try
        {
            var fi = new FileInfo(fullPath);
            if (fi.Exists)
                bytes = fi.Length;
        }
        catch
        {
            return;
        }

        _allItems.Add(new AssetItem
        {
            Category = category,
            Name = Path.GetFileNameWithoutExtension(path),
            Path = path,
            SizeMB = bytes / (1024.0 * 1024.0)
        });
    }

    // ---------------------------------------------------------------
    // 筛选与排序（由高到低）
    // ---------------------------------------------------------------
    private void TryParseRange()
    {
        if (!double.TryParse(_minText, out var min)) min = 0.0;
        if (!double.TryParse(_maxText, out var max)) max = 100.0;
        if (min < 0) min = 0;
        if (max < min) max = min;

        // 数值有变化时才重新筛选，避免每帧重算
        if (min.Equals(_minMB) && max.Equals(_maxMB))
            return;

        _minMB = min;
        _maxMB = max;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        _filteredItems.Clear();
        foreach (var item in _allItems)
        {
            // 分类切换键：选中"所有"时不限分类，否则只保留该分类
            if (_selectedCategory != CatAll && item.Category != _selectedCategory)
                continue;

            if (item.SizeMB >= _minMB && item.SizeMB <= _maxMB)
                _filteredItems.Add(item);
        }

        // 文件本地大小 由高到低
        _filteredItems.Sort((a, b) => b.SizeMB.CompareTo(a.SizeMB));
    }

    // ---------------------------------------------------------------
    // Project 窗口定位
    // ---------------------------------------------------------------
    private static void PingAsset(AssetItem item)
    {
        var obj = AssetDatabase.LoadAssetAtPath<Object>(item.Path);
        if (obj == null)
        {
            Debug.LogWarning($"[文件筛选] 资源加载失败：{item.Path}");
            return;
        }
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
    }
}
