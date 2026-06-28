#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class MotionProfileEditor
{
    static bool s_manualCurveFoldout;

    void DrawManualCurveSection(MotionProfileSO profile)
    {
        EditorGUILayout.Space(4f);
        s_manualCurveFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_manualCurveFoldout,
            "手工编辑 XYZ 曲线（可选）");
        if (!s_manualCurveFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.HelpBox(
            "直接编辑 AxisCurves；Scale 为米。提取时 Source=Manual 的轴不会被覆盖。",
            MessageType.None);

        DrawAxisCurvesVertical(profile);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("曲线预设", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Z 冲刺 Ease", EditorStyles.miniButtonLeft))
            {
                ApplyForwardZPreset(profile, _defaultForwardMeters);
            }

            if (GUILayout.Button("三轴归零", EditorStyles.miniButtonMid))
            {
                ApplyZeroPreset(profile);
            }

            if (GUILayout.Button("Y 小跳 Ease", EditorStyles.miniButtonRight))
            {
                ApplyJumpYPreset(profile, 0.6f);
            }
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(profile);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawAxisCurvesVertical(MotionProfileSO profile)
    {
        DrawAxisRow(profile, "【X】Left ↔ Right",
            ref profile.AxisCurves.XCurve, ref profile.AxisCurves.XScale);
        EditorGUILayout.Space(2f);
        DrawAxisRow(profile, "【Y】Down ↕ Up",
            ref profile.AxisCurves.YCurve, ref profile.AxisCurves.YScale);
        EditorGUILayout.Space(2f);
        DrawAxisRow(profile, "【Z】Back ↔ Forward",
            ref profile.AxisCurves.ZCurve, ref profile.AxisCurves.ZScale);
    }

    static void DrawAxisRow(
        MotionProfileSO profile,
        string axisLabel,
        ref AnimationCurve curve,
        ref float scale)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(axisLabel, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("↕", "垂直镜像曲线"), GUILayout.Width(24f), GUILayout.Height(16f)))
            {
                Undo.RecordObject(profile, $"Flip {axisLabel}");
                MotionAxisCurveFlipUtil.FlipCurve(ref curve);
            }
        }

        curve = EditorGUILayout.CurveField(GUIContent.none, curve, GUILayout.MinHeight(22f));
        EditorGUILayout.LabelField("Scale (m)", EditorStyles.miniLabel);
        scale = EditorGUILayout.FloatField(GUIContent.none, scale, GUILayout.Width(72f));
    }

    static void ApplyForwardZPreset(MotionProfileSO profile, float meters)
    {
        Undo.RecordObject(profile, "Motion Z Forward Preset");
        profile.AxisCurves.ZCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        profile.AxisCurves.ZScale = Mathf.Max(0f, meters);
        EditorUtility.SetDirty(profile);
    }

    static void ApplyZeroPreset(MotionProfileSO profile)
    {
        Undo.RecordObject(profile, "Motion Zero Preset");
        var flat = AnimationCurve.Constant(0f, 0f, 1f);
        profile.AxisCurves.XCurve = flat;
        profile.AxisCurves.YCurve = flat;
        profile.AxisCurves.ZCurve = flat;
        profile.AxisCurves.XScale = 0f;
        profile.AxisCurves.YScale = 0f;
        profile.AxisCurves.ZScale = 0f;
        EditorUtility.SetDirty(profile);
    }

    static void ApplyJumpYPreset(MotionProfileSO profile, float heightMeters)
    {
        Undo.RecordObject(profile, "Motion Y Jump Preset");
        profile.AxisCurves.YCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f));
        profile.AxisCurves.YScale = Mathf.Max(0f, heightMeters);
        EditorUtility.SetDirty(profile);
    }
}
#endif
