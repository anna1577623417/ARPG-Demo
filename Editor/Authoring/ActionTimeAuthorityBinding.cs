#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 171.7 A.W6 — Inspector 与 Timeline TimeWindow 共享 SerializedProperty 双绑。
/// </summary>
sealed class ActionTimeAuthorityBinding
{
    public SerializedObject SerializedAction;
    public SerializedProperty PropDuration;
    public SerializedProperty PropAnimSpeed;
    public SerializedProperty PropClipAnimSpeedMode;
    public SerializedProperty PropSegmentStart;
    public SerializedProperty PropSegmentEnd;

    public ActionDataSO Action => SerializedAction?.targetObject as ActionDataSO;

    public static ActionTimeAuthorityBinding Create(SerializedObject serializedAction)
    {
        if (serializedAction == null)
        {
            return null;
        }

        return new ActionTimeAuthorityBinding
        {
            SerializedAction = serializedAction,
            PropDuration = serializedAction.FindProperty(nameof(ActionDataSO.Duration)),
            PropAnimSpeed = serializedAction.FindProperty(nameof(ActionDataSO.AnimSpeed)),
            PropClipAnimSpeedMode = serializedAction.FindProperty(nameof(ActionDataSO.ClipAnimSpeedMode)),
            PropSegmentStart = serializedAction.FindProperty(nameof(ActionDataSO.SegmentStart)),
            PropSegmentEnd = serializedAction.FindProperty(nameof(ActionDataSO.SegmentEnd)),
        };
    }

    public void Refresh()
    {
        if (SerializedAction == null)
        {
            return;
        }

        // 同一 OnGUI 帧内若已有未提交的 PropertyField 修改（如 Timeline 上方 Main Clip），
        // 不可 Update() — 否则会丢弃拖拽赋值。
        if (!SerializedAction.hasModifiedProperties)
        {
            SerializedAction.Update();
        }
    }

    public void Commit()
    {
        if (SerializedAction == null)
        {
            return;
        }

        SerializedAction.ApplyModifiedProperties();
        EditorUtility.SetDirty(SerializedAction.targetObject);
        ActionTimelineRootMotionSampler.InvalidateCache();
        SceneView.RepaintAll();
    }

    public bool IsAutoFit =>
        PropClipAnimSpeedMode != null
        && (ActionAnimSpeedMode)PropClipAnimSpeedMode.enumValueIndex == ActionAnimSpeedMode.AutoFitDuration;

    public float LiveAutoFitAnimSpeed =>
        Action != null ? ActionAnimSpeedAuthority.ResolveAutoFitClipAnimSpeed(Action) : 1f;

    /// <summary>Timeline 上方紧凑 Authority 行（Duration / AnimSpeed / Segment）。</summary>
    public void DrawTimelineCompactRow(float availableWidth)
    {
        if (SerializedAction == null || Action == null)
        {
            return;
        }

        Refresh();
        ActionIdleCategoryMigration.WarnIfUnmigratedIdle(Action);
        ActionTimelineEditorUI.LightSectionSeparator("Time Authority");

        if (availableWidth >= 680f)
        {
            DrawTimelineAuthoritySingleRow(availableWidth);
        }
        else if (availableWidth >= 420f)
        {
            DrawTimelineAuthorityBalancedRows(availableWidth);
        }
        else
        {
            DrawTimelineAuthorityStacked();
        }

        if (SerializedAction.hasModifiedProperties)
        {
            Commit();
        }
    }

    void DrawTimelineAuthoritySingleRow(float width)
    {
        var colPad = 8f;
        var halfW = (width - colPad) * 0.5f;

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(halfW)))
            {
                DrawDurationClusterInColumn();
            }

            GUILayout.Space(colPad);

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                DrawAnimSpeedClusterInColumn();
            }
        }

        DrawSegmentClusterInColumn();
    }

    void DrawTimelineAuthorityBalancedRows(float width)
    {
        var half = (width - 6f) * 0.5f;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(half)))
            {
                DrawDurationClusterInColumn();
            }

            GUILayout.Space(6f);

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                DrawAnimSpeedClusterInColumn();
            }
        }

        DrawSegmentClusterInColumn();
    }

    void DrawTimelineAuthorityStacked()
    {
        DrawDurationClusterInColumn();
        DrawAnimSpeedClusterInColumn();
        DrawSegmentClusterInColumn();
    }

    void DrawDurationClusterInColumn()
    {
        EditorGUILayout.LabelField("Duration", EditorStyles.miniLabel);
        EditorGUI.BeginChangeCheck();
        var next = EditorGUILayout.DelayedFloatField(PropDuration.floatValue);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(Action, "Edit Duration");
            PropDuration.floatValue = Mathf.Max(0f, next);
            Commit();
        }
    }

    void DrawAnimSpeedClusterInColumn()
    {
        EditorGUILayout.LabelField("Anim Speed", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAnimSpeedModePopup(GUILayout.MinWidth(72f), GUILayout.MaxWidth(96f));

            using (new EditorGUI.DisabledScope(IsAutoFit))
            {
                EditorGUI.BeginChangeCheck();
                var next = EditorGUILayout.DelayedFloatField(
                    PropAnimSpeed.floatValue,
                    GUILayout.MinWidth(44f),
                    GUILayout.MaxWidth(64f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(Action, "Edit Anim Speed");
                    PropAnimSpeed.floatValue = next;
                    Commit();
                }
            }

            if (IsAutoFit)
            {
                EditorGUILayout.LabelField(
                    $"Live ×{LiveAutoFitAnimSpeed:F2}",
                    EditorStyles.miniLabel,
                    GUILayout.ExpandWidth(true));
            }
            else
            {
                EditorGUILayout.LabelField("×", EditorStyles.miniLabel, GUILayout.Width(10f));
            }
        }
    }

    void DrawAnimSpeedModePopup(params GUILayoutOption[] options)
    {
        const int modeCount = 2;
        var labels = new[] { "Free", "Auto Fit" };
        var idx = Mathf.Clamp(PropClipAnimSpeedMode.enumValueIndex, 0, modeCount - 1);
        EditorGUI.BeginChangeCheck();
        var nextIdx = EditorGUILayout.Popup(idx, labels, options);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(Action, "Edit Clip Anim Speed Mode");
            PropClipAnimSpeedMode.enumValueIndex = nextIdx;
            Commit();
        }
    }

    void DrawSegmentClusterInColumn()
    {
        if (Action.MainClip == null)
        {
            EditorGUILayout.LabelField("Segment", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("(无 MainClip)", EditorStyles.miniLabel);
            return;
        }

        EditorGUILayout.LabelField("Segment", EditorStyles.miniLabel);

        var segStart = PropSegmentStart.floatValue;
        var segEnd = PropSegmentEnd.floatValue;

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.MinMaxSlider(
            new GUIContent("Range", "Clip 归一化片段；Timeline 仍编辑 Action 0~1"),
            ref segStart,
            ref segEnd,
            0f,
            1f);
        if (EditorGUI.EndChangeCheck())
        {
            ApplySegmentRange(segStart, segEnd);
        }

        segStart = PropSegmentStart.floatValue;
        segEnd = PropSegmentEnd.floatValue;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            var typedStart = EditorGUILayout.DelayedFloatField(segStart, GUILayout.Width(52f));
            EditorGUILayout.LabelField("→", EditorStyles.miniLabel, GUILayout.Width(14f));
            var typedEnd = EditorGUILayout.DelayedFloatField(segEnd, GUILayout.Width(52f));
            if (!Mathf.Approximately(typedStart, segStart))
            {
                segStart = Mathf.Clamp01(typedStart);
            }

            if (!Mathf.Approximately(typedEnd, segEnd))
            {
                segEnd = Mathf.Clamp01(typedEnd);
            }

            if (EditorGUI.EndChangeCheck())
            {
                ApplySegmentRange(segStart, segEnd);
            }

            var clipLen = Action.MainClip.length;
            var deltaSec = (PropSegmentEnd.floatValue - PropSegmentStart.floatValue) * clipLen;
            EditorGUILayout.LabelField(
                $"Δ={deltaSec:F3}s",
                EditorStyles.miniLabel,
                GUILayout.MinWidth(64f));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                    new GUIContent("Snap", "吸附 Segment 起止到 30fps 帧边界"),
                    EditorStyles.miniButtonLeft,
                    GUILayout.Width(52f)))
            {
                SnapSegmentToFrame(30f);
            }

            if (GUILayout.Button(
                    new GUIContent("0~1", "重置 Segment 为整段 0~1"),
                    EditorStyles.miniButtonRight,
                    GUILayout.Width(44f)))
            {
                Undo.RecordObject(Action, "Reset Segment 0~1");
                PropSegmentStart.floatValue = 0f;
                PropSegmentEnd.floatValue = 1f;
                Commit();
            }
        }
    }

    void ApplySegmentRange(float segStart, float segEnd)
    {
        Undo.RecordObject(Action, "Edit Clip Segment");
        PropSegmentStart.floatValue = Mathf.Clamp01(segStart);
        PropSegmentEnd.floatValue = Mathf.Clamp01(Mathf.Max(segEnd, segStart + 0.001f));
        ActionTimeAuthority.NormalizeSegmentRange(Action);
        Commit();
    }

    /// <summary>无宽度参数时按宽屏单列布局。</summary>
    public void DrawTimelineCompactRow()
    {
        DrawTimelineCompactRow(720f);
    }

    void SnapSegmentToFrame(float fps)
    {
        if (Action?.MainClip == null)
        {
            return;
        }

        Undo.RecordObject(Action, "Snap Segment To Frame");
        var frameStep = 1f / fps;
        var clipLenSec = Mathf.Max(0.001f, Action.MainClip.length);
        PropSegmentStart.floatValue = Mathf.Round(PropSegmentStart.floatValue * clipLenSec / frameStep)
            * frameStep / clipLenSec;
        PropSegmentEnd.floatValue = Mathf.Round(PropSegmentEnd.floatValue * clipLenSec / frameStep)
            * frameStep / clipLenSec;
        ActionTimeAuthority.NormalizeSegmentRange(Action);
        Commit();
    }

    /// <summary>Inspector Time Authority 区 AnimSpeed 模式 + 字段 + Live。</summary>
    public void DrawInspectorAnimSpeedBlock()
    {
        if (SerializedAction == null)
        {
            return;
        }

        Refresh();
        EditorGUILayout.PropertyField(
            PropClipAnimSpeedMode,
            new GUIContent("Anim Speed Mode", "Free = 手填 AnimSpeed；AutoFitDuration = Clip×Segment÷Duration"));

        using (new EditorGUI.DisabledScope(IsAutoFit))
        {
            EditorGUILayout.PropertyField(
                PropAnimSpeed,
                new GUIContent("Anim Speed", "Clip 速率；「按 Duration 匹配 Clip 速率」= 一次性写入 Free 值"));
        }

        if (IsAutoFit)
        {
            EditorGUILayout.LabelField("Live (auto)", $"×{LiveAutoFitAnimSpeed:F3}", EditorStyles.miniLabel);
        }
    }
}
#endif
