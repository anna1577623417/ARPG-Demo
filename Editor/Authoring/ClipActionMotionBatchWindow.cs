#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 <see cref="AnimationClip"/> 批量生成 <see cref="MotionProfileSO"/> + <see cref="ActionDataSO"/>（Clip → Motion → Action）。<br/>
/// UI 与 <see cref="ActionSkillStageBatchWindow"/> 完全镜像：两步式命名（① 剥离 Clip 名 → ② 生成前后缀）+ 自动识别 +
/// EditorPrefs 持久化 + 选中文件夹自动填批次。<br/>
/// 仅产出动作层资产（含位移模具）；技能 Route / Stage / Loadout 请走 Tools → SkillRoute → Stage / Route Batch 工具。
/// </summary>
public sealed class ClipActionMotionBatchWindow : EditorWindow
{
    const string PrefsPrefix = "ClipActionMotion_";

    const string ActionLibRoot = "Assets/GameMain/Scripts/4_Data/2.Actions/ActionLibrary";
    const string MotionLibRoot = "Assets/GameMain/Scripts/4_Data/3.Motion/MotionLibrary";

    const string DefaultActionSuffix = "_ActionData";
    const string DefaultMotionSuffix = "_MotionProfile";

    /// <summary>常见 Clip 文件名公共前缀（自动识别用，可被用户覆写）。</summary>
    static readonly string[] KnownClipPrefixes =
    {
        "General_Armature_",
        "Wuxia_",
        "Standing_",
        "Crouching_",
        "Running_",
    };

    /// <summary>常见 Clip 文件名公共后缀（自动识别用）。</summary>
    static readonly string[] KnownClipSuffixes =
    {
        "_Clip",
        "_Anim",
        "_AnimationClip",
    };

    [SerializeField] string m_namePrefix = string.Empty;
    [SerializeField] string m_nameSuffix = string.Empty;
    [SerializeField] string m_actionSuffix = DefaultActionSuffix;
    [SerializeField] string m_motionSuffix = DefaultMotionSuffix;
    [SerializeField] string m_stripFromClipPrefix = string.Empty;
    [SerializeField] string m_stripFromClipSuffix = string.Empty;
    [SerializeField] bool m_stripClipNamePrefix = true;
    [SerializeField] bool m_stripClipNameSuffix = false;
    [SerializeField] bool m_autoInferOnSelection = true;
    [SerializeField] bool m_createMotionProfile = true;
    [SerializeField] bool m_overwriteExisting;
    [SerializeField] bool m_writeAssetIdsFromFileName;
    [SerializeField] BatchOutputPathUtil.Settings m_outputSettings;
    [SerializeField] string m_customActionRoot = string.Empty;
    [SerializeField] string m_customMotionRoot = string.Empty;
    [SerializeField] bool m_autoFillOutputFromFolder = true;

    Vector2 m_scroll;
    readonly List<ClipPreview> m_previews = new List<ClipPreview>(32);

    [MenuItem("Tools/Action/Clip → Action + Motion Batch...", false, 4)]
    public static void OpenWindow()
    {
        var w = GetWindow<ClipActionMotionBatchWindow>(true, "Clip → Action + Motion", true);
        w.minSize = new Vector2(460f, 460f);
        w.LoadPrefs();
        w.RebuildPreview();
    }

    [MenuItem("Assets/GameMain/Generate Action + Motion From Clips", false, 2100)]
    public static void GenerateFromProjectSelection()
    {
        var w = CreateInstance<ClipActionMotionBatchWindow>();
        w.LoadPrefs();
        w.m_outputSettings.BatchFolderContent = w.InferBatchFolderFromSelection();
        w.RebuildPreview();
        w.GenerateBatch();
    }

    [MenuItem("Assets/GameMain/Generate Action + Motion From Clips", true, 2100)]
    public static bool ValidateGenerateFromSelection() => CollectClipsFromSelection().Count > 0;

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
        if (!BatchOutputPathUtil.TryGetSelectedProjectFolder(out var folder))
        {
            return;
        }

        var filledAny = false;
        if (string.IsNullOrWhiteSpace(m_customActionRoot))
        {
            m_customActionRoot = folder;
            filledAny = true;
        }

        if (string.IsNullOrWhiteSpace(m_customMotionRoot))
        {
            m_customMotionRoot = folder;
            filledAny = true;
        }

        if (filledAny)
        {
            m_outputSettings.UseCustomRoot = true;
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

        EditorGUILayout.LabelField("Clip → Motion → Action", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "仅生成动作层资产（含位移模具）。\n" +
            "技能 Stage + Route 请使用 Tools → Skill → Action → Stage → Route Batch（或分步 SkillStage / NormalRoute Batch）。",
            MessageType.Info);

        EditorGUILayout.LabelField("命名（基于 Clip 文件名）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "流程：Clip 文件名 → 剥离前后缀得【核心名】→ 再套「生成前缀/后缀」→ 再套 Action / Motion 后缀。",
            MessageType.None);

        EditorGUILayout.LabelField("① 剥离 Clip 名（可自动识别）", EditorStyles.miniBoldLabel);
        m_autoInferOnSelection = EditorGUILayout.Toggle("选中变更时自动识别前后缀", m_autoInferOnSelection);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("自动识别前后缀", GUILayout.Height(22f)))
            {
                TryAutoInferStripFromSelection(force: true);
            }

            if (GUILayout.Button("清空剥离规则", GUILayout.Width(100f)))
            {
                m_stripFromClipPrefix = string.Empty;
                m_stripFromClipSuffix = string.Empty;
                m_stripClipNamePrefix = false;
                m_stripClipNameSuffix = false;
            }
        }

        m_stripClipNamePrefix = EditorGUILayout.Toggle("剥离 Clip 名中的前缀", m_stripClipNamePrefix);
        using (new EditorGUI.DisabledScope(!m_stripClipNamePrefix))
        {
            m_stripFromClipPrefix = EditorGUILayout.TextField("剥离前缀文本", m_stripFromClipPrefix);
        }

        m_stripClipNameSuffix = EditorGUILayout.Toggle("剥离 Clip 名中的后缀", m_stripClipNameSuffix);
        using (new EditorGUI.DisabledScope(!m_stripClipNameSuffix))
        {
            m_stripFromClipSuffix = EditorGUILayout.TextField("剥离后缀文本", m_stripFromClipSuffix);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("② 生成核心名前后缀", EditorStyles.miniBoldLabel);
        m_namePrefix = EditorGUILayout.TextField("生成前缀（加在核心名前）", m_namePrefix);
        m_nameSuffix = EditorGUILayout.TextField("生成后缀（加在核心名后）", m_nameSuffix);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("③ 文件名后缀（Action / Motion 独立）", EditorStyles.miniBoldLabel);
        m_actionSuffix = EditorGUILayout.TextField("Action 后缀", m_actionSuffix);
        m_motionSuffix = EditorGUILayout.TextField("Motion 后缀", m_motionSuffix);

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = "ClipBatch";
        }

        if (!m_outputSettings.CreateDirectoryIfMissing)
        {
            m_outputSettings.CreateDirectoryIfMissing = true;
        }

        m_autoFillOutputFromFolder = EditorGUILayout.Toggle(
            "选中 Project 文件夹时自动填入（仅仍为空的 Action / Motion 路径）",
            m_autoFillOutputFromFolder);

        BatchOutputPathUtil.DrawClipDualOutputSection(
            ref m_outputSettings,
            ref m_customActionRoot,
            ref m_customMotionRoot,
            ActionLibRoot,
            MotionLibRoot,
            () => m_outputSettings.BatchFolderContent = InferBatchFolderFromSelection());

        EditorGUILayout.Space(6f);
        m_createMotionProfile = EditorGUILayout.Toggle("生成 MotionProfile", m_createMotionProfile);
        m_writeAssetIdsFromFileName = EditorGUILayout.Toggle(
            "写入资产 displayName（= 输出文件名）", m_writeAssetIdsFromFileName);
        m_overwriteExisting = EditorGUILayout.Toggle("覆盖已存在资产", m_overwriteExisting);

        if (EditorGUI.EndChangeCheck())
        {
            RebuildPreview();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"预览（{m_previews.Count} 个 Clip）", EditorStyles.boldLabel);

        if (m_previews.Count == 0)
        {
            EditorGUILayout.HelpBox("在 Project 中选中 .anim、模型子 Clip 或文件夹。", MessageType.Warning);
        }
        else
        {
            m_scroll = EditorGUILayout.BeginScrollView(m_scroll, GUILayout.MaxHeight(220f));
            for (var i = 0; i < m_previews.Count; i++)
            {
                var p = m_previews[i];
                EditorGUILayout.LabelField($"• {p.ClipName}  →  核心名「{p.CoreName}」", EditorStyles.miniLabel);
                if (m_createMotionProfile)
                {
                    EditorGUILayout.LabelField($"    {p.MotionFile}", EditorStyles.miniLabel);
                }

                EditorGUILayout.LabelField($"    {p.ActionFile}", EditorStyles.miniLabel);
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
        var actionDir = ResolveActionOutputDir();
        var motionDir = ResolveMotionOutputDir();
        var clips = CollectClipsFromSelection();
        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            var core = BuildCoreName(clip.name);
            m_previews.Add(new ClipPreview
            {
                ClipName = clip.name,
                CoreName = core,
                MotionFile = $"{motionDir}/{core}{m_motionSuffix}.asset",
                ActionFile = $"{actionDir}/{core}{m_actionSuffix}.asset",
            });
        }
    }

    string ResolveActionOutputDir() =>
        BatchOutputPathUtil.ResolveClipOutputDirectory(
            ActionLibRoot, m_customActionRoot, in m_outputSettings, m_outputSettings.BatchFolderContent);

    string ResolveMotionOutputDir() =>
        BatchOutputPathUtil.ResolveClipOutputDirectory(
            MotionLibRoot, m_customMotionRoot, in m_outputSettings, m_outputSettings.BatchFolderContent);

    void GenerateBatch()
    {
        var clips = CollectClipsFromSelection();
        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("Clip → Action + Motion", "未选中任何 AnimationClip。", "OK");
            return;
        }

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = InferBatchFolderFromSelection();
        }

        if (string.IsNullOrEmpty(m_outputSettings.BatchFolderContent))
        {
            m_outputSettings.BatchFolderContent = "ClipBatch";
        }

        var actionDir = ResolveActionOutputDir();
        var motionDir = ResolveMotionOutputDir();

        if (!BatchOutputPathUtil.TryPrepareOutputDirectory(actionDir, m_outputSettings.CreateDirectoryIfMissing, out var errAction))
        {
            EditorUtility.DisplayDialog("Clip → Action + Motion", errAction, "OK");
            return;
        }

        if (m_createMotionProfile
            && !BatchOutputPathUtil.TryPrepareOutputDirectory(motionDir, m_outputSettings.CreateDirectoryIfMissing, out var errMotion))
        {
            EditorUtility.DisplayDialog("Clip → Action + Motion", errMotion, "OK");
            return;
        }

        var created = 0;
        var skipped = 0;
        var log = new StringBuilder(256);

        try
        {
            AssetDatabase.StartAssetEditing();
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                if (TryGenerateOne(clip, actionDir, motionDir, log, out var ok))
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
            $"[ClipActionMotion] 完成：{created} 组，跳过 {skipped} 组\nAction→{actionDir}\nMotion→{motionDir}\n{log}");
        EditorUtility.DisplayDialog(
            "Clip → Action + Motion",
            $"生成/更新 {created} 组\n跳过 {skipped} 组（已存在且未勾选覆盖）\n\nAction：{actionDir}\nMotion：{motionDir}",
            "OK");
    }

    bool TryGenerateOne(AnimationClip clip, string actionDir, string motionDir, StringBuilder log, out bool createdOrUpdated)
    {
        createdOrUpdated = false;
        var core = BuildCoreName(clip.name);
        var motionFileName = $"{core}{m_motionSuffix}";
        var actionFileName = $"{core}{m_actionSuffix}";
        var motionPath = $"{motionDir}/{motionFileName}.asset";
        var actionPath = $"{actionDir}/{actionFileName}.asset";

        MotionProfileSO motion = null;
        if (m_createMotionProfile)
        {
            motion = GetOrCreateMotion(motionPath, clip, motionFileName);
            if (motion == null)
            {
                return false;
            }
        }

        var action = GetOrCreateAction(actionPath, clip, motion, actionFileName);
        if (action == null)
        {
            return false;
        }

        createdOrUpdated = true;
        log.AppendLine($"  ✓ {clip.name} → {core}{m_actionSuffix}");
        return true;
    }

    MotionProfileSO GetOrCreateMotion(string path, AnimationClip clip, string fileName)
    {
        var existing = AssetDatabase.LoadAssetAtPath<MotionProfileSO>(path);
        if (existing != null && !m_overwriteExisting)
        {
            return existing;
        }

        var profile = existing != null ? existing : ScriptableObject.CreateInstance<MotionProfileSO>();
        profile.ApplyDefaultZeroAxisDisplacement();
        profile.AnimSpeedMode = AnimSpeedMode.Constant;
        profile.SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);
        ApplyMotionBaselineFromClip(clip, profile);

        if (existing == null)
        {
            AssetDatabase.CreateAsset(profile, path);
        }
        else
        {
            EditorUtility.SetDirty(profile);
        }

        return profile;
    }

    ActionDataSO GetOrCreateAction(string path, AnimationClip clip, MotionProfileSO motion, string fileName)
    {
        var existing = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
        if (existing != null && !m_overwriteExisting)
        {
            return existing;
        }

        var action = existing != null ? existing : ScriptableObject.CreateInstance<ActionDataSO>();
        action.MainClip = clip;
        action.AnimSpeed = 1f;
        action.CrossfadeTime = 0.08f;
        action.Duration = Mathf.Max(0.05f, clip.length);
        action.Category = ActionCategory.Offense;
        action.MotionProfile = motion;

        if (existing == null)
        {
            AssetDatabase.CreateAsset(action, path);
        }
        else
        {
            EditorUtility.SetDirty(action);
        }

        return action;
    }

    static void ApplyMotionBaselineFromClip(AnimationClip clip, MotionProfileSO profile)
    {
        if (!profile.UsesAxisCurves)
        {
            profile.ApplyDefaultZeroAxisDisplacement();
        }
    }

    string BuildCoreName(string clipFileName)
    {
        var core = SanitizeAssetName(clipFileName);
        core = StripAffixesFromClipName(core);

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

    string StripAffixesFromClipName(string name)
    {
        var core = name;
        if (m_stripClipNamePrefix && !string.IsNullOrEmpty(m_stripFromClipPrefix)
            && core.StartsWith(m_stripFromClipPrefix, StringComparison.Ordinal))
        {
            core = core.Substring(m_stripFromClipPrefix.Length);
        }

        if (m_stripClipNameSuffix && !string.IsNullOrEmpty(m_stripFromClipSuffix)
            && core.EndsWith(m_stripFromClipSuffix, StringComparison.Ordinal))
        {
            core = core.Substring(0, core.Length - m_stripFromClipSuffix.Length);
        }

        return core.Trim('_', '-', ' ');
    }

    void TryAutoInferStripFromSelection(bool force = false)
    {
        var clips = CollectClipsFromSelection();
        if (clips.Count == 0)
        {
            return;
        }

        var names = new string[clips.Count];
        for (var i = 0; i < clips.Count; i++)
        {
            names[i] = clips[i].name;
        }

        if (!ClipNameAffixInferer.TryInfer(names, out var prefix, out var suffix, out var batchHint))
        {
            if (!force)
            {
                return;
            }
        }

        if (!string.IsNullOrEmpty(suffix))
        {
            m_stripFromClipSuffix = suffix;
            m_stripClipNameSuffix = true;
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            m_stripFromClipPrefix = prefix;
            m_stripClipNamePrefix = true;
        }

        if (!string.IsNullOrEmpty(batchHint)
            && (force || string.IsNullOrEmpty(m_outputSettings.BatchFolderContent)
                || m_outputSettings.BatchFolderContent == "ClipBatch"))
        {
            m_outputSettings.BatchFolderContent = BatchOutputPathUtil.SanitizeFolderToken(batchHint);
        }
    }

    string InferBatchFolderFromSelection()
    {
        var fromFolder = BatchOutputPathUtil.InferBatchNameFromSelectedFolder();
        if (!string.IsNullOrEmpty(fromFolder))
        {
            return fromFolder;
        }

        var clips = CollectClipsFromSelection();
        if (clips.Count == 0)
        {
            return "ClipBatch";
        }

        var names = new string[clips.Count];
        for (var i = 0; i < clips.Count; i++)
        {
            names[i] = clips[i].name;
        }

        if (ClipNameAffixInferer.TryInfer(names, out _, out _, out var batchHint)
            && !string.IsNullOrEmpty(batchHint))
        {
            return BatchOutputPathUtil.SanitizeFolderToken(batchHint);
        }

        if (clips.Count == 1)
        {
            return BatchOutputPathUtil.SanitizeFolderToken(
                StripAffixesFromClipName(SanitizeAssetName(clips[0].name)));
        }

        return "ClipBatch";
    }

    static string SanitizeAssetName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "UnnamedClip";
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

    static List<AnimationClip> CollectClipsFromSelection()
    {
        var list = new List<AnimationClip>(16);
        var objs = Selection.objects;
        if (objs == null)
        {
            return list;
        }

        for (var i = 0; i < objs.Length; i++)
        {
            switch (objs[i])
            {
                case AnimationClip clip when !clip.name.StartsWith("__preview__", StringComparison.Ordinal):
                    if (!list.Contains(clip))
                    {
                        list.Add(clip);
                    }

                    break;
                case DefaultAsset when AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(objs[i])):
                    AddClipsInFolder(AssetDatabase.GetAssetPath(objs[i]), list);
                    break;
                default:
                {
                    var path = AssetDatabase.GetAssetPath(objs[i]);
                    if (string.IsNullOrEmpty(path))
                    {
                        break;
                    }

                    if (objs[i] is GameObject)
                    {
                        AddClipsFromAssetPath(path, list);
                    }
                    else
                    {
                        var loaded = AssetDatabase.LoadAllAssetsAtPath(path);
                        for (var k = 0; k < loaded.Length; k++)
                        {
                            if (loaded[k] is AnimationClip c &&
                                !c.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                                !list.Contains(c))
                            {
                                list.Add(c);
                            }
                        }
                    }

                    break;
                }
            }
        }

        list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        return list;
    }

    static void AddClipsInFolder(string folder, List<AnimationClip> list)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
        for (var i = 0; i < guids.Length; i++)
        {
            AddClipsFromAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]), list);
        }
    }

    static void AddClipsFromAssetPath(string path, List<AnimationClip> list)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (var i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip &&
                !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                !list.Contains(clip))
            {
                list.Add(clip);
            }
        }
    }

    void LoadPrefs()
    {
        m_namePrefix = EditorPrefs.GetString(PrefsPrefix + "NamePrefix", string.Empty);
        m_nameSuffix = EditorPrefs.GetString(PrefsPrefix + "NameSuffix", string.Empty);
        m_outputSettings.BatchFolderContent = EditorPrefs.GetString(PrefsPrefix + "Batch", "ClipBatch");
        m_actionSuffix = EditorPrefs.GetString(PrefsPrefix + "ActionSfx", DefaultActionSuffix);
        m_motionSuffix = EditorPrefs.GetString(PrefsPrefix + "MotionSfx", DefaultMotionSuffix);
        m_stripFromClipPrefix = EditorPrefs.GetString(PrefsPrefix + "StripPrefix", string.Empty);
        m_stripFromClipSuffix = EditorPrefs.GetString(PrefsPrefix + "StripSuffix", string.Empty);
        m_stripClipNamePrefix = EditorPrefs.GetBool(PrefsPrefix + "StripPrefixOn", true);
        m_stripClipNameSuffix = EditorPrefs.GetBool(PrefsPrefix + "StripSuffixOn", false);
        m_autoInferOnSelection = EditorPrefs.GetBool(PrefsPrefix + "AutoInfer", true);
        m_createMotionProfile = EditorPrefs.GetBool(PrefsPrefix + "Motion", true);
        m_overwriteExisting = EditorPrefs.GetBool(PrefsPrefix + "Overwrite", false);
        m_writeAssetIdsFromFileName = EditorPrefs.GetBool(PrefsPrefix + "WriteIds", false);
        m_outputSettings.UseCustomRoot = EditorPrefs.GetBool(PrefsPrefix + "CustomRoot", false);
        m_customActionRoot = EditorPrefs.GetString(PrefsPrefix + "CustomActionRoot", string.Empty);
        m_customMotionRoot = EditorPrefs.GetString(PrefsPrefix + "CustomMotionRoot", string.Empty);
        m_outputSettings.SubfolderMode = (BatchOutputPathUtil.BatchSubfolderMode)EditorPrefs.GetInt(
            PrefsPrefix + "SubfolderMode", (int)BatchOutputPathUtil.BatchSubfolderMode.NewPrefixAndBatch);
        m_outputSettings.CreateDirectoryIfMissing = EditorPrefs.GetBool(PrefsPrefix + "CreateDir", true);
        m_autoFillOutputFromFolder = EditorPrefs.GetBool(PrefsPrefix + "AutoFillFolder", true);
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PrefsPrefix + "NamePrefix", m_namePrefix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "NameSuffix", m_nameSuffix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "Batch", m_outputSettings.BatchFolderContent ?? "ClipBatch");
        EditorPrefs.SetString(PrefsPrefix + "ActionSfx", m_actionSuffix ?? DefaultActionSuffix);
        EditorPrefs.SetString(PrefsPrefix + "MotionSfx", m_motionSuffix ?? DefaultMotionSuffix);
        EditorPrefs.SetString(PrefsPrefix + "StripPrefix", m_stripFromClipPrefix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "StripSuffix", m_stripFromClipSuffix ?? string.Empty);
        EditorPrefs.SetBool(PrefsPrefix + "StripPrefixOn", m_stripClipNamePrefix);
        EditorPrefs.SetBool(PrefsPrefix + "StripSuffixOn", m_stripClipNameSuffix);
        EditorPrefs.SetBool(PrefsPrefix + "AutoInfer", m_autoInferOnSelection);
        EditorPrefs.SetBool(PrefsPrefix + "Motion", m_createMotionProfile);
        EditorPrefs.SetBool(PrefsPrefix + "Overwrite", m_overwriteExisting);
        EditorPrefs.SetBool(PrefsPrefix + "WriteIds", m_writeAssetIdsFromFileName);
        EditorPrefs.SetBool(PrefsPrefix + "CustomRoot", m_outputSettings.UseCustomRoot);
        EditorPrefs.SetString(PrefsPrefix + "CustomActionRoot", m_customActionRoot ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "CustomMotionRoot", m_customMotionRoot ?? string.Empty);
        EditorPrefs.SetInt(PrefsPrefix + "SubfolderMode", (int)m_outputSettings.SubfolderMode);
        EditorPrefs.SetBool(PrefsPrefix + "CreateDir", m_outputSettings.CreateDirectoryIfMissing);
        EditorPrefs.SetBool(PrefsPrefix + "AutoFillFolder", m_autoFillOutputFromFolder);
    }

    struct ClipPreview
    {
        public string ClipName;
        public string CoreName;
        public string MotionFile;
        public string ActionFile;
    }

    /// <summary>从一组 Clip 资产名推断公共前缀/后缀（按 _ 分词边界对齐；含 KnownClipPrefixes / KnownClipSuffixes 兜底）。</summary>
    static class ClipNameAffixInferer
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

            if (names.Length >= 2 && !string.IsNullOrEmpty(prefix))
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

            for (var i = 0; i < KnownClipSuffixes.Length; i++)
            {
                if (AllEndWith(names, KnownClipSuffixes[i]))
                {
                    return KnownClipSuffixes[i];
                }
            }

            return !string.IsNullOrEmpty(lcs) && lcs.Length >= 1 && lcs[0] == '_' ? lcs : null;
        }

        static string InferPrefix(string[] names, string suffix)
        {
            if (names.Length == 1)
            {
                return InferKnownPrefix(names[0]);
            }

            var lcp = LongestCommonPrefix(names);
            lcp = TrimToTokenBoundary(lcp, keepTrailingUnderscore: true);

            // 若 LCP 短于已知前缀（如 names 都以 General_Armature_ 开头但 LCP 只到 General_），优先用 Known
            if (!string.IsNullOrEmpty(lcp))
            {
                for (var i = 0; i < KnownClipPrefixes.Length; i++)
                {
                    var kp = KnownClipPrefixes[i];
                    if (kp.Length > lcp.Length && AllStartWith(names, kp))
                    {
                        return kp;
                    }
                }
            }
            else
            {
                for (var i = 0; i < KnownClipPrefixes.Length; i++)
                {
                    if (AllStartWith(names, KnownClipPrefixes[i]))
                    {
                        return KnownClipPrefixes[i];
                    }
                }
            }

            return string.IsNullOrEmpty(lcp) ? null : lcp;
        }

        static string InferKnownPrefix(string name)
        {
            for (var i = 0; i < KnownClipPrefixes.Length; i++)
            {
                if (name.StartsWith(KnownClipPrefixes[i], StringComparison.Ordinal))
                {
                    return KnownClipPrefixes[i];
                }
            }

            // 兜底：多段名 (A_B_C → A_B_)
            var parts = name.Split('_');
            if (parts.Length >= 3)
            {
                return string.Join("_", parts, 0, parts.Length - 1) + "_";
            }

            return null;
        }

        static string InferKnownSuffix(string name)
        {
            for (var i = 0; i < KnownClipSuffixes.Length; i++)
            {
                if (name.EndsWith(KnownClipSuffixes[i], StringComparison.Ordinal))
                {
                    return KnownClipSuffixes[i];
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

        static bool AllStartWith(string[] names, string prefix)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (!names[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
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
