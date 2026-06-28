#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 174.2 — MotionProfile V2 区段：Curve Segment Library 一键插入。
/// V2 字段自身由 DrawPropertiesExcluding 自动渲染；本文件只补"快速预设按钮"。
/// </summary>
public sealed partial class MotionProfileEditor
{
    static bool s_v2LibraryFoldout = false;
    static int s_v2TargetCurveIndex = 0;
    static MotionCurveLibrary.Segment s_v2SelectedSeg = MotionCurveLibrary.Segment.ConstantOne;

    static readonly (string Label, string PropName)[] s_v2CurveTargets = new (string, string)[]
    {
        ("Gravity Weight",      nameof(MotionProfileSO.V2GravityWeight)),
        ("Yaw Over Time",       nameof(MotionProfileSO.V2YawOverTime)),
        ("Facing Input Weight", nameof(MotionProfileSO.V2FacingInputWeight)),
        ("Move Input Weight",   nameof(MotionProfileSO.V2MoveInputWeight)),
        ("Target Tracking",     nameof(MotionProfileSO.V2TargetTrackingWeight)),
        ("Root Motion Blend",   nameof(MotionProfileSO.V2RootMotionBlend)),
        ("Hitstop Multiplier",  nameof(MotionProfileSO.V2HitstopMultiplier)),
    };

    public void DrawV2CurveLibrarySection()
    {
        DrawV2StatusOverview();

        s_v2LibraryFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            s_v2LibraryFoldout, "V2 Curve Library / 一键预设（174.2）");
        if (!s_v2LibraryFoldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.HelpBox(
            "选择目标曲线 + 预设段，点击 Apply 将一键替换对应字段为该段曲线。\n" +
            "每段附「对应游戏招式 + 应用层」提示，便于知识复用。",
            MessageType.Info);

        var targetLabels = new string[s_v2CurveTargets.Length];
        for (var i = 0; i < s_v2CurveTargets.Length; i++)
        {
            targetLabels[i] = s_v2CurveTargets[i].Label;
        }
        s_v2TargetCurveIndex = EditorGUILayout.Popup("目标曲线", s_v2TargetCurveIndex, targetLabels);
        s_v2TargetCurveIndex = Mathf.Clamp(s_v2TargetCurveIndex, 0, s_v2CurveTargets.Length - 1);

        s_v2SelectedSeg = (MotionCurveLibrary.Segment)EditorGUILayout.EnumPopup("预设段", s_v2SelectedSeg);
        EditorGUILayout.HelpBox(MotionCurveLibrary.GetUseCase(s_v2SelectedSeg), MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button($"Apply → {s_v2CurveTargets[s_v2TargetCurveIndex].Label}", GUILayout.Height(24)))
            {
                ApplySegmentToTarget();
            }

            if (GUILayout.Button("预览生成的曲线", GUILayout.Height(24)))
            {
                Debug.Log($"[V2 Curve Preview] seg={s_v2SelectedSeg} keys={MotionCurveLibrary.Make(s_v2SelectedSeg).length}");
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>174.2 W13 — V2 状态概览：一眼看清本 Profile 启用了哪些 V2 通道。</summary>
    void DrawV2StatusOverview()
    {
        var p = (MotionProfileSO)target;
        var sb = new System.Text.StringBuilder();
        sb.Append("V2 通道：");
        var anyActive = false;

        if (p.V2GravityWeightMode == GravityWeightMode.Curve)
        {
            sb.Append("Gravity(Curve) ");
            anyActive = true;
        }
        if (p.V2RotationMode != RotationMode.None)
        {
            sb.Append($"Rotation({p.V2RotationMode}) ");
            anyActive = true;
        }
        if (p.V2YStrategy != YStrategyV2.Default)
        {
            sb.Append($"YStrat({p.V2YStrategy}) ");
            anyActive = true;
        }
        if (HasNonDefaultCurve(p.V2FacingInputWeight, 1f))
        {
            sb.Append("FacingW ");
            anyActive = true;
        }
        if (HasNonDefaultCurve(p.V2MoveInputWeight, 0f))
        {
            sb.Append("MoveW ");
            anyActive = true;
        }
        if (HasNonDefaultCurve(p.V2TargetTrackingWeight, 1f))
        {
            sb.Append("Tracking ");
            anyActive = true;
        }
        if (HasNonDefaultCurve(p.V2RootMotionBlend, 0f))
        {
            sb.Append("RMBlend ");
            anyActive = true;
        }
        if (HasNonDefaultCurve(p.V2HitstopMultiplier, 1f))
        {
            sb.Append("Hitstop ");
            anyActive = true;
        }

        if (!anyActive)
        {
            sb.Append("(全部 V1 兼容，无 V2 覆盖)");
        }

        EditorGUILayout.HelpBox(sb.ToString(), anyActive ? MessageType.None : MessageType.Info);
    }

    static bool HasNonDefaultCurve(AnimationCurve curve, float defaultValue)
    {
        if (curve == null || curve.length == 0)
        {
            return false;
        }
        for (var i = 0; i < curve.length; i++)
        {
            if (Mathf.Abs(curve[i].value - defaultValue) > 0.001f)
            {
                return true;
            }
        }
        return false;
    }

    void ApplySegmentToTarget()
    {
        var profile = (MotionProfileSO)target;
        var prop = serializedObject.FindProperty(s_v2CurveTargets[s_v2TargetCurveIndex].PropName);
        if (prop == null)
        {
            Debug.LogError($"[V2 Curve Library] property not found: {s_v2CurveTargets[s_v2TargetCurveIndex].PropName}");
            return;
        }

        Undo.RecordObject(profile, $"V2 Curve Library: {s_v2SelectedSeg} → {s_v2CurveTargets[s_v2TargetCurveIndex].Label}");
        prop.animationCurveValue = MotionCurveLibrary.Make(s_v2SelectedSeg);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
    }
}
#endif
