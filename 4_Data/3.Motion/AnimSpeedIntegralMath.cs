using UnityEngine;

/// <summary>
/// 局部 Anim 曲线积分守恒与三点求解（226）。
/// 硬约束：∫₀¹ f(u) du = 1，使 AutoFit/Free 叠加曲线后总播放量与 Constant 一致。
/// </summary>
public static class AnimSpeedIntegralMath
{
    public const float DefaultEpsilon = 0.001f;
    public const float DefaultMidTime = 0.5f;
    const int DefaultSampleCount = 64;

    /// <summary>分段线性三点积分：I = (a+b)·m/2 + (b+c)·(1-m)/2。</summary>
    public static float IntegrateThreePoint(float start, float mid, float end, float midTime = DefaultMidTime)
    {
        var m = Mathf.Clamp01(midTime);
        return (start + mid) * m * 0.5f + (mid + end) * (1f - m) * 0.5f;
    }

    /// <summary>对 AnimationCurve 在 [0,1] 上做梯形积分。</summary>
    public static float IntegrateCurve(AnimationCurve curve, int sampleCount = DefaultSampleCount)
    {
        if (curve == null || curve.length == 0)
        {
            return 1f;
        }

        var steps = Mathf.Max(2, sampleCount);
        var sum = 0f;
        var prevT = 0f;
        var prevV = Mathf.Max(0f, curve.Evaluate(0f));
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var v = Mathf.Max(0f, curve.Evaluate(t));
            sum += (prevV + v) * 0.5f * (t - prevT);
            prevT = t;
            prevV = v;
        }

        return sum;
    }

    /// <summary>积分 ∫₀^{toNt} f；用于 clipDone / Preview 推演。</summary>
    public static float IntegrateCurveRange(
        AnimationCurve curve,
        float toNt,
        int sampleCount = DefaultSampleCount)
    {
        if (curve == null || curve.length == 0)
        {
            return Mathf.Clamp01(toNt);
        }

        var end = Mathf.Clamp01(toNt);
        if (end <= 0f)
        {
            return 0f;
        }

        var steps = Mathf.Max(2, sampleCount);
        var sum = 0f;
        var prevT = 0f;
        var prevV = Mathf.Max(0f, curve.Evaluate(0f));
        for (var i = 1; i <= steps; i++)
        {
            var t = end * (i / (float)steps);
            var v = Mathf.Max(0f, curve.Evaluate(t));
            sum += (prevV + v) * 0.5f * (t - prevT);
            prevT = t;
            prevV = v;
        }

        return sum;
    }

    public static bool IsIntegralValid(float integral, float epsilon = DefaultEpsilon) =>
        Mathf.Abs(integral - 1f) <= epsilon;

    public static bool TryValidateCurve(
        AnimationCurve curve,
        out float integral,
        float epsilon = DefaultEpsilon)
    {
        integral = IntegrateCurve(curve);
        return IsIntegralValid(integral, epsilon);
    }

    /// <summary>
    /// 锁 SolveTarget 以外两点，反算目标点，使 I=1。
    /// 解为负或 MidTime 非法时失败（策略1：拒绝写入，由 Editor 红字）。
    /// </summary>
    public static bool TrySolveThirdPoint(ref AnimSpeedThreePointSpec spec, out string error)
    {
        error = null;
        var m = spec.MidTime;
        if (m <= 0.001f || m >= 0.999f)
        {
            error = "MidTime 必须在 (0,1) 内。";
            return false;
        }

        var a = spec.Start;
        var b = spec.Mid;
        var c = spec.End;

        switch (spec.SolveTarget)
        {
            case AnimSpeedCurveSolveTarget.End:
                // (a+b)m/2 + (b+c)(1-m)/2 = 1
                // (b+c)(1-m) = 2 - (a+b)m
                // c = [2 - (a+b)m]/(1-m) - b
                c = (2f - (a + b) * m) / (1f - m) - b;
                break;
            case AnimSpeedCurveSolveTarget.Start:
                // (a+b)m/2 + (b+c)(1-m)/2 = 1
                // (a+b)m = 2 - (b+c)(1-m)
                // a = [2 - (b+c)(1-m)]/m - b
                a = (2f - (b + c) * (1f - m)) / m - b;
                break;
            case AnimSpeedCurveSolveTarget.Mid:
                // (a+b)m/2 + (b+c)(1-m)/2 = 1
                // b·m/2 + b·(1-m)/2 = 1 - a·m/2 - c·(1-m)/2
                // b/2 = 1 - a·m/2 - c·(1-m)/2
                // b = 2 - a·m - c·(1-m)
                b = 2f - a * m - c * (1f - m);
                break;
            default:
                error = "未知 SolveTarget。";
                return false;
        }

        if (a < 0f || b < 0f || c < 0f)
        {
            error = $"求解得负倍率（Start={a:F3}, Mid={b:F3}, End={c:F3}）。请调高锁定点或改 SolveTarget。";
            return false;
        }

        spec.Start = a;
        spec.Mid = b;
        spec.End = c;

        var integral = IntegrateThreePoint(a, b, c, m);
        if (!IsIntegralValid(integral))
        {
            error = $"积分未收敛 I={integral:F4}（ε={DefaultEpsilon}）。";
            return false;
        }

        return true;
    }

    public static AnimationCurve BuildThreePointCurve(in AnimSpeedThreePointSpec spec)
    {
        var m = Mathf.Clamp(spec.MidTime, 0.001f, 0.999f);
        var curve = new AnimationCurve(
            new Keyframe(0f, Mathf.Max(0f, spec.Start)),
            new Keyframe(m, Mathf.Max(0f, spec.Mid)),
            new Keyframe(1f, Mathf.Max(0f, spec.End)));
        for (var i = 0; i < curve.length; i++)
        {
            AnimationUtilitySetLinear(curve, i);
        }

        return curve;
    }

    /// <summary>无 Editor 依赖的线性切线近似（Runtime 安全）。</summary>
    static void AnimationUtilitySetLinear(AnimationCurve curve, int index)
    {
        if (curve == null || index < 0 || index >= curve.length)
        {
            return;
        }

        var key = curve[index];
        key.inTangent = 0f;
        key.outTangent = 0f;
        key.tangentMode = 0;
        curve.MoveKey(index, key);
        if (index > 0)
        {
            var prev = curve[index - 1];
            var dt = key.time - prev.time;
            var slope = dt > 1e-6f ? (key.value - prev.value) / dt : 0f;
            prev.outTangent = slope;
            key.inTangent = slope;
            curve.MoveKey(index - 1, prev);
            curve.MoveKey(index, key);
        }
    }

    public static AnimSpeedThreePointSpec SampleThreePointFromCurve(
        AnimationCurve curve,
        float midTime = DefaultMidTime,
        AnimSpeedCurveSolveTarget solveTarget = AnimSpeedCurveSolveTarget.End)
    {
        midTime = Mathf.Clamp(midTime, 0.001f, 0.999f);
        if (curve == null || curve.length == 0)
        {
            return AnimSpeedThreePointSpec.DefaultConserve;
        }

        return new AnimSpeedThreePointSpec
        {
            MidTime = midTime,
            Start = Mathf.Max(0f, curve.Evaluate(0f)),
            Mid = Mathf.Max(0f, curve.Evaluate(midTime)),
            End = Mathf.Max(0f, curve.Evaluate(1f)),
            SolveTarget = solveTarget,
        };
    }
}
