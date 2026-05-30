#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 对 AnimationCurve 的 Key[i]→Key[i+1] 段应用切线预设（不改 Key 时间与值）。
/// </summary>
public static class MotionCurveSegmentPresetUtility
{
    const float FastOutMultiplier = 2.5f;
    const float SlowOutEndFactor = 0.12f;

    public static bool CanApplySegment(AnimationCurve curve, int keyIndex) =>
        curve != null && curve.length >= 2 && keyIndex >= 0 && keyIndex < curve.length - 1;

    public static bool ApplySegmentPreset(AnimationCurve curve, int keyIndex, MotionCurveSegmentPreset preset)
    {
        if (!CanApplySegment(curve, keyIndex))
        {
            return false;
        }

        var current = curve.keys[keyIndex];
        var next = curve.keys[keyIndex + 1];
        var dt = next.time - current.time;
        if (Mathf.Abs(dt) < 1e-6f)
        {
            return false;
        }

        var slope = (next.value - current.value) / dt;
        ApplyTangents(preset, slope, ref current, ref next);

        current.weightedMode = WeightedMode.None;
        next.weightedMode = WeightedMode.None;

        curve.MoveKey(keyIndex, current);
        curve.MoveKey(keyIndex + 1, next);

        AnimationUtility.SetKeyBroken(curve, keyIndex, true);
        AnimationUtility.SetKeyBroken(curve, keyIndex + 1, true);
        AnimationUtility.SetKeyRightTangentMode(curve, keyIndex, AnimationUtility.TangentMode.Free);
        AnimationUtility.SetKeyLeftTangentMode(curve, keyIndex + 1, AnimationUtility.TangentMode.Free);

        // MoveKey 可能重算切线，Free 模式后再写一次
        current = curve.keys[keyIndex];
        next = curve.keys[keyIndex + 1];
        ApplyTangents(preset, slope, ref current, ref next);
        curve.MoveKey(keyIndex, current);
        curve.MoveKey(keyIndex + 1, next);
        AnimationUtility.SetKeyRightTangentMode(curve, keyIndex, AnimationUtility.TangentMode.Free);
        AnimationUtility.SetKeyLeftTangentMode(curve, keyIndex + 1, AnimationUtility.TangentMode.Free);

        return true;
    }

    static void ApplyTangents(
        MotionCurveSegmentPreset preset,
        float slope,
        ref Keyframe current,
        ref Keyframe next)
    {
        switch (preset)
        {
            case MotionCurveSegmentPreset.Linear:
                current.outTangent = slope;
                next.inTangent = slope;
                break;

            case MotionCurveSegmentPreset.EaseIn:
                current.outTangent = 0f;
                next.inTangent = slope;
                break;

            case MotionCurveSegmentPreset.EaseOut:
                current.outTangent = slope;
                next.inTangent = 0f;
                break;

            case MotionCurveSegmentPreset.EaseInOut:
                current.outTangent = 0f;
                next.inTangent = 0f;
                break;

            case MotionCurveSegmentPreset.FastOut:
                current.outTangent = slope * FastOutMultiplier;
                next.inTangent = 0f;
                break;

            case MotionCurveSegmentPreset.SlowOut:
                current.outTangent = slope;
                next.inTangent = slope * SlowOutEndFactor;
                break;

            default:
                current.outTangent = slope;
                next.inTangent = slope;
                break;
        }
    }

    public static string GetPresetLabel(MotionCurveSegmentPreset preset) => preset switch
    {
        MotionCurveSegmentPreset.Linear => "Linear",
        MotionCurveSegmentPreset.EaseIn => "Ease In",
        MotionCurveSegmentPreset.EaseOut => "Ease Out",
        MotionCurveSegmentPreset.EaseInOut => "Ease In Out",
        MotionCurveSegmentPreset.FastOut => "Fast Out",
        MotionCurveSegmentPreset.SlowOut => "Slow Out",
        _ => preset.ToString(),
    };
}
#endif
