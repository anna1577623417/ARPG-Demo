#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 预设函数到 AnimationCurve 的生成器。
/// Why: 把“手感函数”参数化，减少纯手搓曲线的维护成本。
/// </summary>
public static class MotionCurveGenerator
{
    public static AnimationCurve Generate(CurvePresetType type, float power = 2f, int sampleCount = 32)
    {
        sampleCount = Mathf.Clamp(sampleCount, 8, 128);
        var keys = new Keyframe[sampleCount + 1];
        var k = Mathf.Max(1f, power);

        for (var i = 0; i <= sampleCount; i++)
        {
            var t = (float)i / sampleCount;
            keys[i] = new Keyframe(t, Evaluate(type, t, k));
        }

        return new AnimationCurve(keys);
    }

    private static float Evaluate(CurvePresetType type, float t, float k)
    {
        switch (type)
        {
            case CurvePresetType.Linear:
                return t;
            case CurvePresetType.EaseIn:
                return Mathf.Pow(t, k);
            case CurvePresetType.EaseOut:
                return 1f - Mathf.Pow(1f - t, k);
            case CurvePresetType.EaseInOut:
                if (t < 0.5f)
                {
                    return 0.5f * Mathf.Pow(2f * t, k);
                }

                return 1f - 0.5f * Mathf.Pow(2f * (1f - t), k);
            case CurvePresetType.BurstStop:
                return 1f - Mathf.Pow(1f - t, k * 2f);
            default:
                return t;
        }
    }
}
#endif
