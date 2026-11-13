using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 工具菜单 -> 修改图片尺寸
/// 列出工程内所有图片（名称/尺寸/格式/大小MB），支持：
/// 1. 按大小筛选（默认 0~100MB，由高到低排序）
/// 2. 按名称搜索（不填显示全部）
/// 3. 勾选一张或多张，输入固定宽高（如 2048x2048），
///    覆盖原有文件：保留原格式（png/jpg/tga/exr）与透明通道，仅修改尺寸。
/// 注：psd/tif/bmp 等 Unity 无内置编码器，修改时会提示"不支持写回"。
/// </summary>
public class ImageResizeTool : EditorWindow
{
    [MenuItem("工具/修改图片尺寸")]
    public static void OpenWindow()
    {
        var win = GetWindow<ImageResizeTool>();
        win.titleContent = new GUIContent("修改图片尺寸");
        win.minSize = new Vector2(640f, 360f);
    }

    // ---------------------------------------------------------------
    // 数据
    // ---------------------------------------------------------------
    private class ImageItem
    {
        public string Name;
        public string Path;
        public string Ext;       // 格式（小写扩展名）
        public int Width;
        public int Height;
        public double SizeMB;
    }

    // 可列出的图片扩展名（写回支持 png/jpg/tga/exr/hdr，hdr 走自定义 Radiance 编码器）
    private static readonly string[] TextureExts =
    {
        ".png", ".jpg", ".jpeg", ".tga", ".exr",
        ".psd", ".tif", ".tiff", ".bmp", ".gif", ".hdr", ".dds"
    };

    // 列宽
    private const float CheckColWidth = 24f;
    private const float NameColWidth = 260f;
    private const float DimColWidth = 110f;
    private const float ExtColWidth = 60f;
    private const float SizeColWidth = 100f;

    // 筛选与搜索状态
    private double _minMB = 0.0;
    private double _maxMB = 100.0;
    private string _minText = "0";
    private string _maxText = "100";
    private string _searchText = "";
    private string _folderText = ""; // 文件夹搜索：为空 = 按所有文件筛选
    private string _sizeText = "0";   // 尺寸筛选：0 = 不过滤（显示全部）

    // 修改目标尺寸
    private string _newWidthText = "2048";
    private string _newHeightText = "2048";

    private Vector2 _scroll;
    private bool _allSelected;

    private readonly List<ImageItem> _allItems = new List<ImageItem>();
    private readonly List<ImageItem> _filteredItems = new List<ImageItem>();
    private readonly HashSet<string> _selectedPaths = new HashSet<string>();

    // "名字"栏链接样式（点击在 Project 窗口定位）
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
    // UI
    // ---------------------------------------------------------------
    private void OnGUI()
    {
        // ---- 第一行：搜索 + 筛选范围 + 刷新 ----
        EditorGUILayout.BeginHorizontal(GUILayout.Height(22f));
        GUILayout.Label("搜索名称", GUILayout.Width(56f));
        var newSearch = EditorGUILayout.TextField(_searchText, GUILayout.Width(150f));
        if (newSearch != _searchText)
        {
            _searchText = newSearch;
            ApplyFilter();
        }

        GUILayout.Label("文件夹", GUILayout.Width(42f));
        var newFolder = EditorGUILayout.TextField(_folderText, GUILayout.Width(150f));
        if (newFolder != _folderText)
        {
            _folderText = newFolder;
            ApplyFilter();
        }

        GUILayout.Space(12f);
        GUILayout.Label("筛选范围（MB）", GUILayout.Width(90f));
        var newMin = EditorGUILayout.TextField(_minText, GUILayout.Width(55f));
        GUILayout.Label(" - ", GUILayout.Width(24f));
        var newMax = EditorGUILayout.TextField(_maxText, GUILayout.Width(55f));
        if (newMin != _minText || newMax != _maxText)
        {
            _minText = newMin;
            _maxText = newMax;
            TryParseRange();
        }

        GUILayout.Space(12f);
        GUILayout.Label("尺寸", GUILayout.Width(32f));
        var newSize = EditorGUILayout.TextField(_sizeText, GUILayout.Width(55f));
        if (newSize != _sizeText)
        {
            _sizeText = newSize;
            ApplyFilter();
        }
        GUILayout.Label("（0=显示全部）", EditorStyles.miniLabel, GUILayout.Width(86f));

        if (GUILayout.Button("刷新", EditorStyles.miniButton, GUILayout.Width(44f)))
        {
            ScanAssets();
            ApplyFilter();
        }
        GUILayout.Label($"共 {_filteredItems.Count} 张", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // ---- 第二行：全选 + 修改目标尺寸 + 应用 ----
        EditorGUILayout.BeginHorizontal(GUILayout.Height(24f));
        var newAll = GUILayout.Toggle(_allSelected, "全选", GUILayout.Width(44f));
        if (newAll != _allSelected)
        {
            _allSelected = newAll;
            ToggleAll(_allSelected);
        }
        GUILayout.Label($"已勾选 {_selectedPaths.Count} 张", EditorStyles.miniLabel, GUILayout.Width(80f));

        GUILayout.Space(12f);
        GUILayout.Label("修改为 宽", GUILayout.Width(58f));
        _newWidthText = EditorGUILayout.TextField(_newWidthText, GUILayout.Width(60f));
        GUILayout.Label("高", GUILayout.Width(18f));
        _newHeightText = EditorGUILayout.TextField(_newHeightText, GUILayout.Width(60f));

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("应用修改", GUILayout.Width(80f)))
            ApplyResize();
        EditorGUILayout.EndHorizontal();

        // ---- 表头 ----
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("", EditorStyles.toolbarButton, GUILayout.Width(CheckColWidth));
        GUILayout.Label("名称", EditorStyles.toolbarButton, GUILayout.Width(NameColWidth));
        GUILayout.Label("尺寸", EditorStyles.toolbarButton, GUILayout.Width(DimColWidth));
        GUILayout.Label("格式", EditorStyles.toolbarButton, GUILayout.Width(ExtColWidth));
        GUILayout.Label("大小（MB）", EditorStyles.toolbarButton, GUILayout.Width(SizeColWidth));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // ---- 表格 ----
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        for (var i = 0; i < _filteredItems.Count; i++)
        {
            var item = _filteredItems[i];

            if (i % 2 == 1)
            {
                var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(18f));
                EditorGUI.DrawRect(rowRect, new Color(0f, 0f, 0f, 0.045f));
            }
            else
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(18f));
            }

            // 勾选框（单选=只勾一张，多选=随意勾）
            var isSelected = _selectedPaths.Contains(item.Path);
            var nowSelected = GUILayout.Toggle(isSelected, GUIContent.none, GUILayout.Width(CheckColWidth));
            if (nowSelected != isSelected)
            {
                if (nowSelected) _selectedPaths.Add(item.Path);
                else _selectedPaths.Remove(item.Path);
                _allSelected = false;
            }

            // 名称：点击在 Project 窗口定位
            var nameRect = GUILayoutUtility.GetRect(
                new GUIContent(item.Name, item.Path), LinkStyle,
                GUILayout.Width(NameColWidth), GUILayout.Height(16f));
            EditorGUIUtility.AddCursorRect(nameRect, MouseCursor.Link);
            if (GUI.Button(nameRect, new GUIContent(item.Name, item.Path), LinkStyle))
                PingAsset(item.Path);

            GUILayout.Label($"{item.Width} x {item.Height}", GUILayout.Width(DimColWidth));
            GUILayout.Label(item.Ext.TrimStart('.').ToUpperInvariant(), GUILayout.Width(ExtColWidth));
            GUILayout.Label(item.SizeMB.ToString("F2"), GUILayout.Width(SizeColWidth));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        if (GUI.tooltip != string.Empty)
            Repaint();
    }

    // ---------------------------------------------------------------
    // 扫描
    // ---------------------------------------------------------------
    private void ScanAssets()
    {
        _allItems.Clear();

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!TextureExts.Contains(ext))
                continue; // 只列出图片文件（排除 fbx 内嵌贴图等）

            long bytes = 0;
            try
            {
                var fi = new FileInfo(Path.GetFullPath(path));
                if (fi.Exists) bytes = fi.Length;
            }
            catch
            {
                continue;
            }

            // 取源文件像素尺寸：png 直接读文件头（不受导入压缩/裁剪影响），其余用导入尺寸
            int w, h;
            if (!TryGetDimensions(path, ext, out w, out h))
                continue;

            _allItems.Add(new ImageItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path,
                Ext = ext,
                Width = w,
                Height = h,
                SizeMB = bytes / (1024.0 * 1024.0)
            });
        }
    }

    private static bool TryGetDimensions(string path, string ext, out int w, out int h)
    {
        w = 0;
        h = 0;

        // PNG：文件头第 16~23 字节是 IHDR 的宽高（大端）
        if (ext == ".png")
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var header = new byte[24];
                    if (fs.Read(header, 0, 24) == 24 &&
                        header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                    {
                        w = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                        h = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                        return w > 0 && h > 0;
                    }
                }
            }
            catch { }
            return false;
        }

        // Radiance .hdr：解析文件头 ASCII 的 "-Y {高} +X {宽}"
        if (ext == ".hdr")
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var buf = new byte[512];
                    var n = fs.Read(buf, 0, 512);
                    var headerText = System.Text.Encoding.ASCII.GetString(buf, 0, n);
                    var match = System.Text.RegularExpressions.Regex.Match(headerText, @"-Y\s+(\d+)\s+\+X\s+(\d+)");
                    if (match.Success)
                    {
                        h = int.Parse(match.Groups[1].Value);
                        w = int.Parse(match.Groups[2].Value);
                        return w > 0 && h > 0;
                    }
                }
            }
            catch { }
            return false;
        }

        // 其余格式：用导入后的贴图尺寸
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
            return false;
        w = tex.width;
        h = tex.height;
        return true;
    }

    // ---------------------------------------------------------------
    // 筛选：大小范围 + 名称搜索，按大小由高到低
    // ---------------------------------------------------------------
    private void TryParseRange()
    {
        if (!double.TryParse(_minText, out var min)) min = 0.0;
        if (!double.TryParse(_maxText, out var max)) max = 100.0;
        if (min < 0) min = 0;
        if (max < min) max = min;

        if (min.Equals(_minMB) && max.Equals(_maxMB))
            return;

        _minMB = min;
        _maxMB = max;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        _filteredItems.Clear();
        var keyword = _searchText.Trim();
        var folder = _folderText.Trim().Replace('\\', '/');

        // 尺寸筛选：输入 0 或非法值 = 不过滤；否则只显示 宽或高 等于该值的图片
        int.TryParse(_sizeText, out var sizeFilter);

        foreach (var item in _allItems)
        {
            // 名称搜索：不填显示全部
            if (keyword != "" &&
                item.Name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            // 文件夹搜索：不填按所有文件；填了则按路径匹配该文件夹（含其子文件夹）下的图片
            if (folder != "" &&
                item.Path.Replace('\\', '/').IndexOf(folder, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            // 尺寸筛选
            if (sizeFilter > 0 && item.Width != sizeFilter && item.Height != sizeFilter)
                continue;

            if (item.SizeMB >= _minMB && item.SizeMB <= _maxMB)
                _filteredItems.Add(item);
        }

        // 大小 由高到低
        _filteredItems.Sort((a, b) => b.SizeMB.CompareTo(a.SizeMB));
    }

    private void ToggleAll(bool on)
    {
        _selectedPaths.Clear();
        if (on)
        {
            foreach (var item in _filteredItems)
                _selectedPaths.Add(item.Path);
        }
    }

    // ---------------------------------------------------------------
    // 修改尺寸：覆盖原文件
    // ---------------------------------------------------------------
    private void ApplyResize()
    {
        // 校验目标尺寸
        if (!int.TryParse(_newWidthText, out var targetW) || targetW <= 0 ||
            !int.TryParse(_newHeightText, out var targetH) || targetH <= 0)
        {
            ShowNotification(new GUIContent("宽高必须是正整数"));
            return;
        }
        if (targetW > 16384 || targetH > 16384)
        {
            ShowNotification(new GUIContent("宽高不能超过 16384"));
            return;
        }

        // 只处理 当前列表中被勾选的图片
        var targets = _filteredItems.Where(i => _selectedPaths.Contains(i.Path)).ToList();
        if (targets.Count == 0)
        {
            ShowNotification(new GUIContent("请先勾选要修改的图片"));
            return;
        }

        // 破坏性操作确认（直接覆盖原文件，不可撤销）
        if (!EditorUtility.DisplayDialog("确认修改尺寸",
                $"将把 {targets.Count} 张图片修改为 {targetW} x {targetH}，\n" +
                "并直接覆盖原文件（保留格式与通道）。\n\n此操作不可撤销，确定继续？",
                "确定修改", "取消"))
            return;

        var okCount = 0;
        var failList = new List<string>();

        try
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var item = targets[i];
                if (EditorUtility.DisplayCancelableProgressBar("修改图片尺寸",
                        $"({i + 1}/{targets.Count}) {item.Name}  ->  {targetW} x {targetH}",
                        (float)i / targets.Count))
                {
                    break; // 用户取消，已完成的部分保留
                }

                if (ResizeImageFile(item, targetW, targetH, out var error))
                    okCount++;
                else
                    failList.Add($"{item.Name}：{error}");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // 刷新列表（大小变了，可能已不在筛选范围内）
        _selectedPaths.Clear();
        _allSelected = false;
        ScanAssets();
        ApplyFilter();

        if (failList.Count > 0)
        {
            var msg = $"成功 {okCount} 张，失败 {failList.Count} 张：\n" + string.Join("\n", failList.Take(10));
            if (failList.Count > 10) msg += $"\n……等共 {failList.Count} 张";
            Debug.LogWarning($"[修改图片尺寸] {msg}");
            EditorUtility.DisplayDialog("修改完成", msg, "知道了");
        }
        else
        {
            EditorUtility.DisplayDialog("修改完成", $"成功修改 {okCount} 张图片为 {targetW} x {targetH}", "知道了");
        }
    }

    /// <summary>
    /// 将单张图片重设为 targetW x targetH 并覆盖原文件（保留格式与通道）。
    /// </summary>
    private static bool ResizeImageFile(ImageItem item, int targetW, int targetH, out string error)
    {
        error = "";

        Texture2D source = null;
        switch (item.Ext)
        {
            case ".png":
            case ".jpg":
            case ".jpeg":
                // 直接解码源文件（不受导入设置压缩/裁剪影响），LoadImage 保留透明通道
                try
                {
                    source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!source.LoadImage(File.ReadAllBytes(item.Path)))
                    {
                        error = "源文件解码失败";
                        return false;
                    }
                }
                catch
                {
                    error = "源文件读取失败";
                    return false;
                }
                break;

            case ".tga":
            case ".exr":
                // 无内置文件解码器，用导入后的贴图做像素源
                source = AssetDatabase.LoadAssetAtPath<Texture2D>(item.Path);
                if (source == null)
                {
                    error = "贴图加载失败";
                    return false;
                }
                break;

            case ".hdr":
                // Unity 无内置 .hdr 编码器（连 Unity 6 都没有），走自定义 Radiance RGBE 编码器
                return ResizeHdr(item, targetW, targetH, out error);

            default:
                error = $"格式 {item.Ext} 不支持写回（支持 png/jpg/tga/exr/hdr）";
                return false;
        }

        byte[] encoded;
        try
        {
            encoded = BlitAndEncode(source, item.Ext, targetW, targetH);
        }
        catch (System.Exception e)
        {
            error = $"编码失败：{e.Message}";
            return false;
        }

        if (encoded == null || encoded.Length == 0)
        {
            error = "编码结果为空";
            return false;
        }

        // 覆盖原文件并重新导入
        try
        {
            File.WriteAllBytes(item.Path, encoded);
        }
        catch
        {
            error = "写入文件失败（检查文件是否被占用/只读）";
            return false;
        }
        AssetDatabase.ImportAsset(item.Path);
        return true;
    }

    /// <summary>
    /// 把 source 缩放到 targetW x targetH，并按原格式编码。
    /// PNG/TGA 走 sRGB 通路保留原观感与透明通道；EXR 走 Linear 浮点通路保留 HDR。
    /// </summary>
    private static byte[] BlitAndEncode(Texture2D source, string ext, int targetW, int targetH)
    {
        var isExr = ext == ".exr";
        var rtFormat = isExr ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32;
        var texFormat = isExr ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;
        var rwMode = isExr ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;

        var rt = RenderTexture.GetTemporary(targetW, targetH, 0, rtFormat, rwMode);
        var prev = RenderTexture.active;
        Graphics.Blit(source, rt);

        var dst = new Texture2D(targetW, targetH, texFormat, false, isExr);
        RenderTexture.active = rt;
        dst.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
        dst.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        switch (ext)
        {
            case ".png": return dst.EncodeToPNG();
            case ".jpg":
            case ".jpeg": return dst.EncodeToJPG(95);
            case ".tga": return dst.EncodeToTGA();
            case ".exr": return dst.EncodeToEXR();
            default: return null;
        }
    }

    // ---------------------------------------------------------------
    // .hdr（Radiance RGBE）专用：Unity 无内置编码器，此处按公开格式规范实现
    // ---------------------------------------------------------------

    /// <summary>
    /// 修改 .hdr 文件尺寸：临时开启可读 + 提高 MaxSize 拿到源分辨率像素，
    /// Blit 到目标尺寸后用自定义 Radiance RGBE 编码器写回原文件，最后还原导入设置。
    /// </summary>
    private static bool ResizeHdr(ImageItem item, int targetW, int targetH, out string error)
    {
        error = "";
        var importer = AssetImporter.GetAtPath(item.Path) as TextureImporter;
        if (importer == null)
        {
            error = "找不到 TextureImporter";
            return false;
        }

        // 备份导入设置；临时开启可读并保证 MaxSize 不裁剪源分辨率
        var oldReadable = importer.isReadable;
        var oldMaxSize = importer.maxTextureSize;
        var dirty = false;
        var needMax = NextPow2(System.Math.Max(item.Width, item.Height));
        if (!importer.isReadable) { importer.isReadable = true; dirty = true; }
        if (importer.maxTextureSize < needMax) { importer.maxTextureSize = needMax; dirty = true; }
        if (dirty)
            importer.SaveAndReimport();

        try
        {
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(item.Path);
            if (source == null)
            {
                error = "贴图加载失败";
                return false;
            }

            // Linear 浮点通路 Blit 到目标尺寸（保留 HDR 动态范围）
            var rt = RenderTexture.GetTemporary(targetW, targetH, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            var dst = new Texture2D(targetW, targetH, TextureFormat.RGBAFloat, false, true);
            RenderTexture.active = rt;
            dst.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
            dst.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = dst.GetPixels(); // 底->顶排列的线性浮点像素
            var bytes = EncodeRadianceHdr(pixels, targetW, targetH);
            File.WriteAllBytes(item.Path, bytes);
            return true;
        }
        catch (System.Exception e)
        {
            error = $"处理失败：{e.Message}";
            return false;
        }
        finally
        {
            // 还原导入设置（或重新导入新文件）
            if (dirty)
            {
                importer.isReadable = oldReadable;
                importer.maxTextureSize = oldMaxSize;
                importer.SaveAndReimport();
            }
            else
            {
                AssetDatabase.ImportAsset(item.Path);
            }
        }
    }

    /// <summary>大于等于 v 的最小 2 的幂（TextureImporter.MaxSize 只接受 32~16384 的 2 的幂）</summary>
    private static int NextPow2(int v)
    {
        var r = 32;
        while (r < v && r < 16384) r <<= 1;
        return r;
    }

    /// <summary>
    /// 把线性浮点像素编码为 Radiance .hdr（32-bit_rle_rgbe）字节流。
    /// Unity 没有内置 .hdr 编码 API，此实现遵循 Radiance 公开格式规范。
    /// </summary>
    private static byte[] EncodeRadianceHdr(Color[] pixels, int w, int h)
    {
        // 1) 浮点 RGB -> RGBE（8bit 尾数 + 8bit 共享指数），并按文件顺序（自顶向下）翻转行
        var rgbe = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            var srcY = h - 1 - y;
            for (var x = 0; x < w; x++)
            {
                var c = pixels[srcY * w + x];
                var i = (y * w + x) * 4;
                FloatToRgbe(c.r, c.g, c.b,
                    out rgbe[i], out rgbe[i + 1], out rgbe[i + 2], out rgbe[i + 3]);
            }
        }

        // 2) 写文件头 + RLE 压缩的扫描行
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes(
                "#?RADIANCE\nFORMAT=32-bit_rle_rgbe\nEXPOSURE=1.000000\n"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes($"-Y {h} +X {w}\n"));

            if (w < 8 || w > 32768)
            {
                // 宽度过小/过大：规范允许写不压缩的"flat"扫描行
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < w; x++)
                        for (var ch = 0; ch < 4; ch++)
                            bw.Write(rgbe[(y * w + x) * 4 + ch]);
            }
            else
            {
                var scanline = new byte[w];
                for (var y = 0; y < h; y++)
                {
                    // 扫描行头：0x02 0x02 + 16 位大端宽度
                    bw.Write((byte)0x02);
                    bw.Write((byte)0x02);
                    bw.Write((byte)((w >> 8) & 0xFF));
                    bw.Write((byte)(w & 0xFF));

                    for (var ch = 0; ch < 4; ch++)
                    {
                        for (var x = 0; x < w; x++)
                            scanline[x] = rgbe[(y * w + x) * 4 + ch];
                        WriteRleChannel(scanline, bw);
                    }
                }
            }

            bw.Flush();
            return ms.ToArray();
        }
    }

    /// <summary>浮点 RGB -> RGBE（Radiance 共享指数编码）</summary>
    private static void FloatToRgbe(float r, float g, float b,
        out byte er, out byte eg, out byte eb, out byte ee)
    {
        var v = Mathf.Max(r, Mathf.Max(g, b));
        if (v < 1e-32f)
        {
            er = eg = eb = ee = 0;
            return;
        }

        // v = m * 2^e，m ∈ [0.5, 1)
        var e = (int)Mathf.Floor(Mathf.Log(v, 2.0f)) + 1;
        var m = v / Mathf.Pow(2, e);
        if (m >= 1f) { m *= 0.5f; e += 1; }
        if (m < 0.5f) { m *= 2f; e -= 1; }

        var scale = m * 256f / v;
        er = (byte)Mathf.Clamp((int)(r * scale + 0.5f), 0, 255);
        eg = (byte)Mathf.Clamp((int)(g * scale + 0.5f), 0, 255);
        eb = (byte)Mathf.Clamp((int)(b * scale + 0.5f), 0, 255);
        ee = (byte)(e + 128);
    }

    /// <summary>对单条通道数据做规范要求的自适应 RLE（连续段 / 字面段）</summary>
    private static void WriteRleChannel(byte[] scanline, BinaryWriter bw)
    {
        var w = scanline.Length;
        var x = 0;
        while (x < w)
        {
            // 先看当前位置往后的最长连续 run（上限 128）
            var runLen = 1;
            while (x + runLen < w && runLen < 128 && scanline[x + runLen] == scanline[x])
                runLen++;

            if (runLen >= 4)
            {
                // 连续段：高位标记 0x80 + 长度，后跟 1 个字节
                bw.Write((byte)(128 + runLen));
                bw.Write(scanline[x]);
                x += runLen;
            }
            else
            {
                // 字面段：累积到出现 >=4 的 run 或满 128 个为止
                var start = x;
                var len = 0;
                while (x < w && len < 128)
                {
                    var rl = 1;
                    while (x + rl < w && rl < 128 && scanline[x + rl] == scanline[x])
                        rl++;
                    if (rl >= 4) break;
                    x++;
                    len++;
                }
                bw.Write((byte)len);
                for (var i = 0; i < len; i++)
                    bw.Write(scanline[start + i]);
            }
        }
    }

    // ---------------------------------------------------------------
    // Project 窗口定位
    // ---------------------------------------------------------------
    private static void PingAsset(string path)
    {
        var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (obj == null)
        {
            Debug.LogWarning($"[修改图片尺寸] 资源加载失败：{path}");
            return;
        }
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
    }
}

