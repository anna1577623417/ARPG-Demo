#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 188.3 W15+W16 — ActionDataSO 上 Combat Track Inspector + Scene 预览。
/// 214.3 — playhead 与 Action 时间轴编辑器同步；预览走 <see cref="CombatHitPreviewResolver"/>。
/// </summary>
[InitializeOnLoad]
public static class CombatTrackEditor
{
    public static ActionDataSO CurrentAction;
    public static bool ScenePreviewEnabled;
    public static bool FollowTimelinePlayhead = true;

    static float s_fallbackScrubber = 0.5f;

    /// <summary>归一化预览时间；优先 Action 时间轴编辑器 playhead。</summary>
    public static float ScrubberNormalized
    {
        get
        {
            if (FollowTimelinePlayhead
                && ActionDataTimelineEditor.ActiveInstance != null
                && ActionDataTimelineEditor.ActiveInstance.HasBoundAction)
            {
                return ActionDataTimelineEditor.ActiveInstance.PreviewNormalizedTime;
            }

            return s_fallbackScrubber;
        }
        set => s_fallbackScrubber = Mathf.Clamp01(value);
    }

    static CombatTrackEditor()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public static void DrawInspector(SerializedObject serializedAction, ActionDataSO action)
    {
        if (action == null)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Combat Track (214.3)", EditorStyles.boldLabel);

        var trackProp = serializedAction.FindProperty("CombatTrack");
        if (trackProp == null)
        {
            EditorGUILayout.HelpBox("ActionDataSO 没有 CombatTrack 字段。", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Spawn Override（可选）", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(trackProp, includeChildren: true);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            ScenePreviewEnabled = EditorGUILayout.Toggle("Scene Preview", ScenePreviewEnabled);
            CurrentAction = ScenePreviewEnabled ? action : null;
            if (GUILayout.Button("Clear Preview"))
            {
                ScenePreviewEnabled = false;
                CurrentAction = null;
                SceneView.RepaintAll();
            }
        }

        FollowTimelinePlayhead = EditorGUILayout.Toggle("跟随 TL Playhead", FollowTimelinePlayhead);
        using (new EditorGUI.DisabledScope(FollowTimelinePlayhead))
        {
            EditorGUI.BeginChangeCheck();
            s_fallbackScrubber = EditorGUILayout.Slider("Scrubber (t)", s_fallbackScrubber, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }
        }

        if (GUILayout.Button("打开 Action 时间轴编辑器"))
        {
            ActionDataTimelineEditor.Open(action);
        }

        if (action.CombatTrack == null || action.CombatTrack.Length == 0)
        {
            EditorGUILayout.HelpBox("CombatTrack 为空 → 运行时不会 Spawn 攻击盒。", MessageType.Warning);
        }

        EditorGUILayout.HelpBox(
            "选中场景角色作锚点；开启 Scene Preview 后拖动 TL playhead 或 Scrubber 预览真实 HitShape。",
            MessageType.Info);
    }

    static void OnSceneGUI(SceneView view)
    {
        if (!ScenePreviewEnabled || CurrentAction == null)
        {
            return;
        }

        var anchor = ResolveAnchor();
        if (anchor == null)
        {
            return;
        }

        CombatHitPreviewResolver.DrawAllForAction(
            CurrentAction,
            ScrubberNormalized,
            anchor,
            drawTrajectory: true,
            drawExpandRings: true);
        CombatSceneDrawSourceProbe.RegisterPrimaryDraw(
            CombatSceneDrawSourceProbe.SourceCombatTrackEditor,
            anchor.position,
            $"action={CurrentAction.name} t={ScrubberNormalized:F3}");
    }

    static Transform ResolveAnchor()
    {
        return ActionTimelineEditorUI.ResolvePreviewAnchor(
            ActionDataTimelineEditor.ActiveInstance?.GizmoAnchorOverride);
    }
}
#endif
