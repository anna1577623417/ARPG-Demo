#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从选中的 <see cref="ActionDataSO"/> 批量生成 <see cref="SkillStageDefinition"/> + <see cref="NormalRouteDefinition"/>
/// （Action → Stage → NormalRoute）。UI / 命名 / 双输出目录 镜像 <see cref="ClipActionMotionBatchWindow"/>。
/// </summary>
public sealed class ActionSkillStageNormalRouteBatchWindow : EditorWindow
{
    const string PrefsPrefix = "ActionStageRoute_";

    const string SkillRoot = "Assets/GameMain/Scripts/4_Data/1.Skills";
    const string DefaultStageFilePrefix = "Stage_";
    const string DefaultRouteFilePrefix = "Route_Normal_";
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
    [SerializeField] string m_routeFilePrefix = DefaultRouteFilePrefix;
    [SerializeField] string m_routeSuffix = string.Empty;
    [SerializeField] string m_stripFromActionPrefix = string.Empty;
    [SerializeField] string m_stripFromActionName = DefaultStripFromActionName;
    [SerializeField] BatchOutputPathUtil.Settings m_outputSettings;
    [SerializeField] string m_customStageRoot = string.Empty;
    [SerializeField] string m_customRouteRoot = string.Empty;
    [SerializeField] bool m_stripActionNamePrefix = true;
    [SerializeField] bool m_stripActionNameSuffix = true;
    [SerializeField] bool m_autoInferOnSelection = true;
    [SerializeField] bool m_autoFillOutputFromFolder = true;
    [SerializeField] bool m_overwriteExisting;
    [SerializeField] bool m_writeStageIdFromFileName = true;
    [SerializeField] bool m_writeRouteIdFromFileName = true;
    [SerializeField] bool m_showOnHud;

    Vector2 m_scroll;
    readonly List<PipelinePreview> m_previews = new List<PipelinePreview>(32);

    [MenuItem("Tools/Skill/Action → Stage → Route Batch...", false, 10)]
    public static void OpenWindow()
    {
        var w = GetWindow<ActionSkillStageNormalRouteBatchWindow>(true, "Action → Stage → Route", true);
        w.minSize = new Vector2(480f, 520f);
        w.LoadPrefs();
        w.RebuildPreview();
    }

    [MenuItem("Assets/GameMain/Generate Stage + Route From Actions", false, 2103)]
    public static void GenerateFromProjectSelection()
    {
        var w = CreateInstance<ActionSkillStageNormalRouteBatchWindow>();
        w.LoadPrefs();
        w.m_outputSettings.BatchFolderContent = w.InferBatchFolderFromSelection();
        w.RebuildPreview();
        w.GenerateBatch();
    }

    [MenuItem("Assets/GameMain/Generate Stage + Route From Actions", true, 2103)]
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
        if (!BatchOutputPathUtil.TryApplySelectedFolderToActionStageRouteOutputs(
                ref m_customStageRoot,
                ref m_customRouteRoot,
                ref m_outputSettings,
                onlyIfEmpty: true))
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

        EditorGUILayout.LabelField("Action → Stage → Route", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "从选中的 ActionDataSO 批量生成 SkillStageDefinition + NormalRouteDefinition（stages[0] 绑定）。\n" +
            "Entry / Loadout / CombatGraph 请另行配置；单步生成可用 Tools → Skill 下 Action→SkillStage / Stage→NormalRoute。",
            MessageType.Info);

        EditorGUILayout.LabelField("命名（基于 Action 文件名）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "流程：Action 文件名 → 剥离前后缀得【核心名】→ 生成前后缀 → Stage_ / Route_Normal_ 文件名。",
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
        EditorGUILayout.LabelField("② 生成核心名", EditorStyles.miniBoldLabel);
        m_namePrefix = EditorGUILayout.TextField("生成前缀（加在核心名前）", m_namePrefix);
        m_nameSuffix = EditorGUILayout.TextField("生成后缀（加在核心名后）", m_nameSuffix);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("③ Stage / Route 文件名", EditorStyles.miniBoldLabel);
        m_stageFilePrefix = EditorGUILayout.TextField("Stage 文件名前缀", m_stageFilePrefix);
        m_stageSuffix = EditorGUILayout.TextField("Stage 文件名后缀", m_stageSuffix);
        m_routeFilePrefix = EditorGUILayout.TextField("Route 文件名前缀", m_routeFilePrefix);
        m_routeSuffix = EditorGUILayout.TextField("Route 文件名后缀", m_routeSuffix);

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = "ActionBatch";
        }

        if (!m_outputSettings.CreateDirectoryIfMissing)
        {
            m_outputSettings.CreateDirectoryIfMissing = true;
        }

        m_autoFillOutputFromFolder = EditorGUILayout.Toggle(
            "选中 Project 文件夹时自动填入 Stage / SkillUnit 路径",
            m_autoFillOutputFromFolder);

        BatchOutputPathUtil.DrawActionStageRouteTripleOutputSection(
            ref m_outputSettings,
            ref m_customStageRoot,
            ref m_customRouteRoot,
            SkillRoot,
            SkillRoot,
            () => m_outputSettings.BatchFolderContent = InferBatchFolderFromSelection());

        EditorGUILayout.Space(6f);
        m_writeStageIdFromFileName = EditorGUILayout.Toggle("写入 stageId（= Stage 文件名）", m_writeStageIdFromFileName);
        m_writeRouteIdFromFileName = EditorGUILayout.Toggle("写入 routeId（= Route 文件名）", m_writeRouteIdFromFileName);
        m_showOnHud = EditorGUILayout.Toggle("ShowOnHud（默认 false）", m_showOnHud);
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
        var stageDir = ResolveStageOutputDir();
        var routeDir = ResolveRouteOutputDir();
        var actions = CollectActionsFromSelection();
        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (action == null)
            {
                continue;
            }

            var core = BuildCoreName(action.name);
            var stageFileName = BuildStageFileName(core);
            var routeFileName = BuildRouteFileName(core);
            m_previews.Add(new PipelinePreview
            {
                ActionName = action.name,
                CoreName = core,
                StageFile = $"{stageDir}/{stageFileName}.asset",
                RouteFile = $"{routeDir}/{routeFileName}.asset",
            });
        }
    }

    void GenerateBatch()
    {
        var actions = CollectActionsFromSelection();
        if (actions.Count == 0)
        {
            EditorUtility.DisplayDialog("Action → Stage → Route", "未选中任何 ActionDataSO。", "OK");
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

        var stageDir = ResolveStageOutputDir();
        var routeDir = ResolveRouteOutputDir();

        if (!BatchOutputPathUtil.TryPrepareOutputDirectory(stageDir, m_outputSettings.CreateDirectoryIfMissing, out var errStage))
        {
            EditorUtility.DisplayDialog("Action → Stage → Route", errStage, "OK");
            return;
        }

        if (!BatchOutputPathUtil.TryPrepareOutputDirectory(routeDir, m_outputSettings.CreateDirectoryIfMissing, out var errRoute))
        {
            EditorUtility.DisplayDialog("Action → Stage → Route", errRoute, "OK");
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

                if (TryGenerateOne(action, stageDir, routeDir, log, out var ok))
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

        Debug.Log(
            $"[ActionStageRoute] 完成：{created} 组，跳过 {skipped} 组\nStage→{stageDir}\nRoute→{routeDir}\n{log}");
        EditorUtility.DisplayDialog(
            "Action → Stage → Route",
            $"生成/更新 {created} 组\n跳过 {skipped} 组（已存在且未勾选覆盖）\n\nStage：{stageDir}\nRoute：{routeDir}",
            "OK");
    }

    bool TryGenerateOne(ActionDataSO action, string stageDir, string routeDir, StringBuilder log, out bool createdOrUpdated)
    {
        createdOrUpdated = false;
        var core = BuildCoreName(action.name);
        var stageFileName = BuildStageFileName(core);
        var routeFileName = BuildRouteFileName(core);
        var stagePath = $"{stageDir}/{stageFileName}.asset";
        var routePath = $"{routeDir}/{routeFileName}.asset";

        var existingRoute = AssetDatabase.LoadAssetAtPath<NormalRouteDefinition>(routePath);
        if (existingRoute != null && !m_overwriteExisting)
        {
            return true;
        }

        var stage = GetOrCreateStage(action, stagePath, stageFileName, out var stageCreated);
        if (stage == null)
        {
            return false;
        }

        var routeCreated = GetOrCreateRoute(stage, routePath, routeFileName, out var routeOk);
        if (!routeOk)
        {
            return false;
        }

        if (stageCreated || routeCreated)
        {
            createdOrUpdated = true;
            log.AppendLine($"  ✓ {action.name} → {stageFileName} + {routeFileName}");
        }

        return true;
    }

    SkillStageDefinition GetOrCreateStage(ActionDataSO action, string path, string fileName, out bool createdOrUpdated)
    {
        createdOrUpdated = false;
        var existing = AssetDatabase.LoadAssetAtPath<SkillStageDefinition>(path);
        if (existing != null && !m_overwriteExisting)
        {
            return existing;
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
            createdOrUpdated = true;
        }
        else
        {
            EditorUtility.SetDirty(stage);
            createdOrUpdated = true;
        }

        return stage;
    }

    bool GetOrCreateRoute(SkillStageDefinition stage, string path, string fileName, out bool createdOrUpdated)
    {
        createdOrUpdated = false;
        var existing = AssetDatabase.LoadAssetAtPath<NormalRouteDefinition>(path);
        if (existing != null && !m_overwriteExisting)
        {
            return true;
        }

        var route = existing != null ? existing : ScriptableObject.CreateInstance<NormalRouteDefinition>();
        var so = new SerializedObject(route);

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

        var showHudProp = so.FindProperty("showOnHud");
        if (showHudProp != null)
        {
            showHudProp.boolValue = m_showOnHud;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        if (existing == null)
        {
            AssetDatabase.CreateAsset(route, path);
            createdOrUpdated = true;
        }
        else
        {
            EditorUtility.SetDirty(route);
            createdOrUpdated = true;
        }

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

    string ResolveStageOutputDir() =>
        BatchOutputPathUtil.ResolveClipOutputDirectory(
            SkillRoot, m_customStageRoot, in m_outputSettings, m_outputSettings.BatchFolderContent);

    string ResolveRouteOutputDir() =>
        BatchOutputPathUtil.ResolveClipOutputDirectory(
            SkillRoot, m_customRouteRoot, in m_outputSettings, m_outputSettings.BatchFolderContent);

    string BuildStageFileName(string core) => $"{m_stageFilePrefix}{core}{m_stageSuffix}";

    string BuildRouteFileName(string core) => $"{m_routeFilePrefix}{core}{m_routeSuffix}";

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
        m_routeFilePrefix = EditorPrefs.GetString(PrefsPrefix + "RoutePrefix", DefaultRouteFilePrefix);
        m_routeSuffix = EditorPrefs.GetString(PrefsPrefix + "RouteSuffix", string.Empty);
        m_stripFromActionPrefix = EditorPrefs.GetString(PrefsPrefix + "StripPrefix", string.Empty);
        m_stripFromActionName = EditorPrefs.GetString(PrefsPrefix + "StripSuffix", DefaultStripFromActionName);
        m_outputSettings.BatchFolderContent = EditorPrefs.GetString(PrefsPrefix + "Batch", "ActionBatch");
        m_outputSettings.UseCustomRoot = EditorPrefs.GetBool(PrefsPrefix + "CustomRoot", false);
        m_customStageRoot = EditorPrefs.GetString(PrefsPrefix + "CustomStageRoot", string.Empty);
        m_customRouteRoot = EditorPrefs.GetString(PrefsPrefix + "CustomRouteRoot", string.Empty);
        m_outputSettings.SubfolderMode = (BatchOutputPathUtil.BatchSubfolderMode)EditorPrefs.GetInt(
            PrefsPrefix + "SubfolderMode", (int)BatchOutputPathUtil.BatchSubfolderMode.NewPrefixAndBatch);
        m_outputSettings.CreateDirectoryIfMissing = EditorPrefs.GetBool(PrefsPrefix + "CreateDir", true);
        m_stripActionNamePrefix = EditorPrefs.GetBool(PrefsPrefix + "StripPrefixOn", true);
        m_stripActionNameSuffix = EditorPrefs.GetBool(PrefsPrefix + "StripSuffixOn", true);
        m_autoInferOnSelection = EditorPrefs.GetBool(PrefsPrefix + "AutoInfer", true);
        m_autoFillOutputFromFolder = EditorPrefs.GetBool(PrefsPrefix + "AutoFillFolder", true);
        m_overwriteExisting = EditorPrefs.GetBool(PrefsPrefix + "Overwrite", false);
        m_writeStageIdFromFileName = EditorPrefs.GetBool(PrefsPrefix + "StageId", true);
        m_writeRouteIdFromFileName = EditorPrefs.GetBool(PrefsPrefix + "RouteId", true);
        m_showOnHud = EditorPrefs.GetBool(PrefsPrefix + "ShowOnHud", false);
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PrefsPrefix + "NamePrefix", m_namePrefix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "NameSuffix", m_nameSuffix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "StagePrefix", m_stageFilePrefix ?? DefaultStageFilePrefix);
        EditorPrefs.SetString(PrefsPrefix + "StageSuffix", m_stageSuffix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "RoutePrefix", m_routeFilePrefix ?? DefaultRouteFilePrefix);
        EditorPrefs.SetString(PrefsPrefix + "RouteSuffix", m_routeSuffix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "StripPrefix", m_stripFromActionPrefix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "StripSuffix", m_stripFromActionName ?? DefaultStripFromActionName);
        EditorPrefs.SetString(PrefsPrefix + "Batch", m_outputSettings.BatchFolderContent ?? "ActionBatch");
        EditorPrefs.SetBool(PrefsPrefix + "CustomRoot", m_outputSettings.UseCustomRoot);
        EditorPrefs.SetString(PrefsPrefix + "CustomStageRoot", m_customStageRoot ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "CustomRouteRoot", m_customRouteRoot ?? string.Empty);
        EditorPrefs.SetInt(PrefsPrefix + "SubfolderMode", (int)m_outputSettings.SubfolderMode);
        EditorPrefs.SetBool(PrefsPrefix + "CreateDir", m_outputSettings.CreateDirectoryIfMissing);
        EditorPrefs.SetBool(PrefsPrefix + "StripPrefixOn", m_stripActionNamePrefix);
        EditorPrefs.SetBool(PrefsPrefix + "StripSuffixOn", m_stripActionNameSuffix);
        EditorPrefs.SetBool(PrefsPrefix + "AutoInfer", m_autoInferOnSelection);
        EditorPrefs.SetBool(PrefsPrefix + "AutoFillFolder", m_autoFillOutputFromFolder);
        EditorPrefs.SetBool(PrefsPrefix + "Overwrite", m_overwriteExisting);
        EditorPrefs.SetBool(PrefsPrefix + "StageId", m_writeStageIdFromFileName);
        EditorPrefs.SetBool(PrefsPrefix + "RouteId", m_writeRouteIdFromFileName);
        EditorPrefs.SetBool(PrefsPrefix + "ShowOnHud", m_showOnHud);
    }

    struct PipelinePreview
    {
        public string ActionName;
        public string CoreName;
        public string StageFile;
        public string RouteFile;
    }

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

            if (parts.Length >= 3)
            {
                return string.Join("_", parts, 0, parts.Length - 1) + "_";
            }

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
