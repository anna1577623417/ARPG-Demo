#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从选中的 <see cref="SkillStageDefinition"/> 批量生成 <see cref="NormalRouteDefinition"/>（Stage → NormalRoute）。<br/>
/// 默认绑定：一个 Stage → 一个 NormalRoute，且 <see cref="SkillRouteDefinition.Stages"/>[0] = 该 Stage。<br/>
/// 命名 / 输出目录 / 自动推断 / EditorPrefs 持久化 全部对齐 <see cref="ActionSkillStageBatchWindow"/>。
/// </summary>
public sealed class SkillStageNormalRouteBatchWindow : EditorWindow
{
    const string PrefsPrefix = "SkillStageNormalRoute_";

    const string SkillRouteRoot = "Assets/GameMain/Scripts/4_Data/1.Skills";
    const string DefaultRouteFilePrefix = "Route_Normal_";
    const string DefaultStripFromStageName = "_SkillStage";

    static readonly string[] KnownStageSuffixes =
    {
        "_SkillStage",
        "_Stage",
        "_StageDef",
        "_Definition",
    };

    [SerializeField] string m_namePrefix = string.Empty;
    [SerializeField] string m_nameSuffix = string.Empty;
    [SerializeField] string m_routeFilePrefix = DefaultRouteFilePrefix;
    [SerializeField] string m_routeSuffix = string.Empty;
    [SerializeField] string m_stripFromStagePrefix = string.Empty;
    [SerializeField] string m_stripFromStageName = DefaultStripFromStageName;
    [SerializeField] BatchOutputPathUtil.Settings m_outputSettings;
    [SerializeField] bool m_stripStageNamePrefix = true;
    [SerializeField] bool m_stripStageNameSuffix = true;
    [SerializeField] bool m_autoInferOnSelection = true;
    [SerializeField] bool m_autoFillOutputFromFolder = true;
    [SerializeField] bool m_overwriteExisting;
    [SerializeField] bool m_writeRouteIdFromFileName = true;
    [SerializeField] bool m_showOnHud;

    Vector2 m_scroll;
    readonly List<RoutePreview> m_previews = new List<RoutePreview>(32);

    [MenuItem("Tools/Skill/Stage → NormalRoute Batch...", false, 12)]
    public static void OpenWindow()
    {
        var w = GetWindow<SkillStageNormalRouteBatchWindow>(true, "Stage → NormalRoute", true);
        w.minSize = new Vector2(460f, 420f);
        w.LoadPrefs();
        w.RebuildPreview();
    }

    [MenuItem("Assets/GameMain/SkillRoute/Generate Routes From Stages", false, 2102)]
    public static void GenerateFromProjectSelection()
    {
        var w = CreateInstance<SkillStageNormalRouteBatchWindow>();
        w.LoadPrefs();
        w.m_outputSettings.BatchFolderContent = w.InferBatchFolderFromSelection();
        w.RebuildPreview();
        w.GenerateBatch();
    }

    [MenuItem("Assets/GameMain/SkillRoute/Generate Routes From Stages", true, 2102)]
    public static bool ValidateGenerateFromSelection() => CollectStagesFromSelection().Count > 0;

    void OnEnable()
    {
        LoadPrefs();
        Selection.selectionChanged += OnEditorSelectionChanged;
        TryApplyOutputFromSelectedFolder();
        TryAutoInferStripFromSelection();
        RebuildPreview();
    }

    void OnDisable()
    {
        Selection.selectionChanged -= OnEditorSelectionChanged;
        SavePrefs();
    }

    void OnEditorSelectionChanged()
    {
        if (m_autoFillOutputFromFolder)
        {
            TryApplyOutputFromSelectedFolder();
        }

        if (m_autoInferOnSelection)
        {
            TryAutoInferStripFromSelection();
        }

        RebuildPreview();
        Repaint();
    }

    void TryApplyOutputFromSelectedFolder()
    {
        if (!BatchOutputPathUtil.TryApplySelectedFolderToOutput(ref m_outputSettings, preferRootOnly: true))
        {
            return;
        }

        var batchFromFolder = BatchOutputPathUtil.InferBatchNameFromSelectedFolder();
        if (!string.IsNullOrEmpty(batchFromFolder))
        {
            m_outputSettings.BatchFolderContent = batchFromFolder;
        }
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Stage → NormalRoute", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "从选中的 SkillStageDefinition 批量生成 NormalRouteDefinition，并绑定 stages[0]。\n" +
            "Entry / Loadout / CombatGraph 请另行配置；本工具只产出 NormalRoute 资产。",
            MessageType.Info);

        EditorGUILayout.LabelField("命名（基于 Stage 文件名）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "流程：Stage 文件名 → 剥离前后缀得【核心名】→ 再套「生成前缀/后缀」→ 再套 Route 文件名前后缀。",
            MessageType.None);

        EditorGUILayout.LabelField("① 剥离 Stage 名（可自动识别）", EditorStyles.miniBoldLabel);
        m_autoInferOnSelection = EditorGUILayout.Toggle("选中变更时自动识别前后缀", m_autoInferOnSelection);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("自动识别前后缀", GUILayout.Height(22f)))
            {
                TryAutoInferStripFromSelection(force: true);
            }

            if (GUILayout.Button("清空剥离规则", GUILayout.Width(100f)))
            {
                m_stripFromStagePrefix = string.Empty;
                m_stripFromStageName = string.Empty;
                m_stripStageNamePrefix = false;
                m_stripStageNameSuffix = false;
            }
        }

        m_stripStageNamePrefix = EditorGUILayout.Toggle("剥离 Stage 名中的前缀", m_stripStageNamePrefix);
        using (new EditorGUI.DisabledScope(!m_stripStageNamePrefix))
        {
            m_stripFromStagePrefix = EditorGUILayout.TextField("剥离前缀文本", m_stripFromStagePrefix);
        }

        m_stripStageNameSuffix = EditorGUILayout.Toggle("剥离 Stage 名中的后缀", m_stripStageNameSuffix);
        using (new EditorGUI.DisabledScope(!m_stripStageNameSuffix))
        {
            m_stripFromStageName = EditorGUILayout.TextField("剥离后缀文本", m_stripFromStageName);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("② 生成 Route 名", EditorStyles.miniBoldLabel);
        m_namePrefix = EditorGUILayout.TextField("生成前缀（加在核心名前）", m_namePrefix);
        m_nameSuffix = EditorGUILayout.TextField("生成后缀（加在核心名后）", m_nameSuffix);
        m_routeFilePrefix = EditorGUILayout.TextField("Route 文件名前缀", m_routeFilePrefix);
        m_routeSuffix = EditorGUILayout.TextField("Route 文件名后缀", m_routeSuffix);

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = "RouteBatch";
        }

        if (!m_outputSettings.CreateDirectoryIfMissing)
        {
            m_outputSettings.CreateDirectoryIfMissing = true;
        }

        m_autoFillOutputFromFolder = EditorGUILayout.Toggle(
            "选中 Project 文件夹时自动填入输出目录",
            m_autoFillOutputFromFolder);

        BatchOutputPathUtil.DrawOutputSection(
            ref m_outputSettings,
            SkillRouteRoot,
            PrefsPrefix,
            () => m_outputSettings.BatchFolderContent = InferBatchFolderFromSelection());

        EditorGUILayout.Space(6f);
        m_writeRouteIdFromFileName = EditorGUILayout.Toggle("写入 routeId（= 输出文件名）", m_writeRouteIdFromFileName);
        m_showOnHud = EditorGUILayout.Toggle("ShowOnHud（默认 false：4 向闪避等不占 HUD）", m_showOnHud);
        m_overwriteExisting = EditorGUILayout.Toggle("覆盖已存在资产", m_overwriteExisting);

        if (EditorGUI.EndChangeCheck())
        {
            RebuildPreview();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"预览（{m_previews.Count} 个 Stage）", EditorStyles.boldLabel);

        if (m_previews.Count == 0)
        {
            EditorGUILayout.HelpBox("在 Project 中选中 SkillStageDefinition 资产或所在文件夹。", MessageType.Warning);
        }
        else
        {
            m_scroll = EditorGUILayout.BeginScrollView(m_scroll, GUILayout.MaxHeight(220f));
            for (var i = 0; i < m_previews.Count; i++)
            {
                var p = m_previews[i];
                EditorGUILayout.LabelField($"• {p.StageName}  →  核心名「{p.CoreName}」", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"    {p.RouteFile}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(12f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("刷新预览", GUILayout.Height(28f)))
            {
                RebuildPreview();
            }

            if (GUILayout.Button("一键生成", GUILayout.Height(28f)))
            {
                GenerateBatch();
            }
        }
    }

    void RebuildPreview()
    {
        m_previews.Clear();
        var outputDir = ResolveOutputDir();
        var stages = CollectStagesFromSelection();
        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            if (stage == null)
            {
                continue;
            }

            var core = BuildCoreName(stage.name);
            var fileName = BuildRouteFileName(core);
            m_previews.Add(new RoutePreview
            {
                StageName = stage.name,
                CoreName = core,
                RouteFile = $"{outputDir}/{fileName}.asset",
            });
        }
    }

    void GenerateBatch()
    {
        var stages = CollectStagesFromSelection();
        if (stages.Count == 0)
        {
            EditorUtility.DisplayDialog("Stage → NormalRoute", "未选中任何 SkillStageDefinition。", "OK");
            return;
        }

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = InferBatchFolderFromSelection();
        }

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = "RouteBatch";
        }

        var outputDir = ResolveOutputDir();
        if (!BatchOutputPathUtil.TryPrepareOutputDirectory(outputDir, m_outputSettings.CreateDirectoryIfMissing, out var err))
        {
            EditorUtility.DisplayDialog("Stage → NormalRoute", err, "OK");
            return;
        }

        var created = 0;
        var skipped = 0;
        var log = new StringBuilder(256);

        try
        {
            AssetDatabase.StartAssetEditing();
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (stage == null)
                {
                    continue;
                }

                if (TryGenerateOne(stage, outputDir, log, out var ok))
                {
                    if (ok)
                    {
                        created++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RebuildPreview();

        Debug.Log($"[SkillStageNormalRoute] 完成：{created} 个，跳过 {skipped} 个 → {outputDir}\n{log}");
        EditorUtility.DisplayDialog(
            "Stage → NormalRoute",
            $"生成/更新 {created} 个 Route\n跳过 {skipped} 个（已存在且未勾选覆盖）\n\n输出：{outputDir}",
            "OK");
    }

    bool TryGenerateOne(SkillStageDefinition stage, string outputDir, StringBuilder log, out bool createdOrUpdated)
    {
        createdOrUpdated = false;
        var core = BuildCoreName(stage.name);
        var fileName = BuildRouteFileName(core);
        var path = $"{outputDir}/{fileName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<NormalRouteDefinition>(path);
        if (existing != null && !m_overwriteExisting)
        {
            return true;
        }

        var route = existing != null ? existing : ScriptableObject.CreateInstance<NormalRouteDefinition>();
        var so = new SerializedObject(route);

        // stages[0] = stage（NormalRoute 单段释放型）
        var stagesProp = so.FindProperty("stages");
        if (stagesProp != null)
        {
            stagesProp.arraySize = 1;
            stagesProp.GetArrayElementAtIndex(0).objectReferenceValue = stage;
        }

        if (m_writeRouteIdFromFileName)
        {
            var routeIdProp = so.FindProperty("routeId");
            if (routeIdProp != null)
            {
                routeIdProp.stringValue = fileName;
            }
        }

        // ShowOnHud：4 向闪避等子招通常不直接占 HUD；由工具默认 false。
        var showHudProp = so.FindProperty("showOnHud");
        if (showHudProp != null)
        {
            showHudProp.boolValue = m_showOnHud;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        if (existing == null)
        {
            AssetDatabase.CreateAsset(route, path);
        }
        else
        {
            EditorUtility.SetDirty(route);
        }

        createdOrUpdated = true;
        log.AppendLine($"  ✓ {stage.name} → {fileName}");
        return true;
    }

    string BuildCoreName(string stageAssetName)
    {
        var core = SanitizeAssetName(stageAssetName);
        core = StripAffixesFromStageName(core);

        if (!string.IsNullOrEmpty(m_namePrefix))
        {
            core = m_namePrefix + core;
        }

        if (!string.IsNullOrEmpty(m_nameSuffix))
        {
            core += m_nameSuffix;
        }

        return string.IsNullOrEmpty(core) ? "UnnamedCore" : core;
    }

    string StripAffixesFromStageName(string name)
    {
        var core = name;
        if (m_stripStageNamePrefix && !string.IsNullOrEmpty(m_stripFromStagePrefix)
            && core.StartsWith(m_stripFromStagePrefix, StringComparison.Ordinal))
        {
            core = core.Substring(m_stripFromStagePrefix.Length);
        }

        if (m_stripStageNameSuffix && !string.IsNullOrEmpty(m_stripFromStageName)
            && core.EndsWith(m_stripFromStageName, StringComparison.Ordinal))
        {
            core = core.Substring(0, core.Length - m_stripFromStageName.Length);
        }

        // 额外兜底：剥掉项目里常见的 "Stage_" 文件名前缀（Stage_Dodge_Forward → Dodge_Forward）
        if (m_stripStageNamePrefix
            && string.IsNullOrEmpty(m_stripFromStagePrefix)
            && core.StartsWith("Stage_", StringComparison.Ordinal))
        {
            core = core.Substring("Stage_".Length);
        }

        return core.Trim('_', '-', ' ');
    }

    void TryAutoInferStripFromSelection(bool force = false)
    {
        var stages = CollectStagesFromSelection();
        if (stages.Count == 0)
        {
            return;
        }

        var names = new string[stages.Count];
        for (var i = 0; i < stages.Count; i++)
        {
            names[i] = stages[i].name;
        }

        if (!StageNameAffixInferer.TryInfer(names, out var prefix, out var suffix, out var batchHint))
        {
            if (!force)
            {
                return;
            }
        }

        if (!string.IsNullOrEmpty(suffix))
        {
            m_stripFromStageName = suffix;
            m_stripStageNameSuffix = true;
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            m_stripFromStagePrefix = prefix;
            m_stripStageNamePrefix = true;
        }

        if (!string.IsNullOrEmpty(batchHint)
            && (force || string.IsNullOrEmpty(m_outputSettings.BatchFolderContent)
                || m_outputSettings.BatchFolderContent == "RouteBatch"))
        {
            m_outputSettings.BatchFolderContent = batchHint;
        }
    }

    string ResolveOutputDir() =>
        BatchOutputPathUtil.ResolveOutputDirectory(
            SkillRouteRoot, in m_outputSettings, m_outputSettings.BatchFolderContent);

    string BuildRouteFileName(string core) =>
        $"{m_routeFilePrefix}{core}{m_routeSuffix}";

    string InferBatchFolderFromSelection()
    {
        var fromFolder = BatchOutputPathUtil.InferBatchNameFromSelectedFolder();
        if (!string.IsNullOrEmpty(fromFolder))
        {
            return fromFolder;
        }

        var stages = CollectStagesFromSelection();
        if (stages.Count == 0)
        {
            return "RouteBatch";
        }

        var names = new string[stages.Count];
        for (var i = 0; i < stages.Count; i++)
        {
            names[i] = stages[i].name;
        }

        if (StageNameAffixInferer.TryInfer(names, out _, out _, out var batchHint)
            && !string.IsNullOrEmpty(batchHint))
        {
            return BatchOutputPathUtil.SanitizeFolderToken(batchHint);
        }

        if (stages.Count == 1)
        {
            return BatchOutputPathUtil.SanitizeFolderToken(
                StripAffixesFromStageName(SanitizeAssetName(stages[0].name)));
        }

        return "RouteBatch";
    }

    static List<SkillStageDefinition> CollectStagesFromSelection()
    {
        var list = new List<SkillStageDefinition>(16);
        var objs = Selection.objects;
        if (objs == null)
        {
            return list;
        }

        for (var i = 0; i < objs.Length; i++)
        {
            switch (objs[i])
            {
                case SkillStageDefinition stage:
                    if (!list.Contains(stage))
                    {
                        list.Add(stage);
                    }

                    break;
                case DefaultAsset when AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(objs[i])):
                    AddStagesInFolder(AssetDatabase.GetAssetPath(objs[i]), list);
                    break;
                default:
                {
                    var path = AssetDatabase.GetAssetPath(objs[i]);
                    if (string.IsNullOrEmpty(path))
                    {
                        break;
                    }

                    var loaded = AssetDatabase.LoadAssetAtPath<SkillStageDefinition>(path);
                    if (loaded != null && !list.Contains(loaded))
                    {
                        list.Add(loaded);
                    }

                    break;
                }
            }
        }

        list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        return list;
    }

    static void AddStagesInFolder(string folder, List<SkillStageDefinition> list)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var guids = AssetDatabase.FindAssets("t:SkillStageDefinition", new[] { folder });
        for (var i = 0; i < guids.Length; i++)
        {
            var stage = AssetDatabase.LoadAssetAtPath<SkillStageDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (stage != null && !list.Contains(stage))
            {
                list.Add(stage);
            }
        }
    }

    static string SanitizeAssetName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "UnnamedStage";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(raw.Length);
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

    void LoadPrefs()
    {
        m_namePrefix = EditorPrefs.GetString(PrefsPrefix + "NamePrefix", string.Empty);
        m_nameSuffix = EditorPrefs.GetString(PrefsPrefix + "NameSuffix", string.Empty);
        m_routeFilePrefix = EditorPrefs.GetString(PrefsPrefix + "RoutePrefix", DefaultRouteFilePrefix);
        m_routeSuffix = EditorPrefs.GetString(PrefsPrefix + "RouteSuffix", string.Empty);
        m_stripFromStagePrefix = EditorPrefs.GetString(PrefsPrefix + "StripPrefix", string.Empty);
        m_stripFromStageName = EditorPrefs.GetString(PrefsPrefix + "StripSuffix", DefaultStripFromStageName);
        m_outputSettings.BatchFolderContent = EditorPrefs.GetString(PrefsPrefix + "Batch", "RouteBatch");
        m_outputSettings.UseCustomRoot = EditorPrefs.GetBool(PrefsPrefix + "CustomRoot", false);
        m_outputSettings.CustomRoot = EditorPrefs.GetString(PrefsPrefix + "CustomRootPath", string.Empty);
        m_outputSettings.SubfolderMode = (BatchOutputPathUtil.BatchSubfolderMode)EditorPrefs.GetInt(
            PrefsPrefix + "SubfolderMode", (int)BatchOutputPathUtil.BatchSubfolderMode.NewPrefixAndBatch);
        m_outputSettings.CreateDirectoryIfMissing = EditorPrefs.GetBool(PrefsPrefix + "CreateDir", true);
        m_stripStageNamePrefix = EditorPrefs.GetBool(PrefsPrefix + "StripPrefixOn", true);
        m_stripStageNameSuffix = EditorPrefs.GetBool(PrefsPrefix + "StripSuffixOn", true);
        m_autoInferOnSelection = EditorPrefs.GetBool(PrefsPrefix + "AutoInfer", true);
        m_autoFillOutputFromFolder = EditorPrefs.GetBool(PrefsPrefix + "AutoFillFolder", true);
        m_overwriteExisting = EditorPrefs.GetBool(PrefsPrefix + "Overwrite", false);
        m_writeRouteIdFromFileName = EditorPrefs.GetBool(PrefsPrefix + "RouteId", true);
        m_showOnHud = EditorPrefs.GetBool(PrefsPrefix + "ShowOnHud", false);
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PrefsPrefix + "NamePrefix", m_namePrefix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "NameSuffix", m_nameSuffix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "RoutePrefix", m_routeFilePrefix ?? DefaultRouteFilePrefix);
        EditorPrefs.SetString(PrefsPrefix + "RouteSuffix", m_routeSuffix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "StripPrefix", m_stripFromStagePrefix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "StripSuffix", m_stripFromStageName ?? DefaultStripFromStageName);
        EditorPrefs.SetString(PrefsPrefix + "Batch", m_outputSettings.BatchFolderContent ?? "RouteBatch");
        EditorPrefs.SetBool(PrefsPrefix + "CustomRoot", m_outputSettings.UseCustomRoot);
        EditorPrefs.SetString(PrefsPrefix + "CustomRootPath", m_outputSettings.CustomRoot ?? string.Empty);
        EditorPrefs.SetInt(PrefsPrefix + "SubfolderMode", (int)m_outputSettings.SubfolderMode);
        EditorPrefs.SetBool(PrefsPrefix + "CreateDir", m_outputSettings.CreateDirectoryIfMissing);
        EditorPrefs.SetBool(PrefsPrefix + "StripPrefixOn", m_stripStageNamePrefix);
        EditorPrefs.SetBool(PrefsPrefix + "StripSuffixOn", m_stripStageNameSuffix);
        EditorPrefs.SetBool(PrefsPrefix + "AutoInfer", m_autoInferOnSelection);
        EditorPrefs.SetBool(PrefsPrefix + "AutoFillFolder", m_autoFillOutputFromFolder);
        EditorPrefs.SetBool(PrefsPrefix + "Overwrite", m_overwriteExisting);
        EditorPrefs.SetBool(PrefsPrefix + "RouteId", m_writeRouteIdFromFileName);
        EditorPrefs.SetBool(PrefsPrefix + "ShowOnHud", m_showOnHud);
    }

    struct RoutePreview
    {
        public string StageName;
        public string CoreName;
        public string RouteFile;
    }

    /// <summary>从一组 Stage 资产名推断公共前缀/后缀（按 _ 分词边界对齐）。</summary>
    static class StageNameAffixInferer
    {
        public static bool TryInfer(string[] names, out string prefix, out string suffix, out string batchHint)
        {
            prefix = null;
            suffix = null;
            batchHint = null;
            if (names == null || names.Length == 0)
            {
                return false;
            }

            suffix = InferSuffix(names);
            prefix = InferPrefix(names, suffix);

            if (names.Length >= 2 && !string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(suffix))
            {
                var middles = new List<string>(names.Length);
                for (var i = 0; i < names.Length; i++)
                {
                    var mid = ExtractMiddle(names[i], prefix, suffix);
                    if (!string.IsNullOrEmpty(mid))
                    {
                        middles.Add(mid);
                    }
                }

                if (middles.Count >= 2)
                {
                    var midPrefix = LongestCommonPrefix(middles);
                    midPrefix = TrimToTokenBoundary(midPrefix, keepTrailingUnderscore: true);
                    batchHint = midPrefix.TrimEnd('_');
                }
            }

            if (string.IsNullOrEmpty(batchHint) && names.Length == 1)
            {
                batchHint = ExtractMiddle(names[0], prefix, suffix);
            }

            if (string.IsNullOrEmpty(batchHint))
            {
                batchHint = InferBatchFromPrefixToken(prefix);
            }

            return !string.IsNullOrEmpty(prefix) || !string.IsNullOrEmpty(suffix);
        }

        static string InferBatchFromPrefixToken(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return null;
            }

            var p = prefix.TrimEnd('_');
            var idx = p.LastIndexOf('_');
            return idx >= 0 ? p.Substring(idx + 1) : p;
        }

        static string InferSuffix(string[] names)
        {
            if (names.Length == 1)
            {
                return InferKnownSuffix(names[0]);
            }

            var lcs = LongestCommonSuffix(names);
            if (!string.IsNullOrEmpty(lcs) && lcs.Length >= 4 && lcs[0] == '_')
            {
                return lcs;
            }

            for (var i = 0; i < KnownStageSuffixes.Length; i++)
            {
                if (AllEndWith(names, KnownStageSuffixes[i]))
                {
                    return KnownStageSuffixes[i];
                }
            }

            return !string.IsNullOrEmpty(lcs) && lcs[0] == '_' ? lcs : null;
        }

        static string InferPrefix(string[] names, string suffix)
        {
            if (names.Length == 1)
            {
                var n = names[0];
                if (!string.IsNullOrEmpty(suffix) && n.EndsWith(suffix, StringComparison.Ordinal))
                {
                    n = n.Substring(0, n.Length - suffix.Length);
                }

                return InferSinglePrefix(n);
            }

            var lcp = LongestCommonPrefix(names);
            lcp = TrimToTokenBoundary(lcp, keepTrailingUnderscore: true);
            return string.IsNullOrEmpty(lcp) ? null : lcp;
        }

        static string InferSinglePrefix(string nameWithoutSuffix)
        {
            if (string.IsNullOrEmpty(nameWithoutSuffix))
            {
                return null;
            }

            var parts = nameWithoutSuffix.Split('_');
            if (parts.Length <= 1)
            {
                return null;
            }

            // Stage_Dodge_Forward → Stage_ 为公共前缀
            if (parts.Length >= 2 && parts[0] == "Stage")
            {
                return "Stage_";
            }

            // 多段名：默认剥掉除最后一段外的公共头
            if (parts.Length >= 3)
            {
                var head = string.Join("_", parts, 0, parts.Length - 1) + "_";
                return head;
            }

            return null;
        }

        static string InferKnownSuffix(string name)
        {
            for (var i = 0; i < KnownStageSuffixes.Length; i++)
            {
                if (name.EndsWith(KnownStageSuffixes[i], StringComparison.Ordinal))
                {
                    return KnownStageSuffixes[i];
                }
            }

            return null;
        }

        static string ExtractMiddle(string name, string prefix, string suffix)
        {
            var mid = name;
            if (!string.IsNullOrEmpty(prefix) && mid.StartsWith(prefix, StringComparison.Ordinal))
            {
                mid = mid.Substring(prefix.Length);
            }

            if (!string.IsNullOrEmpty(suffix) && mid.EndsWith(suffix, StringComparison.Ordinal))
            {
                mid = mid.Substring(0, mid.Length - suffix.Length);
            }

            return mid.Trim('_', '-', ' ');
        }

        static bool AllEndWith(string[] names, string suffix)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (!names[i].EndsWith(suffix, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        static string LongestCommonPrefix(IReadOnlyList<string> names)
        {
            var first = names[0];
            var len = first.Length;
            for (var i = 1; i < names.Count; i++)
            {
                var n = names[i];
                var max = Mathf.Min(len, n.Length);
                var j = 0;
                while (j < max && first[j] == n[j])
                {
                    j++;
                }

                len = j;
                if (len == 0)
                {
                    break;
                }
            }

            return len > 0 ? first.Substring(0, len) : string.Empty;
        }

        static string LongestCommonSuffix(string[] names)
        {
            var first = names[0];
            var len = 0;
            for (var s = 1; s <= first.Length; s++)
            {
                var candidate = first.Substring(first.Length - s);
                var all = true;
                for (var i = 1; i < names.Length; i++)
                {
                    if (!names[i].EndsWith(candidate, StringComparison.Ordinal))
                    {
                        all = false;
                        break;
                    }
                }

                if (all)
                {
                    len = s;
                }
            }

            return len > 0 ? first.Substring(first.Length - len) : string.Empty;
        }

        static string TrimToTokenBoundary(string token, bool keepTrailingUnderscore)
        {
            if (string.IsNullOrEmpty(token))
            {
                return token;
            }

            if (token.EndsWith("_", StringComparison.Ordinal))
            {
                return token;
            }

            var idx = token.LastIndexOf('_');
            if (idx < 0)
            {
                return keepTrailingUnderscore ? string.Empty : token;
            }

            return token.Substring(0, idx + 1);
        }
    }
}
#endif
