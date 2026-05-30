#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从选中的 <see cref="ActionDataSO"/> 批量生成 <see cref="SkillStageDefinition"/>（Action → SkillStage）。<br/>
/// 命名与输出目录风格对齐 <see cref="ClipActionMotionBatchWindow"/>。
/// </summary>
public sealed class ActionSkillStageBatchWindow : EditorWindow
{
    const string PrefsPrefix = "ActionSkillStage_";

    const string SkillStageRoot = "Assets/GameMain/Scripts/4_Data/1.Skills";
    const string DefaultStageFilePrefix = "Stage_";
    const string DefaultStripFromActionName = "_ActionData";

    static readonly string[] KnownActionSuffixes =
    {
        "_ActionData",
        "_Action",
        "_Data",
    };

    [SerializeField] string m_namePrefix = string.Empty;
    [SerializeField] string m_nameSuffix = string.Empty;
    [SerializeField] string m_stageFilePrefix = DefaultStageFilePrefix;
    [SerializeField] string m_stageSuffix = string.Empty;
    [SerializeField] string m_stripFromActionPrefix = string.Empty;
    [SerializeField] string m_stripFromActionName = DefaultStripFromActionName;
    [SerializeField] BatchOutputPathUtil.Settings m_outputSettings;
    [SerializeField] bool m_stripActionNamePrefix = true;
    [SerializeField] bool m_stripActionNameSuffix = true;
    [SerializeField] bool m_autoInferOnSelection = true;
    [SerializeField] bool m_autoFillOutputFromFolder = true;
    [SerializeField] bool m_overwriteExisting;
    [SerializeField] bool m_writeStageIdFromFileName = true;

    Vector2 m_scroll;
    readonly List<StagePreview> m_previews = new List<StagePreview>(32);

    [MenuItem("Tools/SkillRoute/Action → SkillStage Batch...", false, 12)]
    public static void OpenWindow()
    {
        var w = GetWindow<ActionSkillStageBatchWindow>(true, "Action → SkillStage", true);
        w.minSize = new Vector2(460f, 400f);
        w.LoadPrefs();
        w.RebuildPreview();
    }

    [MenuItem("Assets/GameMain/SkillRoute/Generate Stages From Actions", false, 2101)]
    public static void GenerateFromProjectSelection()
    {
        var w = CreateInstance<ActionSkillStageBatchWindow>();
        w.LoadPrefs();
        w.m_outputSettings.BatchFolderContent = w.InferBatchFolderFromSelection();
        w.RebuildPreview();
        w.GenerateBatch();
    }

    [MenuItem("Assets/GameMain/SkillRoute/Generate Stages From Actions", true, 2101)]
    public static bool ValidateGenerateFromSelection() => CollectActionsFromSelection().Count > 0;

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

        EditorGUILayout.LabelField("Action → SkillStage", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "从选中的 ActionDataSO 批量生成 SkillStageDefinition，并绑定 action 字段。\n" +
            "Route / Entry / Loadout 请另行配置；本工具只产出 Stage 资产。",
            MessageType.Info);

        EditorGUILayout.LabelField("命名（基于 Action 文件名）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "流程：Action 文件名 → 剥离前后缀得【核心名】→ 再套「生成前缀/后缀」→ 再套 Stage 文件名前后缀。",
            MessageType.None);

        EditorGUILayout.LabelField("① 剥离 Action 名（可自动识别）", EditorStyles.miniBoldLabel);
        m_autoInferOnSelection = EditorGUILayout.Toggle("选中变更时自动识别前后缀", m_autoInferOnSelection);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("自动识别前后缀", GUILayout.Height(22f)))
            {
                TryAutoInferStripFromSelection(force: true);
            }

            if (GUILayout.Button("清空剥离规则", GUILayout.Width(100f)))
            {
                m_stripFromActionPrefix = string.Empty;
                m_stripFromActionName = string.Empty;
                m_stripActionNamePrefix = false;
                m_stripActionNameSuffix = false;
            }
        }

        m_stripActionNamePrefix = EditorGUILayout.Toggle("剥离 Action 名中的前缀", m_stripActionNamePrefix);
        using (new EditorGUI.DisabledScope(!m_stripActionNamePrefix))
        {
            m_stripFromActionPrefix = EditorGUILayout.TextField("剥离前缀文本", m_stripFromActionPrefix);
        }

        m_stripActionNameSuffix = EditorGUILayout.Toggle("剥离 Action 名中的后缀", m_stripActionNameSuffix);
        using (new EditorGUI.DisabledScope(!m_stripActionNameSuffix))
        {
            m_stripFromActionName = EditorGUILayout.TextField("剥离后缀文本", m_stripFromActionName);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("② 生成 Stage 名", EditorStyles.miniBoldLabel);
        m_namePrefix = EditorGUILayout.TextField("生成前缀（加在核心名前）", m_namePrefix);
        m_nameSuffix = EditorGUILayout.TextField("生成后缀（加在核心名后）", m_nameSuffix);
        m_stageFilePrefix = EditorGUILayout.TextField("Stage 文件名前缀", m_stageFilePrefix);
        m_stageSuffix = EditorGUILayout.TextField("Stage 文件名后缀", m_stageSuffix);

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = "ActionBatch";
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
            SkillStageRoot,
            PrefsPrefix,
            () => m_outputSettings.BatchFolderContent = InferBatchFolderFromSelection());

        EditorGUILayout.Space(6f);
        m_writeStageIdFromFileName = EditorGUILayout.Toggle("写入 stageId（= 输出文件名）", m_writeStageIdFromFileName);
        m_overwriteExisting = EditorGUILayout.Toggle("覆盖已存在资产", m_overwriteExisting);

        if (EditorGUI.EndChangeCheck())
        {
            RebuildPreview();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"预览（{m_previews.Count} 个 Action）", EditorStyles.boldLabel);

        if (m_previews.Count == 0)
        {
            EditorGUILayout.HelpBox("在 Project 中选中 ActionDataSO 资产或所在文件夹。", MessageType.Warning);
        }
        else
        {
            m_scroll = EditorGUILayout.BeginScrollView(m_scroll, GUILayout.MaxHeight(220f));
            for (var i = 0; i < m_previews.Count; i++)
            {
                var p = m_previews[i];
                EditorGUILayout.LabelField($"• {p.ActionName}  →  核心名「{p.CoreName}」", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"    {p.StageFile}", EditorStyles.miniLabel);
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
        var actions = CollectActionsFromSelection();
        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action == null)
            {
                continue;
            }

            var core = BuildCoreName(action.name);
            var fileName = BuildStageFileName(core);
            m_previews.Add(new StagePreview
            {
                ActionName = action.name,
                CoreName = core,
                StageFile = $"{outputDir}/{fileName}.asset",
            });
        }
    }

    void GenerateBatch()
    {
        var actions = CollectActionsFromSelection();
        if (actions.Count == 0)
        {
            EditorUtility.DisplayDialog("Action → SkillStage", "未选中任何 ActionDataSO。", "OK");
            return;
        }

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = InferBatchFolderFromSelection();
        }

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = "ActionBatch";
        }

        var outputDir = ResolveOutputDir();
        if (!BatchOutputPathUtil.TryPrepareOutputDirectory(outputDir, m_outputSettings.CreateDirectoryIfMissing, out var err))
        {
            EditorUtility.DisplayDialog("Action → SkillStage", err, "OK");
            return;
        }

        var created = 0;
        var skipped = 0;
        var log = new StringBuilder(256);

        try
        {
            AssetDatabase.StartAssetEditing();
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                if (TryGenerateOne(action, outputDir, log, out var ok))
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

        Debug.Log($"[ActionSkillStage] 完成：{created} 个，跳过 {skipped} 个 → {outputDir}\n{log}");
        EditorUtility.DisplayDialog(
            "Action → SkillStage",
            $"生成/更新 {created} 个 Stage\n跳过 {skipped} 个（已存在且未勾选覆盖）\n\n输出：{outputDir}",
            "OK");
    }

    bool TryGenerateOne(ActionDataSO action, string outputDir, StringBuilder log, out bool createdOrUpdated)
    {
        createdOrUpdated = false;
        var core = BuildCoreName(action.name);
        var fileName = BuildStageFileName(core);
        var path = $"{outputDir}/{fileName}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<SkillStageDefinition>(path);
        if (existing != null && !m_overwriteExisting)
        {
            return true;
        }

        var stage = existing != null ? existing : ScriptableObject.CreateInstance<SkillStageDefinition>();
        var so = new SerializedObject(stage);
        so.FindProperty("action").objectReferenceValue = action;
        if (m_writeStageIdFromFileName)
        {
            so.FindProperty("stageId").stringValue = fileName;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        if (existing == null)
        {
            AssetDatabase.CreateAsset(stage, path);
        }
        else
        {
            EditorUtility.SetDirty(stage);
        }

        createdOrUpdated = true;
        log.AppendLine($"  ✓ {action.name} → {fileName}");
        return true;
    }

    string BuildCoreName(string actionAssetName)
    {
        var core = SanitizeAssetName(actionAssetName);
        core = StripAffixesFromActionName(core);

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

    string StripAffixesFromActionName(string name)
    {
        var core = name;
        if (m_stripActionNamePrefix && !string.IsNullOrEmpty(m_stripFromActionPrefix)
            && core.StartsWith(m_stripFromActionPrefix, StringComparison.Ordinal))
        {
            core = core.Substring(m_stripFromActionPrefix.Length);
        }

        if (m_stripActionNameSuffix && !string.IsNullOrEmpty(m_stripFromActionName)
            && core.EndsWith(m_stripFromActionName, StringComparison.Ordinal))
        {
            core = core.Substring(0, core.Length - m_stripFromActionName.Length);
        }

        return core.Trim('_', '-', ' ');
    }

    void TryAutoInferStripFromSelection(bool force = false)
    {
        var actions = CollectActionsFromSelection();
        if (actions.Count == 0)
        {
            return;
        }

        var names = new string[actions.Count];
        for (var i = 0; i < actions.Count; i++)
        {
            names[i] = actions[i].name;
        }

        if (!ActionNameAffixInferer.TryInfer(names, out var prefix, out var suffix, out var batchHint))
        {
            if (!force)
            {
                return;
            }
        }

        if (!string.IsNullOrEmpty(suffix))
        {
            m_stripFromActionName = suffix;
            m_stripActionNameSuffix = true;
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            m_stripFromActionPrefix = prefix;
            m_stripActionNamePrefix = true;
        }

        if (!string.IsNullOrEmpty(batchHint)
            && (force || string.IsNullOrEmpty(m_outputSettings.BatchFolderContent)
                || m_outputSettings.BatchFolderContent == "ActionBatch"))
        {
            m_outputSettings.BatchFolderContent = batchHint;
        }
    }

    string ResolveOutputDir() =>
        BatchOutputPathUtil.ResolveOutputDirectory(
            SkillStageRoot, in m_outputSettings, m_outputSettings.BatchFolderContent);

    string BuildStageFileName(string core) =>
        $"{m_stageFilePrefix}{core}{m_stageSuffix}";

    string InferBatchFolderFromSelection()
    {
        var fromFolder = BatchOutputPathUtil.InferBatchNameFromSelectedFolder();
        if (!string.IsNullOrEmpty(fromFolder))
        {
            return fromFolder;
        }

        var actions = CollectActionsFromSelection();
        if (actions.Count == 0)
        {
            return "ActionBatch";
        }

        var names = new string[actions.Count];
        for (var i = 0; i < actions.Count; i++)
        {
            names[i] = actions[i].name;
        }

        if (ActionNameAffixInferer.TryInfer(names, out _, out _, out var batchHint)
            && !string.IsNullOrEmpty(batchHint))
        {
            return BatchOutputPathUtil.SanitizeFolderToken(batchHint);
        }

        if (actions.Count == 1)
        {
            return BatchOutputPathUtil.SanitizeFolderToken(
                StripAffixesFromActionName(SanitizeAssetName(actions[0].name)));
        }

        return "ActionBatch";
    }

    static List<ActionDataSO> CollectActionsFromSelection()
    {
        var list = new List<ActionDataSO>(16);
        var objs = Selection.objects;
        if (objs == null)
        {
            return list;
        }

        for (var i = 0; i < objs.Length; i++)
        {
            switch (objs[i])
            {
                case ActionDataSO action:
                    if (!list.Contains(action))
                    {
                        list.Add(action);
                    }

                    break;
                case DefaultAsset when AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(objs[i])):
                    AddActionsInFolder(AssetDatabase.GetAssetPath(objs[i]), list);
                    break;
                default:
                {
                    var path = AssetDatabase.GetAssetPath(objs[i]);
                    if (string.IsNullOrEmpty(path))
                    {
                        break;
                    }

                    var loaded = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
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

    static void AddActionsInFolder(string folder, List<ActionDataSO> list)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var guids = AssetDatabase.FindAssets("t:ActionDataSO", new[] { folder });
        for (var i = 0; i < guids.Length; i++)
        {
            var action = AssetDatabase.LoadAssetAtPath<ActionDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (action != null && !list.Contains(action))
            {
                list.Add(action);
            }
        }
    }

    static string SanitizeAssetName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "UnnamedAction";
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
        m_stageFilePrefix = EditorPrefs.GetString(PrefsPrefix + "StagePrefix", DefaultStageFilePrefix);
        m_stageSuffix = EditorPrefs.GetString(PrefsPrefix + "StageSuffix", string.Empty);
        m_stripFromActionPrefix = EditorPrefs.GetString(PrefsPrefix + "StripPrefix", string.Empty);
        m_stripFromActionName = EditorPrefs.GetString(PrefsPrefix + "StripSuffix", DefaultStripFromActionName);
        m_outputSettings.BatchFolderContent = EditorPrefs.GetString(PrefsPrefix + "Batch", "ActionBatch");
        m_outputSettings.UseCustomRoot = EditorPrefs.GetBool(PrefsPrefix + "CustomRoot", false);
        m_outputSettings.CustomRoot = EditorPrefs.GetString(PrefsPrefix + "CustomRootPath", string.Empty);
        m_outputSettings.SubfolderMode = (BatchOutputPathUtil.BatchSubfolderMode)EditorPrefs.GetInt(
            PrefsPrefix + "SubfolderMode", (int)BatchOutputPathUtil.BatchSubfolderMode.NewPrefixAndBatch);
        m_outputSettings.CreateDirectoryIfMissing = EditorPrefs.GetBool(PrefsPrefix + "CreateDir", true);
        m_stripActionNamePrefix = EditorPrefs.GetBool(PrefsPrefix + "StripPrefixOn", true);
        m_stripActionNameSuffix = EditorPrefs.GetBool(PrefsPrefix + "StripSuffixOn", true);
        m_autoInferOnSelection = EditorPrefs.GetBool(PrefsPrefix + "AutoInfer", true);
        m_overwriteExisting = EditorPrefs.GetBool(PrefsPrefix + "Overwrite", false);
        m_writeStageIdFromFileName = EditorPrefs.GetBool(PrefsPrefix + "StageId", true);
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PrefsPrefix + "NamePrefix", m_namePrefix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "NameSuffix", m_nameSuffix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "StagePrefix", m_stageFilePrefix ?? DefaultStageFilePrefix);
        EditorPrefs.SetString(PrefsPrefix + "StageSuffix", m_stageSuffix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "StripPrefix", m_stripFromActionPrefix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "StripSuffix", m_stripFromActionName ?? DefaultStripFromActionName);
        EditorPrefs.SetString(PrefsPrefix + "Batch", m_outputSettings.BatchFolderContent ?? "ActionBatch");
        EditorPrefs.SetBool(PrefsPrefix + "CustomRoot", m_outputSettings.UseCustomRoot);
        EditorPrefs.SetString(PrefsPrefix + "CustomRootPath", m_outputSettings.CustomRoot ?? string.Empty);
        EditorPrefs.SetInt(PrefsPrefix + "SubfolderMode", (int)m_outputSettings.SubfolderMode);
        EditorPrefs.SetBool(PrefsPrefix + "CreateDir", m_outputSettings.CreateDirectoryIfMissing);
        EditorPrefs.SetBool(PrefsPrefix + "StripPrefixOn", m_stripActionNamePrefix);
        EditorPrefs.SetBool(PrefsPrefix + "StripSuffixOn", m_stripActionNameSuffix);
        EditorPrefs.SetBool(PrefsPrefix + "AutoInfer", m_autoInferOnSelection);
        EditorPrefs.SetBool(PrefsPrefix + "AutoFillFolder", m_autoFillOutputFromFolder);
        EditorPrefs.SetBool(PrefsPrefix + "Overwrite", m_overwriteExisting);
        EditorPrefs.SetBool(PrefsPrefix + "StageId", m_writeStageIdFromFileName);
    }

    struct StagePreview
    {
        public string ActionName;
        public string CoreName;
        public string StageFile;
    }

    /// <summary>从一组 Action 资产名推断公共前缀/后缀（按 _ 分词边界对齐）。</summary>
    static class ActionNameAffixInferer
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

            for (var i = 0; i < KnownActionSuffixes.Length; i++)
            {
                if (AllEndWith(names, KnownActionSuffixes[i]))
                {
                    return KnownActionSuffixes[i];
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

            // 多段名：默认剥掉除最后一段外的公共头（General_Armature_XXX → General_Armature_）
            if (parts.Length >= 3)
            {
                var head = string.Join("_", parts, 0, parts.Length - 1) + "_";
                return head;
            }

            // 两段名 Wuxia_roll_front → 不自动剥前缀，留给用户或多项选中
            return null;
        }

        static string InferKnownSuffix(string name)
        {
            for (var i = 0; i < KnownActionSuffixes.Length; i++)
            {
                if (name.EndsWith(KnownActionSuffixes[i], StringComparison.Ordinal))
                {
                    return KnownActionSuffixes[i];
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
