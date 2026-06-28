#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 202.2 — MotionProfile 位移曲线垂直镜像（沿时间轴水平镜面）：仅 Value 取反，Scale 不变。
/// 每次点击：0→-1 ↔ 0→+1，可反复切换。
/// </summary>
public static class MotionAxisCurveFlipUtil
{
    /// <summary>曲线 Y 值取反；不改变 Scale。</summary>
    public static void FlipCurve(ref AnimationCurve curve)
    {
        curve = FlipCurveVertical(curve);
    }

    public static AnimationCurve FlipCurveVertical(AnimationCurve source)
    {
        if (source == null || source.length == 0)
        {
            return source;
        }

        var keys = source.keys;
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i].value = -keys[i].value;
            keys[i].inTangent = -keys[i].inTangent;
            keys[i].outTangent = -keys[i].outTangent;
        }

        return new AnimationCurve(keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode,
        };
    }
}
#endif
