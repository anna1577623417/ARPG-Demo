#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 188.3 W15+W16 — ActionDataSO 上 Combat Track 的简化 Inspector + Scene 实时回放。
/// <para>挂在 ActionDataSO Inspector 内：通过 partial 或 Editor 调用绘制本组件。</para>
/// <para>同时持有 Scrubber 时间，SceneView 重绘时按 Scrubber 估算 CombatObject 位置并画 Gizmo。</para>
/// </summary>
[InitializeOnLoad]
public static class CombatTrackEditor
{
    /// <summary>当前编辑中的 ActionDataSO（由 ActionDataSOInspector 注入）。</summary>
    public static ActionDataSO CurrentAction;

    /// <summary>当前 Scrubber 归一化时间。</summary>
    public static float ScrubberNormalized = 0.5f;

    /// <summary>是否启用 Scene 预览。</summary>
    public static bool ScenePreviewEnabled;

    static CombatTrackEditor()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    /// <summary>在 ActionDataSO Inspector 内调用：绘制 Combat Track 编辑面板。</summary>
    public static void DrawInspector(SerializedObject serializedAction, ActionDataSO action)
    {
        if (action == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Combat Track (188.3)", EditorStyles.boldLabel);

        var trackProp = serializedAction.FindProperty("CombatTrack");
        if (trackProp == null)
        {
            EditorGUILayout.HelpBox("ActionDataSO 没有 CombatTrack 字段（W7 未落地）。", MessageType.Warning);
            return;
        }

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

        if (ScenePreviewEnabled)
        {
            EditorGUI.BeginChangeCheck();
            ScrubberNormalized = EditorGUILayout.Slider("Scrubber (t)", ScrubberNormalized, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }
            EditorGUILayout.HelpBox(
                "选中场景中一个 Transform 作为 Spawn 锚点 → 拖动 Scrubber → Scene 中实时显示当前时间各 CombatEvent 的 Shape 位置。",
                MessageType.Info);
        }
    }

    static void OnSceneGUI(SceneView view)
    {
        if (!ScenePreviewEnabled || CurrentAction == null) return;
        if (CurrentAction.CombatTrack == null) return;

        var anchor = Selection.activeTransform;
        var anchorPos = anchor != null ? anchor.position : Vector3.zero;
        var anchorRot = anchor != null ? anchor.rotation : Quaternion.identity;

        for (var i = 0; i < CurrentAction.CombatTrack.Length; i++)
        {
            ref var ev = ref CurrentAction.CombatTrack[i];
            if (ev.Definition == null || ev.Definition.Shape == null) continue;
            // 仅在 Scrubber >= NormalizedTime 时显示（CombatObject 已生成）
            if (ScrubberNormalized < ev.NormalizedTime) continue;

            // 估算从 Spawn 起经过的"伪秒数"：归一化偏移 × 一个假设 Action 总长（1s）
            // 实际项目中可读 action.BaseDuration 但 ActionDataSO 字段名因项目而异；简化用相对偏移。
            var pseudoDt = ScrubberNormalized - ev.NormalizedTime;

            var (pos, rot) = ResolveSpawnPos(anchorPos, anchorRot, ev);
            var currentPos = SimulateMovement(ev.Definition, pos, rot, pseudoDt);

            ev.Definition.Shape.DrawGizmo(currentPos, rot, new Color(1f, 0.3f, 0.3f, 1f));
        }
    }

    static (Vector3, Quaternion) ResolveSpawnPos(Vector3 anchorPos, Quaternion anchorRot, in CombatEvent ev)
    {
        var def = ev.Definition;
        var offset = ev.OverrideSpawn ? ev.LocalOffsetOverride : def.LocalOffset;
        var euler = ev.OverrideSpawn ? ev.LocalEulerOffsetOverride : def.LocalEulerOffset;
        return (anchorPos + anchorRot * offset, anchorRot * Quaternion.Euler(euler));
    }

    static Vector3 SimulateMovement(CombatObjectDefinitionSO def, Vector3 spawn, Quaternion rot, float t)
    {
        var mov = def.Movement;
        switch (mov.Kind)
        {
            case MovementKind.Linear:
            {
                var dir = rot * Vector3.forward;
                var dist = mov.Speed * t;
                if (mov.MaxDistance > 0f) dist = Mathf.Min(dist, mov.MaxDistance);
                return spawn + dir * dist;
            }
            case MovementKind.Curve:
            {
                var x = mov.LocalOffsetXOverTime != null ? mov.LocalOffsetXOverTime.Evaluate(t) : 0f;
                var y = mov.LocalOffsetYOverTime != null ? mov.LocalOffsetYOverTime.Evaluate(t) : 0f;
                var z = mov.LocalOffsetZOverTime != null ? mov.LocalOffsetZOverTime.Evaluate(t) : 0f;
                return spawn + rot * new Vector3(x, y, z);
            }
            // Expand / Homing / Static — 位置不变（半径变化由 Shape 自身体现）
            default:
                return spawn;
        }
    }
}
#endif
