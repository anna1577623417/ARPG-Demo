#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionDataSO))]
public sealed partial class ActionDataInspector : Editor
{
    static bool s_timeFoldout;
    static bool s_extractFoldout = true;
    static bool s_timelineFoldout = true;

    static readonly string[] HiddenInDefaultInspector =
    {
        "m_Script",
        nameof(ActionDataSO.Duration),
        nameof(ActionDataSO.SegmentStart),
        nameof(ActionDataSO.SegmentEnd),
        nameof(ActionDataSO.AnimSpeed),
        nameof(ActionDataSO.ClipAnimSpeedMode),
        nameof(ActionDataSO.DurationStatScaling),
        nameof(ActionDataSO.PrincipalAxis),
        nameof(ActionDataSO.ReferenceMotionSpeed),
        nameof(ActionDataSO.BakeMinAnimSpeed),
        nameof(ActionDataSO.BakeMaxAnimSpeed),
        nameof(ActionDataSO.Windows),
        nameof(ActionDataSO.TeleportTriggers),
        nameof(ActionDataSO.TimelineMarkers),
        nameof(ActionDataSO.MotionProfile),
        nameof(ActionDataSO.MotionDriverMode),
        nameof(ActionDataSO.UseClipRootMotion),
        nameof(ActionDataSO.EnableStopFeature),
        nameof(ActionDataSO.StopStrategy),
        nameof(ActionDataSO.InheritPhysics),
        nameof(ActionDataSO.ReferenceDuration),
        nameof(ActionDataSO.StopPresentation),
        // 198.x — Tail Segment / TapWindowSec 字段已删除
        // 198.x — ExitVelocityPolicy 字段已删除（167.1 旧机制完全退役）
        nameof(ActionDataSO.LinearDecayDuration),
        nameof(ActionDataSO.ExpDecayHalfLife),
        nameof(ActionDataSO.FixedDecelDuration),
        nameof(ActionDataSO.FixedDecelDistance),
        nameof(ActionDataSO.DurationPerUnitSpeed),
        nameof(ActionDataSO.StepValues),
        nameof(ActionDataSO.StepIntervalSec),
        nameof(ActionDataSO.SlideMaxResidualSpeed),
        nameof(ActionDataSO.IsLocomotionRecovery),
        nameof(ActionDataSO.TransitionType),
        nameof(ActionDataSO.OverrideGrammar),
        nameof(ActionDataSO.GrammarOverride),
        nameof(ActionDataSO.CombatTrack),
        nameof(ActionDataSO.ContactEvents),
        // 198.3 — EnableRotationInput 由顶部 Timeline 快速卡片接管渲染，避免重复
        nameof(ActionDataSO.EnableRotationInput),
        // 227.5.1 — 连续 Locomotion 接管开关单独绘制。
        nameof(ActionDataSO.IsContinuousLocomotion),
    };

    public override void OnInspectorGUI()
    {
        var action = (ActionDataSO)target;
        if (action == null)
        {
            return;
        }

        serializedObject.Update();

        // 198.3 — Timeline 快速入口上移到 Inspector 顶部（最常用，应该最容易访问）
        DrawTimelineQuickAccessTop(action);
        DrawMotionAuthoritySection(action);
        if (ShouldDrawMotionPass(action))
        {
            DrawMotionPassSection(action);
        }
        DrawPropertiesExcluding(serializedObject, HiddenInDefaultInspector);
        DrawGrammarSection(action);
        DrawStopAuthoringSection(action);
        DrawTimelineSection(action);
        DrawTimeAuthoritySection(action);
        DrawCombatTrackSection(action);
        serializedObject.ApplyModifiedProperties();
    }

    static bool ShouldDrawMotionPass(ActionDataSO action) =>
        action != null
        && (action.MotionDriverMode == ActionMotionDriverMode.MotionProfile
            || (action.MotionDriverMode == ActionMotionDriverMode.LegacyAuto
                && (action.MotionProfile != null
                    || action.IntentCategory != ActionIntentCategory.Locomotion)));

    /// <summary>198.3 — Inspector 顶部 Timeline 快速入口（紧跟 Script / CrossFade 上方常用字段）。</summary>
    void DrawTimelineQuickAccessTop(ActionDataSO action)
    {
        EditorGUILayout.Space(4f);

        var winCount = action.Windows != null ? action.Windows.Count : 0;
        var rotCount = CountRotationInputWindows(action);
        var tpCount = action.TeleportTriggers != null ? action.TeleportTriggers.Count : 0;
        var mkCount = action.TimelineMarkers != null ? action.TimelineMarkers.Count : 0;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // 标题独占一行
            EditorGUILayout.LabelField("🎬 Action Timeline", EditorStyles.boldLabel);

            // 计数信息独占一行（4 项数据竖直分布）
            EditorGUILayout.LabelField($"Windows: {winCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Rotation Input: {rotCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Teleport: {tpCount}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Marker: {mkCount}", EditorStyles.miniLabel);

            EditorGUILayout.Space(2f);

            // 打开按钮独占一行
            if (GUILayout.Button("打开 Action 时间轴编辑器", GUILayout.Height(26f)))
            {
                ActionDataTimelineEditor.Open(action);
            }

            // 198.3 — Rotation Input 总开关：每个 UI 元素独占一行，避免左右拉伸
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Rotation Input (198.3)", EditorStyles.boldLabel);

            var enableProp = serializedObject.FindProperty(nameof(ActionDataSO.EnableRotationInput));
            if (enableProp != null)
            {
                EditorGUILayout.PropertyField(enableProp,
                    new GUIContent("Enable Rotation Input",
                        "总开关。未勾选时，即使 Timeline 里配了 Rotation Input 窗口，玩家方向输入也完全无效。"));

                if (!enableProp.boolValue && rotCount > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"⚠ 时间轴里已编辑 {rotCount} 个 Rotation Input 窗口,但总开关关闭中,运行时全部不生效。",
                        MessageType.Warning);
                }
                else if (enableProp.boolValue && rotCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "总开关已勾选,但 Timeline 里还没编辑 Rotation Input 窗口 → 等同关闭(双保险)。",
                        MessageType.Info);
                }
                else if (enableProp.boolValue && rotCount > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"✓ Rotation Input 已启用,{rotCount} 个窗口生效。",
                        MessageType.None);
                }
            }
        }
        EditorGUILayout.Space(2f);
    }

    static int CountRotationInputWindows(ActionDataSO action)
    {
        if (action.Windows == null) return 0;
        var n = 0;
        for (var i = 0; i < action.Windows.Count; i++)
        {
            var w = action.Windows[i];
            if (w.AllowFacingInput || w.AllowMoveInput) n++;
        }
        return n;
    }

    void DrawTimelineSection(ActionDataSO action)
    {
        EditorGUILayout.Space(8f);
        s_timelineFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_timelineFoldout,
            "Timeline（139.2 · Interrupt + Combat）");

        if (!s_timelineFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        var winCount = action.Windows != null ? action.Windows.Count : 0;
        var tpCount = action.TeleportTriggers != null ? action.TeleportTriggers.Count : 0;
        var mkCount = action.TimelineMarkers != null ? action.TimelineMarkers.Count : 0;
        EditorGUILayout.LabelField("Windows", winCount.ToString());
        EditorGUILayout.LabelField("Teleport Triggers", tpCount.ToString());
        EditorGUILayout.LabelField("Timeline Markers", mkCount.ToString());

        EditorGUILayout.HelpBox(
            "推荐用「打开 Action 时间轴编辑器」：左右分栏，属性不再与时间轴纵向堆叠。",
            MessageType.None);

        if (GUILayout.Button("打开 Action 时间轴编辑器", GUILayout.Height(26f)))
        {
            ActionDataTimelineEditor.Open(action);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>214.3 — CombatTrack 独立于 Timeline Foldout，避免嵌套 Foldout Header 报错。</summary>
    void DrawCombatTrackSection(ActionDataSO action)
    {
        EditorGUILayout.Space(4f);
        CombatTrackEditor.DrawInspector(serializedObject, action);
    }

    void DrawMotionPassSection(ActionDataSO action)
    {
        EditorGUILayout.Space(4f);
        s_extractFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_extractFoldout,
            "Motion Extract → MotionProfile");
        if (!s_extractFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        // 173.6 — MotionProfile 配置入口移入本区（避免散落在隐藏字段里）
        var profileProp = serializedObject.FindProperty(nameof(ActionDataSO.MotionProfile));
        if (profileProp != null)
        {
            EditorGUILayout.PropertyField(profileProp, new GUIContent("MotionProfile"));
        }

        DrawMotionProfileReadOnlyPreview(action);

        EditorGUILayout.Space(2f);

        var canPass = action.MainClip != null && action.MotionProfile != null;
        using (new EditorGUI.DisabledScope(!canPass))
        {
            if (GUILayout.Button("传递 MainClip → MotionProfile", GUILayout.Height(22f)))
            {
                PassMainClipToMotionProfile(action);
            }

            if (GUILayout.Button("按 Segment 提取", GUILayout.Height(22f)))
            {
                ExtractSegmentMotionProfile(action);
            }
        }

        if (action.MainClip == null)
        {
            EditorGUILayout.HelpBox("需要 MainClip。", MessageType.Info);
        }
        else if (action.MotionProfile == null)
        {
            EditorGUILayout.HelpBox("需要 MotionProfile 引用。", MessageType.Warning);
        }
        else if (canPass)
        {
            var bound = action.MotionProfile.SourceClip == action.MainClip;
            EditorGUILayout.HelpBox(
                bound
                    ? $"✓ {action.MotionProfile.name}.SourceClip = {action.MainClip.name} · Seg {ActionTimeAuthority.ResolveSegmentStart(action):F2}~{ActionTimeAuthority.ResolveSegmentEnd(action):F2}"
                    : "MainClip 尚未写入 MotionProfile.SourceClip — 点击「传递」。",
                bound ? MessageType.None : MessageType.Warning);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void PassMainClipToMotionProfile(ActionDataSO action)
    {
        var profile = action.MotionProfile;
        if (profile == null || action.MainClip == null)
        {
            return;
        }

        Undo.RecordObject(profile, "Assign Source Clip From Action");
        profile.SourceClip = action.MainClip;
        EditorUtility.SetDirty(profile);
        EditorGUIUtility.PingObject(profile);

        Debug.Log(
            $"[MotionXYZ] Action '{action.name}' → MotionProfile '{profile.name}'.SourceClip = '{action.MainClip.name}'");
    }

    static void ExtractSegmentMotionProfile(ActionDataSO action)
    {
        var profile = action.MotionProfile;
        if (profile == null || action.MainClip == null)
        {
            return;
        }

        var rig = Selection.activeGameObject;
        if (rig == null)
        {
            EditorUtility.DisplayDialog(
                "Segment Extract",
                "请在 Hierarchy 选中预览 Rig（如 Armature），再按 Segment 提取位移。",
                "OK");
            return;
        }

        Undo.RecordObject(profile, "Extract Motion From Action Segment");
        profile.SourceClip = action.MainClip;
        var opt = ClipMotionExtractor.OptionsFromProfile(profile, ClipMotionExtractor.Options.Default);
        if (ClipMotionExtractor.ExtractIntoForAction(action, rig, profile, opt))
        {
            EditorUtility.SetDirty(profile);
            EditorGUIUtility.PingObject(profile);
        }
    }

    void DrawProperty(string propertyName)
    {
        var prop = serializedObject.FindProperty(propertyName);
        if (prop != null)
        {
            EditorGUILayout.PropertyField(prop, includeChildren: true);
        }
    }
}
#endif
