#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>批次生成工具共用的输出目录解析与 Inspector 绘制。</summary>
public static class BatchOutputPathUtil
{
    public const string NewBatchPrefix = "NEW_";

    public enum BatchSubfolderMode : byte
    {
        /// <summary>根目录/NEW_{批次名}/</summary>
        NewPrefixAndBatch = 0,
        /// <summary>根目录/{批次名}/</summary>
        BatchNameOnly = 1,
        /// <summary>直接写入根目录（忽略批次名）</summary>
        RootOnly = 2,
    }

    [Serializable]
    public struct Settings
    {
        public bool UseCustomRoot;
        public string CustomRoot;
        public string BatchFolderContent;
        public BatchSubfolderMode SubfolderMode;
        public bool CreateDirectoryIfMissing;
    }

    public static void DrawOutputSection(
        ref Settings s,
        string defaultRootPath,
        string prefsKeyPrefix,
        Action inferBatchFromSelection,
        string helpExtra = null)
    {
        EditorGUILayout.LabelField("输出目录", EditorStyles.boldLabel);
        var help = "在 Project 中进入或点选目标文件夹，可自动或点击【从选中文件夹填入】。\n" +
                   "文件夹内多选 Action 时，以 Project 窗口当前目录为准。\n" +
                   "未勾选自定义时，使用默认库路径 + 下方批次子夹规则。";
        if (!string.IsNullOrEmpty(helpExtra))
        {
            help += "\n" + helpExtra;
        }

        EditorGUILayout.HelpBox(help, MessageType.None);
        EditorGUILayout.LabelField($"默认根目录：{defaultRootPath}", EditorStyles.miniLabel);

        s.UseCustomRoot = EditorGUILayout.Toggle("自定义输出根目录", s.UseCustomRoot);
        using (new EditorGUI.IndentLevelScope())
        {
            using (new EditorGUI.DisabledScope(!s.UseCustomRoot))
            {
                DrawPathRow(ref s.CustomRoot, "输出根目录", prefsKeyPrefix + "_OutRoot");
            }

            if (GUILayout.Button("从选中文件夹填入", GUILayout.Height(20f)))
            {
                if (!TryApplySelectedFolderToOutput(ref s, preferRootOnly: true))
                {
                    EditorUtility.DisplayDialog(
                        "输出目录",
                        "无法识别 Project 当前文件夹。\n" +
                        "请在 Project 左侧点选目标文件夹（或进入该文件夹后再点【填入】）。",
                        "OK");
                }
            }
        }

        EditorGUILayout.Space(4f);
        s.SubfolderMode = (BatchSubfolderMode)EditorGUILayout.EnumPopup("子夹模式", s.SubfolderMode);
        using (new EditorGUI.DisabledScope(s.SubfolderMode == BatchSubfolderMode.RootOnly))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                s.BatchFolderContent = EditorGUILayout.TextField("批次内容名", s.BatchFolderContent);
                if (GUILayout.Button("从选中推断", GUILayout.Width(100f)))
                {
                    inferBatchFromSelection?.Invoke();
                }
            }
        }

        s.CreateDirectoryIfMissing = EditorGUILayout.Toggle("不存在时创建输出目录", s.CreateDirectoryIfMissing);

        var resolved = ResolveOutputDirectory(defaultRootPath, in s, s.BatchFolderContent);
        EditorGUILayout.LabelField("解析结果", resolved, EditorStyles.wordWrappedMiniLabel);
    }

    public static void DrawClipDualOutputSection(
        ref Settings shared,
        ref string customActionRoot,
        ref string customMotionRoot,
        string defaultActionRoot,
        string defaultMotionRoot,
        Action inferBatchFromSelection)
    {
        EditorGUILayout.LabelField("输出目录", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "未勾选自定义时：Action / Motion 各用默认库路径 + 批次子夹。\n" +
            "勾选自定义后：Action / Motion 根目录独立；各行【填入】仅更新对应路径。",
            MessageType.None);
        EditorGUILayout.LabelField($"默认 Action：{defaultActionRoot}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"默认 Motion：{defaultMotionRoot}", EditorStyles.miniLabel);

        shared.UseCustomRoot = EditorGUILayout.Toggle("自定义输出根目录", shared.UseCustomRoot);
        using (new EditorGUI.IndentLevelScope())
        {
            using (new EditorGUI.DisabledScope(!shared.UseCustomRoot))
            {
                DrawPathRowWithFill(ref customActionRoot, "Action 输出根目录", "ClipOutAction", ref shared.UseCustomRoot);
                DrawPathRowWithFill(ref customMotionRoot, "Motion 输出根目录", "ClipOutMotion", ref shared.UseCustomRoot);
            }
        }

        EditorGUILayout.Space(4f);
        shared.SubfolderMode = (BatchSubfolderMode)EditorGUILayout.EnumPopup("子夹模式", shared.SubfolderMode);
        using (new EditorGUI.DisabledScope(shared.SubfolderMode == BatchSubfolderMode.RootOnly))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                shared.BatchFolderContent = EditorGUILayout.TextField("批次内容名", shared.BatchFolderContent);
                if (GUILayout.Button("从选中推断", GUILayout.Width(100f)))
                {
                    inferBatchFromSelection?.Invoke();
                }
            }
        }

        shared.CreateDirectoryIfMissing = EditorGUILayout.Toggle("不存在时创建输出目录", shared.CreateDirectoryIfMissing);

        var actionResolved = ResolveClipOutputDirectory(defaultActionRoot, customActionRoot, in shared, shared.BatchFolderContent);
        var motionResolved = ResolveClipOutputDirectory(defaultMotionRoot, customMotionRoot, in shared, shared.BatchFolderContent);
        EditorGUILayout.LabelField("Action 解析", actionResolved, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("Motion 解析", motionResolved, EditorStyles.wordWrappedMiniLabel);
    }

    /// <summary>Action → Stage → Route 三联输出：Stage / SkillUnit 根目录独立，选中文件夹可分别填入。</summary>
    public static void DrawActionStageRouteTripleOutputSection(
        ref Settings shared,
        ref string customStageRoot,
        ref string customRouteRoot,
        string defaultStageRoot,
        string defaultRouteRoot,
        Action inferBatchFromSelection)
    {
        EditorGUILayout.LabelField("输出目录", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "未勾选自定义时：Stage / Route(SkillUnit) 各用默认库路径 + 批次子夹。\n" +
            "选中 Project 中 Stage 或 SkillUnit 文件夹可自动填入对应路径；选中角色包根目录时尝试 Stage + SkillUnit 子夹。",
            MessageType.None);
        EditorGUILayout.LabelField($"默认 Stage：{defaultStageRoot}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"默认 Route(SkillUnit)：{defaultRouteRoot}", EditorStyles.miniLabel);

        shared.UseCustomRoot = EditorGUILayout.Toggle("自定义输出根目录", shared.UseCustomRoot);
        using (new EditorGUI.IndentLevelScope())
        {
            using (new EditorGUI.DisabledScope(!shared.UseCustomRoot))
            {
                DrawPathRowWithFill(ref customStageRoot, "Stage 输出根目录", "ActStageOut", ref shared.UseCustomRoot);
                DrawPathRowWithFill(ref customRouteRoot, "Route 输出根目录 (SkillUnit)", "ActRouteOut", ref shared.UseCustomRoot);
            }
        }

        EditorGUILayout.Space(4f);
        shared.SubfolderMode = (BatchSubfolderMode)EditorGUILayout.EnumPopup("子夹模式", shared.SubfolderMode);
        using (new EditorGUI.DisabledScope(shared.SubfolderMode == BatchSubfolderMode.RootOnly))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                shared.BatchFolderContent = EditorGUILayout.TextField("批次内容名", shared.BatchFolderContent);
                if (GUILayout.Button("从选中推断", GUILayout.Width(100f)))
                {
                    inferBatchFromSelection?.Invoke();
                }
            }
        }

        shared.CreateDirectoryIfMissing = EditorGUILayout.Toggle("不存在时创建输出目录", shared.CreateDirectoryIfMissing);

        var stageResolved = ResolveClipOutputDirectory(defaultStageRoot, customStageRoot, in shared, shared.BatchFolderContent);
        var routeResolved = ResolveClipOutputDirectory(defaultRouteRoot, customRouteRoot, in shared, shared.BatchFolderContent);
        EditorGUILayout.LabelField("Stage 解析", stageResolved, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("Route 解析", routeResolved, EditorStyles.wordWrappedMiniLabel);
    }

    /// <summary>选中 Stage / SkillUnit / 含二者子夹的角色包目录时填入三联输出路径。</summary>
    public static bool TryApplySelectedFolderToActionStageRouteOutputs(
        ref string customStageRoot,
        ref string customRouteRoot,
        ref Settings shared,
        bool onlyIfEmpty = true)
    {
        if (!TryGetSelectedProjectFolder(out var folder))
        {
            return false;
        }

        var filledAny = false;
        var folderName = Path.GetFileName(folder.TrimEnd('/', '\\'));

        if (string.Equals(folderName, "Stage", StringComparison.OrdinalIgnoreCase))
        {
            if (!onlyIfEmpty || string.IsNullOrWhiteSpace(customStageRoot))
            {
                customStageRoot = folder;
                filledAny = true;
            }
        }
        else if (string.Equals(folderName, "SkillUnit", StringComparison.OrdinalIgnoreCase))
        {
            if (!onlyIfEmpty || string.IsNullOrWhiteSpace(customRouteRoot))
            {
                customRouteRoot = folder;
                filledAny = true;
            }
        }
        else
        {
            var stageSub = NormalizeAssetsFolder(folder + "/Stage");
            var routeSub = NormalizeAssetsFolder(folder + "/SkillUnit");
            if (AssetDatabase.IsValidFolder(stageSub)
                && (!onlyIfEmpty || string.IsNullOrWhiteSpace(customStageRoot)))
            {
                customStageRoot = stageSub;
                filledAny = true;
            }

            if (AssetDatabase.IsValidFolder(routeSub)
                && (!onlyIfEmpty || string.IsNullOrWhiteSpace(customRouteRoot)))
            {
                customRouteRoot = routeSub;
                filledAny = true;
            }

            if (!filledAny)
            {
                if (!onlyIfEmpty || string.IsNullOrWhiteSpace(customStageRoot))
                {
                    customStageRoot = folder;
                    filledAny = true;
                }

                if (!onlyIfEmpty || string.IsNullOrWhiteSpace(customRouteRoot))
                {
                    customRouteRoot = folder;
                    filledAny = true;
                }
            }
        }

        if (filledAny)
        {
            shared.UseCustomRoot = true;
            shared.SubfolderMode = BatchSubfolderMode.RootOnly;
        }

        return filledAny;
    }

    public static string ResolveClipOutputDirectory(
        string defaultRoot,
        string customRoot,
        in Settings shared,
        string batchContent)
    {
        var s = shared;
        if (s.UseCustomRoot && !string.IsNullOrWhiteSpace(customRoot))
        {
            s.CustomRoot = customRoot;
        }
        else
        {
            s.UseCustomRoot = false;
        }

        return ResolveOutputDirectory(defaultRoot, in s, batchContent);
    }

    static void DrawPathRow(ref string path, string label, string prefsKey)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            path = EditorGUILayout.TextField(label, path);
            if (GUILayout.Button("浏览", GUILayout.Width(44f)))
            {
                var picked = PickAssetsFolder(string.IsNullOrEmpty(path) ? "Assets" : path);
                if (!string.IsNullOrEmpty(picked))
                {
                    path = picked;
                }
            }
        }
    }

    static void DrawPathRowWithFill(ref string path, string label, string prefsKey, ref bool useCustomRoot)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            path = EditorGUILayout.TextField(label, path);
            if (GUILayout.Button("填入", GUILayout.Width(40f)))
            {
                if (!TryApplySelectedFolderToCustomRoot(ref path, ref useCustomRoot))
                {
                    EditorUtility.DisplayDialog(
                        "输出目录",
                        "无法识别 Project 当前文件夹。\n" +
                        "请在 Project 左侧点选目标文件夹（或进入该文件夹后再点【填入】）。",
                        "OK");
                }
            }

            if (GUILayout.Button("浏览", GUILayout.Width(44f)))
            {
                var picked = PickAssetsFolder(string.IsNullOrEmpty(path) ? "Assets" : path);
                if (!string.IsNullOrEmpty(picked))
                {
                    path = picked;
                    useCustomRoot = true;
                }
            }
        }
    }

    /// <summary>将 Project 当前文件夹写入单条自定义根目录（不影响其它路径字段）。</summary>
    public static bool TryApplySelectedFolderToCustomRoot(ref string customRoot, ref bool useCustomRoot)
    {
        if (!TryGetSelectedProjectFolder(out var folder))
        {
            return false;
        }

        customRoot = folder;
        useCustomRoot = true;
        return true;
    }

    public static string ResolveOutputDirectory(string defaultRoot, in Settings s, string batchContent)
    {
        var root = NormalizeAssetsFolder(s.UseCustomRoot && !string.IsNullOrWhiteSpace(s.CustomRoot)
            ? s.CustomRoot
            : defaultRoot);

        if (s.SubfolderMode == BatchSubfolderMode.RootOnly)
        {
            return root;
        }

        var batch = SanitizeFolderToken(batchContent);
        if (string.IsNullOrEmpty(batch))
        {
            batch = "Batch";
        }

        var sub = s.SubfolderMode == BatchSubfolderMode.NewPrefixAndBatch
            ? NewBatchPrefix + batch
            : batch;
        return $"{root}/{sub}";
    }

    public static bool TryPrepareOutputDirectory(string outputDir, bool createIfMissing, out string error)
    {
        error = null;
        outputDir = NormalizeAssetsFolder(outputDir);
        if (string.IsNullOrEmpty(outputDir))
        {
            error = "输出目录为空。";
            return false;
        }

        if (!outputDir.StartsWith("Assets", StringComparison.Ordinal))
        {
            error = "输出目录必须在 Assets 下。";
            return false;
        }

        if (AssetDatabase.IsValidFolder(outputDir))
        {
            return true;
        }

        if (!createIfMissing)
        {
            error = $"输出目录不存在且未勾选创建：{outputDir}";
            return false;
        }

        EnsureFolder(outputDir);
        if (!AssetDatabase.IsValidFolder(outputDir))
        {
            error = $"无法创建输出目录：{outputDir}";
            return false;
        }

        return true;
    }

    public static string PickAssetsFolder(string initialFolder)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var abs = string.IsNullOrEmpty(initialFolder)
            ? projectRoot
            : Path.GetFullPath(Path.Combine(projectRoot, initialFolder.StartsWith("Assets") ? initialFolder.Substring(7) : initialFolder));
        var picked = EditorUtility.OpenFolderPanel("选择输出文件夹（须在 Assets 下）", abs, string.Empty);
        if (string.IsNullOrEmpty(picked))
        {
            return null;
        }

        picked = picked.Replace('\\', '/');
        var assetsFull = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        if (!picked.StartsWith(assetsFull, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("输出目录", "请选择项目 Assets 文件夹内的路径。", "OK");
            return null;
        }

        return "Assets" + picked.Substring(assetsFull.Length);
    }

    /// <summary>
    /// 获取 Project 当前上下文文件夹（优先 Project 窗口高亮目录，其次 Selection 中的文件夹资产）。
    /// 在文件夹内多选 Action 时，Selection 往往只有资产，需读 Project 窗口路径。
    /// </summary>
    public static bool TryGetSelectedProjectFolder(out string folder)
    {
        folder = null;

        if (TryNormalizeFolderPath(GetProjectWindowActiveFolderPath(), out folder))
        {
            return true;
        }

        if (Selection.activeObject != null
            && TryNormalizeFolderPath(AssetDatabase.GetAssetPath(Selection.activeObject), out folder))
        {
            return true;
        }

        var guids = Selection.assetGUIDs;
        if (guids != null)
        {
            for (var i = 0; i < guids.Length; i++)
            {
                if (TryNormalizeFolderPath(AssetDatabase.GUIDToAssetPath(guids[i]), out folder))
                {
                    return true;
                }
            }
        }

        var objs = Selection.objects;
        if (objs != null)
        {
            for (var i = 0; i < objs.Length; i++)
            {
                if (objs[i] == null)
                {
                    continue;
                }

                if (TryNormalizeFolderPath(AssetDatabase.GetAssetPath(objs[i]), out folder))
                {
                    return true;
                }
            }
        }

        return TryGetCommonParentFolderFromAssetSelection(out folder);
    }

    /// <summary>反射调用 Unity 内部 Project 当前目录 API（各版本可见性不同）。</summary>
    static string GetProjectWindowActiveFolderPath()
    {
        var utilType = typeof(ProjectWindowUtil);

        var getMethod = utilType.GetMethod(
            "GetActiveFolderPath",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (getMethod != null && getMethod.ReturnType == typeof(string))
        {
            try
            {
                var path = getMethod.Invoke(null, null) as string;
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }
            catch
            {
                // ignored
            }
        }

        var tryMethod = utilType.GetMethod(
            "TryGetActiveFolderPath",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (tryMethod != null)
        {
            try
            {
                var args = new object[] { string.Empty };
                if (tryMethod.Invoke(null, args) is bool ok && ok)
                {
                    var path = args[0] as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        return path;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        return null;
    }

    /// <summary>所选资产均在同一文件夹时，用该文件夹作为输出目录。</summary>
    static bool TryGetCommonParentFolderFromAssetSelection(out string folder)
    {
        folder = null;
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            return false;
        }

        string commonParent = null;
        var sawAsset = false;
        for (var i = 0; i < objs.Length; i++)
        {
            if (objs[i] == null)
            {
                continue;
            }

            var path = AssetDatabase.GetAssetPath(objs[i]);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                folder = NormalizeAssetsFolder(path);
                return true;
            }

            sawAsset = true;
            var parent = NormalizeAssetsFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
            {
                return false;
            }

            if (commonParent == null)
            {
                commonParent = parent;
            }
            else if (!string.Equals(commonParent, parent, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!sawAsset || commonParent == null)
        {
            return false;
        }

        folder = commonParent;
        return true;
    }

    static bool TryNormalizeFolderPath(string path, out string folder)
    {
        folder = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        path = NormalizeAssetsFolder(path);
        if (!AssetDatabase.IsValidFolder(path))
        {
            return false;
        }

        folder = path;
        return true;
    }

    /// <summary>将 Project 选中的文件夹写入输出设置；成功时可选设为【直接写入根目录】。</summary>
    public static bool TryApplySelectedFolderToOutput(ref Settings s, bool preferRootOnly)
    {
        if (!TryGetSelectedProjectFolder(out var folder))
        {
            return false;
        }

        s.CustomRoot = folder;
        s.UseCustomRoot = true;
        if (preferRootOnly)
        {
            s.SubfolderMode = BatchSubfolderMode.RootOnly;
        }

        return true;
    }

    /// <summary>选中文件夹时返回文件夹名；否则 null。</summary>
    public static string InferBatchNameFromSelectedFolder()
    {
        if (!TryGetSelectedProjectFolder(out var folder))
        {
            return null;
        }

        var name = Path.GetFileName(folder.TrimEnd('/', '\\'));
        return SanitizeFolderToken(name);
    }

    public static string NormalizeAssetsFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Replace('\\', '/').TrimEnd('/');
    }

    public static string SanitizeFolderToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            var ok = true;
            for (var j = 0; j < invalid.Length; j++)
            {
                if (c == invalid[j])
                {
                    ok = false;
                    break;
                }
            }

            sb.Append(ok ? c : '_');
        }

        return sb.ToString().Trim();
    }

    public static void EnsureFolder(string folderPath)
    {
        folderPath = NormalizeAssetsFolder(folderPath);
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
#endif
