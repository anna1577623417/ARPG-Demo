#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// MotionProfile Inspector — Segment Preset 编辑区（选中 Key → 下一 Key 段）。
/// </summary>
public static class MotionCurveSegmentPresetGUI
{
    enum AxisChoice
    {
        X,
        Y,
        Z,
    }

    static AxisChoice s_axis = AxisChoice.Z;
    static int s_selectedKeyIndex;
    static bool s_segmentFoldout;

    public static void Draw(MotionProfileSO profile, SerializedObject serializedObject)
    {
        EditorGUILayout.Space(8f);
        s_segmentFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_segmentFoldout,
            "Selected Segment（曲线段预设）");
        if (!s_segmentFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.HelpBox(
            "选择轴与段起点 Key，再点预设按钮。仅修改该段 Key[i].outTangent 与 Key[i+1].inTangent，不改时间与值。",
            MessageType.None);

        if (profile == null || !profile.UsesAxisCurves)
        {
            EditorGUILayout.HelpBox("请先配置 AxisCurves。", MessageType.Warning);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("编辑轴");
            s_axis = (AxisChoice)GUILayout.Toolbar((int)s_axis, new[] { "X", "Y", "Z" });
        }

        var curve = GetCurve(profile, s_axis);
        if (curve == null || curve.length == 0)
        {
            EditorGUILayout.HelpBox($"当前 {s_axis} 轴无曲线，请先在上方添加 Key。", MessageType.Info);
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        DrawKeySegmentSelector(curve);

        var canApply = MotionCurveSegmentPresetUtility.CanApplySegment(curve, s_selectedKeyIndex);
        using (new EditorGUI.DisabledScope(!canApply))
        {
            DrawSegmentSummary(curve, s_selectedKeyIndex);
            DrawPresetButtons(profile, serializedObject, curve);
        }

        if (!canApply)
        {
            if (curve.length < 2)
            {
                EditorGUILayout.HelpBox("至少需要 2 个 Key 才能编辑 Segment。", MessageType.Info);
            }
            else if (s_selectedKeyIndex >= curve.length - 1)
            {
                EditorGUILayout.HelpBox("最后一个 Key 无下一段，请选择更早的 Key。", MessageType.Info);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static AnimationCurve GetCurve(MotionProfileSO profile, AxisChoice axis) => axis switch
    {
        AxisChoice.X => profile.AxisCurves.XCurve,
        AxisChoice.Y => profile.AxisCurves.YCurve,
        AxisChoice.Z => profile.AxisCurves.ZCurve,
        _ => null,
    };

    static void SetCurve(MotionProfileSO profile, AxisChoice axis, AnimationCurve curve)
    {
        switch (axis)
        {
            case AxisChoice.X:
                profile.AxisCurves.XCurve = curve;
                break;
            case AxisChoice.Y:
                profile.AxisCurves.YCurve = curve;
                break;
            case AxisChoice.Z:
                profile.AxisCurves.ZCurve = curve;
                break;
        }
    }

    static void DrawKeySegmentSelector(AnimationCurve curve)
    {
        var segCount = Mathf.Max(0, curve.length - 1);
        s_selectedKeyIndex = Mathf.Clamp(s_selectedKeyIndex, 0, Mathf.Max(0, segCount - 1));

        EditorGUILayout.LabelField("段起点 Key", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            for (var i = 0; i < segCount; i++)
            {
                var label = $"[{i}]→[{i + 1}]";
                var active = i == s_selectedKeyIndex;
                if (GUILayout.Toggle(active, label, EditorStyles.miniButton))
                {
                    s_selectedKeyIndex = i;
                }
            }
        }

        if (segCount > 0)
        {
            s_selectedKeyIndex = EditorGUILayout.IntSlider("Key 序号", s_selectedKeyIndex, 0, segCount - 1);
        }
    }

    static void DrawSegmentSummary(AnimationCurve curve, int keyIndex)
    {
        var a = curve.keys[keyIndex];
        var b = curve.keys[keyIndex + 1];
        EditorGUILayout.LabelField("Selected Segment", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(
            $"Key[{keyIndex}]  t={a.time:F3}  v={a.value:F3}   →   Key[{keyIndex + 1}]  t={b.time:F3}  v={b.value:F3}",
            EditorStyles.wordWrappedLabel);
    }

    static void DrawPresetButtons(MotionProfileSO profile, SerializedObject serializedObject, AnimationCurve curve)
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(profile, serializedObject, curve, MotionCurveSegmentPreset.Linear);
            DrawPresetButton(profile, serializedObject, curve, MotionCurveSegmentPreset.EaseIn);
            DrawPresetButton(profile, serializedObject, curve, MotionCurveSegmentPreset.EaseOut);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton(profile, serializedObject, curve, MotionCurveSegmentPreset.EaseInOut);
            DrawPresetButton(profile, serializedObject, curve, MotionCurveSegmentPreset.FastOut);
            DrawPresetButton(profile, serializedObject, curve, MotionCurveSegmentPreset.SlowOut);
        }
    }

    static void DrawPresetButton(
        MotionProfileSO profile,
        SerializedObject serializedObject,
        AnimationCurve curve,
        MotionCurveSegmentPreset preset)
    {
        var label = MotionCurveSegmentPresetUtility.GetPresetLabel(preset);
        if (!GUILayout.Button(label, GUILayout.MinHeight(22f)))
        {
            return;
        }

        Undo.RecordObject(profile, $"Apply Segment Preset {label}");
        if (MotionCurveSegmentPresetUtility.ApplySegmentPreset(curve, s_selectedKeyIndex, preset))
        {
            SetCurve(profile, s_axis, curve);
            EditorUtility.SetDirty(profile);
            serializedObject?.Update();
        }
    }
}
#endif
