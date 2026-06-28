#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataInspector
{
    static float s_editorDurationScalePreview = 1f;
    static bool s_retimingFoldout = true;

    void DrawTimeAuthoritySection(ActionDataSO action)
    {
        EditorGUILayout.Space(6f);
        s_timeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_timeFoldout,
            "Time Authority（172.1 · Duration + Segment）");
        if (!s_timeFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        ActionIdleCategoryMigration.WarnIfUnmigratedIdle(action);

        var inheritPhysicsDuration = action.EnableStopFeature
            && action.StopStrategy == StopStrategy.InheritPhysics;
        using (new EditorGUI.DisabledScope(inheritPhysicsDuration))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ActionDataSO.Duration)));
        }

        if (inheritPhysicsDuration)
        {
            EditorGUILayout.HelpBox(
                "InheritPhysics：运行时 Duration 由 speed→Duration 插值覆盖；Action 上 Duration 仍作动画基准/ReferenceDuration 来源。",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(action.MainClip == null))
        {
            if (GUILayout.Button("Segment 时长 → Duration", GUILayout.Height(20f)))
            {
                Undo.RecordObject(action, "Segment Length To Duration");
                action.Duration = ActionTimeAuthority.ComputeSuggestedDurationFromSegment(action);
                EditorUtility.SetDirty(action);
                serializedObject.Update();
            }
        }

        if (action.MainClip != null)
        {
            var segSec = ActionTimeAuthority.ResolveSegmentWallSeconds(action);
            EditorGUILayout.LabelField("Clip", $"{action.MainClip.length:F3}s");
            EditorGUILayout.LabelField("Seg", $"{segSec:F3}s");
        }

        DrawSegmentRangeEditor(action);

        var binding = ActionTimeAuthorityBinding.Create(serializedObject);
        var inheritPhysicsAnim = action.EnableStopFeature
            && action.StopStrategy == StopStrategy.InheritPhysics;
        using (new EditorGUI.DisabledScope(inheritPhysicsAnim))
        {
            binding?.DrawInspectorAnimSpeedBlock();
        }

        if (inheritPhysicsAnim)
        {
            EditorGUILayout.HelpBox(
                "InheritPhysics：静态 AnimSpeed 被 ReferenceDuration/runtimeDuration 覆盖；SpeedOverTime 曲线倍率仍生效。",
                MessageType.Info);
        }

        var isAutoFit = binding != null && binding.IsAutoFit;

        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ActionDataSO.DurationStatScaling)));
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ActionDataSO.PrincipalAxis)));

        EditorGUILayout.Space(4f);
        DrawSegmentShortcutToolbar(action);
        DrawDurationSyncToolbar(action);

        using (new EditorGUI.DisabledScope(isAutoFit || action.MainClip == null))
        {
            if (GUILayout.Button("按 Duration 匹配 Clip 速率", GUILayout.Height(24f)))
            {
                Undo.RecordObject(action, "Match Clip Speed To Duration");
                action.AnimSpeed = ActionTimeAuthority.ComputeAnimSpeed(action);
                EditorUtility.SetDirty(action);
                serializedObject.Update();
                LogMatchClipSpeed(action);
            }
        }

        using (new EditorGUI.DisabledScope(action.MotionProfile == null))
        {
            if (GUILayout.Button("motionProfile 主轴位移", GUILayout.Height(24f)))
            {
                RefreshPrincipalDisplacementFromMotionProfile(action);
                Repaint();
            }
        }

        DrawMotionRetimingSection(action);

        DrawTimeAuthorityPreview(action);

        EditorGUILayout.HelpBox(
            "三条时间轴：Action nt 0~1 | Motion t=nt（100%位移）| Clip 由 AnimSpeed 推演（Free 可见后摇）。\n" +
            "AutoFitDuration = Clip×Segment÷Duration；Free = 手填 AnimSpeed，MotionProfile SpeedOverTime 仅 Free 叠加。\n" +
            "Motion Retiming：Duration = 主轴位移 ÷ ReferenceSpeed；Apply 后写入 Duration + AnimSpeed 并切 Free。",
            MessageType.None);

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawSegmentShortcutToolbar(ActionDataSO action)
    {
        if (GUILayout.Button("Full 0~1"))
        {
            SetSegmentRange(action, 0f, 1f);
        }

        if (GUILayout.Button("Trim End 0.8"))
        {
            SetSegmentRange(action, 0f, 0.8f);
        }

        if (GUILayout.Button("Mid 0.3~0.6"))
        {
            SetSegmentRange(action, 0.3f, 0.6f);
        }
    }

    void DrawDurationSyncToolbar(ActionDataSO action)
    {
        using (new EditorGUI.DisabledScope(action.MainClip == null))
        {
            if (GUILayout.Button("Segment → Duration"))
            {
                Undo.RecordObject(action, "Segment To Duration");
                action.Duration = ActionTimeAuthority.ComputeSuggestedDurationFromSegment(action);
                EditorUtility.SetDirty(action);
                serializedObject.Update();
            }

            if (GUILayout.Button("Duration → SegmentEnd"))
            {
                Undo.RecordObject(action, "Duration To SegmentEnd");
                action.SegmentEnd = ActionTimeAuthority.ComputeSuggestedSegmentEndFromDuration(action);
                ActionTimeAuthority.NormalizeSegmentRange(action);
                EditorUtility.SetDirty(action);
                serializedObject.Update();
            }

            if (GUILayout.Button("From AnimSpeed → End"))
            {
                Undo.RecordObject(action, "Infer SegmentEnd From AnimSpeed");
                action.SegmentEnd = ActionTimeAuthority.InferSegmentEndFromAnimSpeed(action);
                ActionTimeAuthority.NormalizeSegmentRange(action);
                EditorUtility.SetDirty(action);
                serializedObject.Update();
            }
        }
    }

    void DrawMotionRetimingSection(ActionDataSO action)
    {
        EditorGUILayout.Space(6f);
        s_retimingFoldout = EditorGUILayout.Foldout(
            s_retimingFoldout,
            "Motion Retiming（离线 · Reference Speed）",
            true);
        if (!s_retimingFoldout)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(nameof(ActionDataSO.ReferenceMotionSpeed)),
            new GUIContent("Reference Speed", "动作类别参考推进速度（m/s），如普通攻击 5、突刺 7。"));

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(nameof(ActionDataSO.BakeMinAnimSpeed)),
            new GUIContent("Min AnimSpeed"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty(nameof(ActionDataSO.BakeMaxAnimSpeed)),
            new GUIContent("Max AnimSpeed"));

        var retiming = ActionTimeAuthority.ComputeMotionRetiming(
            action,
            action.ReferenceMotionSpeed,
            action.BakeMinAnimSpeed,
            action.BakeMaxAnimSpeed);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Main Distance", retiming.MainDistanceMeters);
            EditorGUILayout.FloatField("Calculated Duration", retiming.Duration);
            EditorGUILayout.FloatField("Calculated AnimSpeed", retiming.AnimSpeed);
        }

        if (!retiming.IsValid && !string.IsNullOrEmpty(retiming.Warning))
        {
            EditorGUILayout.HelpBox(retiming.Warning, MessageType.Info);
        }
        else if (retiming.AnimSpeedWasClamped)
        {
            EditorGUILayout.HelpBox(
                $"⚠ {retiming.Warning}\n未 Clamp AnimSpeed={retiming.UnclampedAnimSpeed:F3}",
                MessageType.Warning);
        }

        if (GUILayout.Button("Recalculate", GUILayout.Height(22f)))
        {
            Repaint();
        }

        using (new EditorGUI.DisabledScope(!retiming.IsValid))
        {
            if (GUILayout.Button("Apply Duration + AnimSpeed", GUILayout.Height(22f)))
            {
                Undo.RecordObject(action, "Apply Motion Retiming");
                ActionTimeAuthority.ApplyMotionRetiming(action, retiming);
                EditorUtility.SetDirty(action);
                serializedObject.Update();
                Debug.Log(
                    $"[ActionTime][Retiming] '{action.name}' dist={retiming.MainDistanceMeters:F3}m " +
                    $"ref={retiming.ReferenceSpeed:F2}m/s → Duration={retiming.Duration:F4}s " +
                    $"AnimSpeed={retiming.AnimSpeed:F3} axis={action.PrincipalAxis}");
                Repaint();
            }
        }

        EditorGUI.indentLevel--;
    }

    void DrawTimeAuthorityPreview(ActionDataSO action)
    {
        var authored = ActionTimeAuthority.ResolveAuthoredLogicDurationSeconds(action);
        var previewScale = action.DurationStatScaling == MotionScaleType.None
            ? 1f
            : s_editorDurationScalePreview;
        var effective = ActionTimeAuthority.ResolveLogicDurationSeconds(action, null, previewScale);
        var motionDist = ActionTimeAuthority.MeasurePrincipalAxisDisplacementMeters(action);
        var computedAnimSpeed = ActionTimeAuthority.ComputeAnimSpeed(action);
        var segStart = ActionTimeAuthority.ResolveSegmentStart(action);
        var segEnd = ActionTimeAuthority.ResolveSegmentEnd(action);
        var segLen = ActionTimeAuthority.ResolveSegmentLength(action);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
        s_editorDurationScalePreview = EditorGUILayout.Slider(
            "Editor 属性倍率预览",
            s_editorDurationScalePreview,
            0.25f,
            3f);

        EditorGUILayout.LabelField("Authored Logic Duration", $"{authored:F4}s");
        EditorGUILayout.LabelField("Effective Logic Duration", $"{effective:F4}s");
        EditorGUILayout.LabelField("Segment", $"{segStart:P0} ~ {segEnd:P0}  (len={segLen:P1})");
        EditorGUILayout.LabelField(
            "Clip @ Action nt=1",
            $"{ActionTimeAuthority.MapActionTimeToClipNormalized(1f, action):P1}");
        EditorGUILayout.LabelField("Computed AnimSpeed", $"{computedAnimSpeed:F3}");
        var clipDone = ActionAnimSpeedAuthority.ResolveClipDoneNormalizedTime(action);
        EditorGUILayout.LabelField("Clip Done @ Action t", $"{clipDone:F3}");

        using (new EditorGUI.DisabledScope(action.MotionProfile == null))
        {
            EditorGUILayout.LabelField(
                $"Motion 主轴 {action.PrincipalAxis} 位移 (nt=1, 100%)",
                action.MotionProfile != null ? $"{motionDist:F3} m" : "—");
        }

        if (action.MainClip != null)
        {
            EditorGUILayout.LabelField("Clip Length", $"{action.MainClip.length:F4}s");
            EditorGUILayout.LabelField("Suggested Duration (Segment→Duration)", $"{ActionTimeAuthority.ComputeSuggestedDurationFromSegment(action):F4}s");
            EditorGUILayout.LabelField("Suggested SegmentEnd (Duration→End)", $"{ActionTimeAuthority.ComputeSuggestedSegmentEndFromDuration(action):F3}");
        }

        if (action.MotionProfile != null)
        {
            var motionDur = MotionDurationResolver.Resolve(action);
            EditorGUILayout.LabelField("Runtime Motion Duration", $"{motionDur:F4}s");
        }
    }

    void DrawSegmentRangeEditor(ActionDataSO action)
    {
        var segStartProp = serializedObject.FindProperty(nameof(ActionDataSO.SegmentStart));
        var segEndProp = serializedObject.FindProperty(nameof(ActionDataSO.SegmentEnd));
        var start = segStartProp.floatValue;
        var end = segEndProp.floatValue;
        EditorGUILayout.MinMaxSlider(
            new GUIContent("Clip Segment", "MainClip 归一化片段；Timeline 仍编辑 Action 0~1"),
            ref start,
            ref end,
            0f,
            1f);
        if (!Mathf.Approximately(start, segStartProp.floatValue)
            || !Mathf.Approximately(end, segEndProp.floatValue))
        {
            segStartProp.floatValue = start;
            segEndProp.floatValue = Mathf.Max(end, start + 0.001f);
        }

        EditorGUILayout.PropertyField(segStartProp, new GUIContent("Segment Start"));
        EditorGUILayout.PropertyField(segEndProp, new GUIContent("Segment End"));
    }

    static void SetSegmentRange(ActionDataSO action, float start, float end)
    {
        Undo.RecordObject(action, "Set Clip Segment");
        action.SegmentStart = start;
        action.SegmentEnd = end;
        ActionTimeAuthority.NormalizeSegmentRange(action);
        EditorUtility.SetDirty(action);
    }

    static void LogMatchClipSpeed(ActionDataSO action)
    {
        if (action == null)
        {
            return;
        }

        var dur = ActionTimeAuthority.ResolveAuthoredLogicDurationSeconds(action);
        var segLen = ActionTimeAuthority.ResolveSegmentLength(action);
        var clipProgress = ActionTimeAuthority.MapActionTimeToClipNormalized(1f, action);
        var motionEnd = ActionTimeAuthority.MeasurePrincipalAxisDisplacementAtActionEnd(action);

        Debug.Log(
            $"[ActionTime][ClipMatch] '{action.name}' Duration={dur:F3}s SegmentLen={segLen:F2} " +
            $"AnimSpeed={action.AnimSpeed:F3} → Clip@{clipProgress:P0} | Motion {action.PrincipalAxis}={motionEnd:F2}m");
    }

    static void RefreshPrincipalDisplacementFromMotionProfile(ActionDataSO action)
    {
        var profile = action.MotionProfile;
        if (profile == null)
        {
            return;
        }

        var dist = ActionTimeAuthority.MeasurePrincipalAxisDisplacementMeters(action);
        EditorGUIUtility.PingObject(profile);
        var seg = ActionTimeAuthority.ResolveSegmentLength(action);
        Debug.Log(
            $"[ActionTime] '{action.name}' ← '{profile.name}' " +
            $"axis={action.PrincipalAxis} displacement={dist:F3}m duration={action.Duration:F3}s segmentLen={seg:F3}");
    }
}
#endif
