#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从 <see cref="AnimationClip"/> 批量生成 <see cref="MotionProfileSO"/> + <see cref="ActionDataSO"/>（Clip → Motion → Action）。<br/>
/// 技能 Route / Stage / Loadout 请用 <see cref="SkillRouteTrialKitWindow"/>（仅键位入口语义，动作后期手配）。
/// </summary>
public sealed class ClipActionMotionBatchWindow : EditorWindow
{
    const string PrefsPrefix = "ClipActionMotion_";

    const string ActionLibRoot = "Assets/GameMain/Scripts/4_Data/2.Actions/ActionLibrary";
    const string MotionLibRoot = "Assets/GameMain/Scripts/4_Data/3.Motion/MotionLibrary";

    const string DefaultActionSuffix = "_ActionData";
    const string DefaultMotionSuffix = "_MotionProfile";
    const string BatchFolderPrefix = "NEW_";

    [SerializeField] string m_namePrefix = string.Empty;
    [SerializeField] string m_nameSuffix = string.Empty;
    [SerializeField] string m_batchFolderContent = "ClipBatch";
    [SerializeField] string m_actionSuffix = DefaultActionSuffix;
    [SerializeField] string m_motionSuffix = DefaultMotionSuffix;
    [SerializeField] bool m_createMotionProfile = true;
    [SerializeField] bool m_overwriteExisting;

    Vector2 m_scroll;
    readonly List<ClipPreview> m_previews = new List<ClipPreview>(32);

    [MenuItem("Tools/Action/Clip → Action + Motion Batch...", false, 4)]
    public static void OpenWindow()
    {
        var w = GetWindow<ClipActionMotionBatchWindow>(true, "Clip → Action + Motion", true);
        w.minSize = new Vector2(460f, 420f);
        w.LoadPrefs();
        w.RebuildPreview();
    }

    [MenuItem("Assets/GameMain/Generate Action + Motion From Clips", false, 2100)]
    public static void GenerateFromProjectSelection()
    {
        var w = CreateInstance<ClipActionMotionBatchWindow>();
        w.LoadPrefs();
        w.m_batchFolderContent = w.InferBatchFolderFromSelection();
        w.RebuildPreview();
        w.GenerateBatch();
    }

    [MenuItem("Assets/GameMain/Generate Action + Motion From Clips", true, 2100)]
    public static bool ValidateGenerateFromSelection() => CollectClipsFromSelection().Count > 0;

    void OnEnable()
    {
        LoadPrefs();
        RebuildPreview();
    }

    void OnDisable() => SavePrefs();

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Clip → Motion → Action", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "仅生成动作层资产（含位移模具）。\n" +
            "技能 Route / Stage / Loadout 请使用 Tools → Skill → Route Trial Kit Generator（键位入口，动作后期绑定）。",
            MessageType.Info);

        EditorGUILayout.LabelField("命名（基于 Clip 文件名）", EditorStyles.boldLabel);
        m_namePrefix = EditorGUILayout.TextField("生成前缀", m_namePrefix);
        m_nameSuffix = EditorGUILayout.TextField("生成后缀（核心名后）", m_nameSuffix);
        m_actionSuffix = EditorGUILayout.TextField("Action 后缀", m_actionSuffix);
        m_motionSuffix = EditorGUILayout.TextField("Motion 后缀", m_motionSuffix);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("输出批次文件夹", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"Action → …/ActionLibrary/{BatchFolderPrefix}{{内容}}/\n" +
            $"Motion → …/MotionLibrary/{BatchFolderPrefix}{{内容}}/",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            m_batchFolderContent = EditorGUILayout.TextField("批次内容名", m_batchFolderContent);
            if (GUILayout.Button("从选中推断", GUILayout.Width(100f)))
            {
                m_batchFolderContent = InferBatchFolderFromSelection();
            }
        }

        EditorGUILayout.Space(6f);
        m_createMotionProfile = EditorGUILayout.Toggle("生成 MotionProfile", m_createMotionProfile);
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
            m_scroll = EditorGUILayout.BeginScrollView(m_scroll, GUILayout.MaxHeight(200f));
            for (var i = 0; i < m_previews.Count; i++)
            {
                var p = m_previews[i];
                EditorGUILayout.LabelField($"• {p.ClipName}", EditorStyles.miniLabel);
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
        var batch = SanitizeFolderToken(m_batchFolderContent);
        if (string.IsNullOrEmpty(batch))
        {
            batch = "ClipBatch";
        }

        var clips = CollectClipsFromSelection();
        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            var core = BuildCoreName(clip.name);
            var folder = $"{BatchFolderPrefix}{batch}";
            m_previews.Add(new ClipPreview
            {
                ClipName = clip.name,
                MotionFile = $"{MotionLibRoot}/{folder}/{core}{m_motionSuffix}.asset",
                ActionFile = $"{ActionLibRoot}/{folder}/{core}{m_actionSuffix}.asset",
            });
        }
    }

    void GenerateBatch()
    {
        var clips = CollectClipsFromSelection();
        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("Clip → Action + Motion", "未选中任何 AnimationClip。", "OK");
            return;
        }

        var batch = SanitizeFolderToken(m_batchFolderContent);
        if (string.IsNullOrEmpty(batch))
        {
            batch = InferBatchFolderFromSelection();
        }

        if (string.IsNullOrEmpty(batch))
        {
            batch = "ClipBatch";
        }

        var batchFolder = BatchFolderPrefix + batch;
        var actionDir = $"{ActionLibRoot}/{batchFolder}";
        var motionDir = $"{MotionLibRoot}/{batchFolder}";

        EnsureFolder(actionDir);
        if (m_createMotionProfile)
        {
            EnsureFolder(motionDir);
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
            $"[ClipActionMotion] 完成：{created} 组，跳过 {skipped} 组 → {batchFolder}\n{log}");
        EditorUtility.DisplayDialog(
            "Clip → Action + Motion",
            $"生成/更新 {created} 组\n跳过 {skipped} 组（已存在且未勾选覆盖）\n\n输出：{batchFolder}",
            "OK");
    }

    bool TryGenerateOne(AnimationClip clip, string actionDir, string motionDir, StringBuilder log, out bool createdOrUpdated)
    {
        createdOrUpdated = false;
        var core = BuildCoreName(clip.name);
        var motionPath = $"{motionDir}/{core}{m_motionSuffix}.asset";
        var actionPath = $"{actionDir}/{core}{m_actionSuffix}.asset";

        MotionProfileSO motion = null;
        if (m_createMotionProfile)
        {
            motion = GetOrCreateMotion(motionPath, clip);
            if (motion == null)
            {
                return false;
            }
        }

        var action = GetOrCreateAction(actionPath, clip, motion);
        if (action == null)
        {
            return false;
        }

        createdOrUpdated = true;
        log.AppendLine($"  ✓ {clip.name} → {core}{m_actionSuffix}");
        return true;
    }

    MotionProfileSO GetOrCreateMotion(string path, AnimationClip clip)
    {
        var existing = AssetDatabase.LoadAssetAtPath<MotionProfileSO>(path);
        if (existing != null && !m_overwriteExisting)
        {
            return existing;
        }

        var profile = existing != null ? existing : ScriptableObject.CreateInstance<MotionProfileSO>();
        profile.DisplacementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        profile.SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);
        profile.AnimSpeedMode = AnimSpeedMode.Constant;
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

    ActionDataSO GetOrCreateAction(string path, AnimationClip clip, MotionProfileSO motion)
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
        var wall = Mathf.Max(0.05f, clip.length);
        profile.BurstDurationSeconds = wall;
        if (profile.BaseDistance < 0.001f)
        {
            profile.BaseDistance = 4f;
        }

        profile.ReferenceSpeed = Mathf.Max(0.1f, profile.BaseDistance / wall);
        profile.UsePlanarVelocityShape = false;
        profile.LegacyConstantPlanarSpeed = profile.ReferenceSpeed;
    }

    string BuildCoreName(string clipFileName)
    {
        var core = SanitizeAssetName(clipFileName);
        if (!string.IsNullOrEmpty(m_namePrefix))
        {
            core = m_namePrefix + core;
        }

        if (!string.IsNullOrEmpty(m_nameSuffix))
        {
            core += m_nameSuffix;
        }

        return core;
    }

    string InferBatchFolderFromSelection()
    {
        var clips = CollectClipsFromSelection();
        if (clips.Count == 0)
        {
            return "ClipBatch";
        }

        if (clips.Count == 1)
        {
            return SanitizeFolderToken(clips[0].name);
        }

        var prefix = clips[0].name;
        for (var i = 1; i < clips.Count; i++)
        {
            prefix = CommonPrefix(prefix, clips[i].name);
        }

        prefix = prefix.TrimEnd('_', '-', ' ');
        return string.IsNullOrEmpty(prefix) ? "ClipBatch" : SanitizeFolderToken(prefix);
    }

    static string CommonPrefix(string a, string b)
    {
        var len = Mathf.Min(a.Length, b.Length);
        var i = 0;
        while (i < len && a[i] == b[i])
        {
            i++;
        }

        return a.Substring(0, i);
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

    static string SanitizeFolderToken(string raw)
    {
        var s = SanitizeAssetName(raw);
        return string.IsNullOrEmpty(s) ? "ClipBatch" : s;
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

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parts = folderPath.Replace('\\', '/').Split('/');
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

    void LoadPrefs()
    {
        m_namePrefix = EditorPrefs.GetString(PrefsPrefix + "NamePrefix", string.Empty);
        m_nameSuffix = EditorPrefs.GetString(PrefsPrefix + "NameSuffix", string.Empty);
        m_batchFolderContent = EditorPrefs.GetString(PrefsPrefix + "Batch", "ClipBatch");
        m_actionSuffix = EditorPrefs.GetString(PrefsPrefix + "ActionSfx", DefaultActionSuffix);
        m_motionSuffix = EditorPrefs.GetString(PrefsPrefix + "MotionSfx", DefaultMotionSuffix);
        m_createMotionProfile = EditorPrefs.GetBool(PrefsPrefix + "Motion", true);
        m_overwriteExisting = EditorPrefs.GetBool(PrefsPrefix + "Overwrite", false);
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PrefsPrefix + "NamePrefix", m_namePrefix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "NameSuffix", m_nameSuffix ?? string.Empty);
        EditorPrefs.SetString(PrefsPrefix + "Batch", m_batchFolderContent ?? "ClipBatch");
        EditorPrefs.SetString(PrefsPrefix + "ActionSfx", m_actionSuffix ?? DefaultActionSuffix);
        EditorPrefs.SetString(PrefsPrefix + "MotionSfx", m_motionSuffix ?? DefaultMotionSuffix);
        EditorPrefs.SetBool(PrefsPrefix + "Motion", m_createMotionProfile);
        EditorPrefs.SetBool(PrefsPrefix + "Overwrite", m_overwriteExisting);
    }

    struct ClipPreview
    {
        public string ClipName;
        public string MotionFile;
        public string ActionFile;
    }
}
#endif
