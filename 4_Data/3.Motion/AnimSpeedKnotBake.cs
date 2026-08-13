using System.Collections.Generic;
using UnityEngine;

/// <summary>【228】FreeFrontAutoTail Bake 结果（纯数据，无 Undo）。</summary>
public struct AnimSpeedKnotBakeResult
{
    public AnimationCurve Curve;
    public float FrontIntegral;
    public float TailBudget;
    public float TailLength;
    public float TailStart;
    public float TailEnd;
    public float TotalIntegral;
    public bool AutoTailUsed;
    public string Warning;
}

/// <summary>
/// 【228】共享结点前段积分与 AutoTail 求解/烘焙。
/// 纯函数；写回 SO 由 <see cref="MotionProfileSO.TryBakeFreeFrontAutoTail"/> / Editor 负责。
/// </summary>
public static class AnimSpeedKnotBake
{
    public const float BreakKeyEpsilon = 1e-4f;
    const int SegmentSampleCount = 32;

    public static bool TrySolveTailAndBake(
        in AnimSpeedKnotTimeline timeline,
        out AnimSpeedKnotBakeResult result,
        out string error,
        float epsilon = AnimSpeedIntegralMath.DefaultEpsilon)
    {
        result = default;
        epsilon = Mathf.Max(0.0001f, epsilon);
        if (!timeline.TryValidate(out error))
        {
            return false;
        }

        var working = timeline;
        working.NormalizeContinuousJoins();

        var frontIntegral = IntegrateFront(working);
        var tStar = working.FrontEndTime;
        var tailLength = Mathf.Max(0f, 1f - tStar);
        var budget = 1f - frontIntegral;
        var overBudget = budget < -epsilon;

        // 关闭 AutoTail：前段权威，不再求解末段（积分过大时的正规路径）
        if (!working.AutoTailEnabled)
        {
            return BakeFrontOnly(
                working,
                frontIntegral,
                epsilon,
                out result,
                out error,
                warning: overBudget
                    ? $"AutoTail 已关闭；I_front={frontIntegral:F4} > 1+ε。仅 Bake 前段。"
                    : "AutoTail 已关闭；仅 Bake 前段（t*→1 保持末值）。");
        }

        // 仍开启 AutoTail 但前段已超预算 → 提示关闭，而非静默压扁
        if (overBudget)
        {
            error =
                $"前段积分过大 I_front={frontIntegral:F4} > 1+ε（ε={epsilon:F4}）；无 Tail 预算。\n" +
                "请取消勾选 AutoTail（此时无需自适应尾部），或降低前段倍率/缩短前段。";
            return false;
        }

        if (budget < 0f)
        {
            budget = 0f;
        }

        // 预算≈0：无需求解爆发尾
        if (budget <= epsilon)
        {
            return BakeFrontOnly(
                working,
                frontIntegral,
                epsilon,
                out result,
                out error,
                warning:
                $"I_front≈1（{frontIntegral:F4}），已跳过 AutoTail 求解；t*→1 保持末值。");
        }

        var tailStart = working.ResolveTailStartValue();
        if (!TrySolveTailEnd(working.TailSolveShape, tailStart, tailLength, budget, out var tailEnd, out error))
        {
            return false;
        }

        var curve = BuildCurve(working, tailStart, tailEnd);
        var total = AnimSpeedIntegralMath.IntegrateCurve(curve);
        if (!AnimSpeedIntegralMath.IsIntegralValid(total, epsilon))
        {
            error =
                $"Bake 后积分未收敛 I={total:F4}（ε={epsilon:F4} I_front={frontIntegral:F4} B={budget:F4} End={tailEnd:F3}）。";
            return false;
        }

        result = new AnimSpeedKnotBakeResult
        {
            Curve = curve,
            FrontIntegral = frontIntegral,
            TailBudget = budget,
            TailLength = tailLength,
            TailStart = tailStart,
            TailEnd = tailEnd,
            TotalIntegral = total,
            AutoTailUsed = true,
            Warning = null,
        };
        error = null;
        return true;
    }

    /// <summary>
    /// 仅 Bake 前段；若 t*&lt;1 则用前段末倍率保持到 t=1（不再求解 AutoTail End）。
    /// AutoTail 关闭或前段积分已够/过大时使用。
    /// </summary>
    static bool BakeFrontOnly(
        in AnimSpeedKnotTimeline timeline,
        float frontIntegral,
        float epsilon,
        out AnimSpeedKnotBakeResult result,
        out string error,
        string warning = null)
    {
        result = default;
        error = null;
        var tStar = timeline.FrontEndTime;
        var lastValue = timeline.KnotCount > 0
            ? Mathf.Max(0f, timeline.LeaveValues[timeline.KnotCount - 1])
            : 1f;
        // Break 在末结点：保持离开值
        if (timeline.KnotCount > 0
            && timeline.Joins[timeline.KnotCount - 1] == AnimSpeedJoinMode.Break)
        {
            lastValue = Mathf.Max(0f, timeline.LeaveValues[timeline.KnotCount - 1]);
        }
        else if (timeline.KnotCount > 0)
        {
            lastValue = Mathf.Max(0f, timeline.ArriveValues[timeline.KnotCount - 1]);
        }

        var curve = BuildFrontOnlyCurve(timeline, lastValue);
        var total = AnimSpeedIntegralMath.IntegrateCurve(curve);
        var warn = warning;
        if (!AnimSpeedIntegralMath.IsIntegralValid(total, epsilon))
        {
            warn = (string.IsNullOrEmpty(warn) ? "" : warn + "\n")
                   + $"I={total:F4} 超出 ε={epsilon:F4}（AutoTail 已跳过）。"
                   + "可调大 Integral ε，或降低前段倍率；Runtime 仍可能 REJECT。";
        }

        result = new AnimSpeedKnotBakeResult
        {
            Curve = curve,
            FrontIntegral = frontIntegral,
            TailBudget = Mathf.Max(0f, 1f - frontIntegral),
            TailLength = Mathf.Max(0f, 1f - tStar),
            TailStart = lastValue,
            TailEnd = lastValue,
            TotalIntegral = total,
            AutoTailUsed = false,
            Warning = warn,
        };
        return true;
    }

    public static float IntegrateFront(in AnimSpeedKnotTimeline timeline)
    {
        var sum = 0f;
        var segs = timeline.FrontSegmentCount;
        for (var i = 0; i < segs; i++)
        {
            var t0 = timeline.Times[i];
            var t1 = timeline.Times[i + 1];
            var v0 = Mathf.Max(0f, timeline.LeaveValues[i]);
            var v1 = Mathf.Max(0f, timeline.ArriveValues[i + 1]);
            var shape = timeline.SegmentShapes[i];
            sum += IntegrateShapedSegment(t0, t1, v0, v1, shape);
        }

        return sum;
    }

    public static float EvaluateShaped(float v0, float v1, AnimSpeedSegmentShapePreset shape, float u)
    {
        u = Mathf.Clamp01(u);
        var w = MapShape(shape, u);
        return Mathf.Lerp(v0, v1, w);
    }

    static float MapShape(AnimSpeedSegmentShapePreset shape, float u)
    {
        switch (shape)
        {
            case AnimSpeedSegmentShapePreset.EaseInFastOut:
                return u * u;
            case AnimSpeedSegmentShapePreset.FastInEaseOut:
                return 1f - (1f - u) * (1f - u);
            default:
                return u;
        }
    }

    static float IntegrateShapedSegment(
        float t0,
        float t1,
        float v0,
        float v1,
        AnimSpeedSegmentShapePreset shape)
    {
        var len = t1 - t0;
        if (len <= 0f)
        {
            return 0f;
        }

        if (shape == AnimSpeedSegmentShapePreset.Linear)
        {
            return (v0 + v1) * 0.5f * len;
        }

        var steps = SegmentSampleCount;
        var sum = 0f;
        var prevU = 0f;
        var prevV = EvaluateShaped(v0, v1, shape, 0f);
        for (var i = 1; i <= steps; i++)
        {
            var u = i / (float)steps;
            var v = EvaluateShaped(v0, v1, shape, u);
            sum += (prevV + v) * 0.5f * (u - prevU) * len;
            prevU = u;
            prevV = v;
        }

        return sum;
    }

    static bool TrySolveTailEnd(
        AnimSpeedTailSolveShape shape,
        float tailStart,
        float tailLength,
        float budget,
        out float tailEnd,
        out string error)
    {
        tailEnd = 0f;
        error = null;
        if (tailLength <= 1e-6f)
        {
            error = "Tail 长度过小。";
            return false;
        }

        var a = Mathf.Max(0f, tailStart);
        switch (shape)
        {
            case AnimSpeedTailSolveShape.Linear:
                // (a+c)·L/2 = B → c = 2B/L - a
                tailEnd = 2f * budget / tailLength - a;
                break;
            case AnimSpeedTailSolveShape.EaseInBurst:
                // v(u)=a+(c-a)u² → ∫ = L·(a + (c-a)/3) = B → c = 3B/L - 2a
                tailEnd = 3f * budget / tailLength - 2f * a;
                break;
            default:
                error = "未知 TailSolveShape。";
                return false;
        }

        if (tailEnd < 0f)
        {
            error = $"求解得负 TailEnd={tailEnd:F3}（a={a:F3} L={tailLength:F3} B={budget:F4}）。";
            return false;
        }

        return true;
    }

    static AnimationCurve BuildFrontOnlyCurve(in AnimSpeedKnotTimeline timeline, float holdValueToEnd)
    {
        var keys = new List<Keyframe>(timeline.KnotCount * 2 + 8);
        const int shapeSamples = 4;
        for (var seg = 0; seg < timeline.FrontSegmentCount; seg++)
        {
            var t0 = timeline.Times[seg];
            var t1 = timeline.Times[seg + 1];
            var v0 = Mathf.Max(0f, timeline.LeaveValues[seg]);
            var v1 = Mathf.Max(0f, timeline.ArriveValues[seg + 1]);
            var shape = timeline.SegmentShapes[seg];

            if (seg == 0)
            {
                AddKey(keys, t0, v0);
            }

            if (shape != AnimSpeedSegmentShapePreset.Linear)
            {
                for (var s = 1; s <= shapeSamples; s++)
                {
                    var u = s / (float)(shapeSamples + 1);
                    AddKey(keys, Mathf.Lerp(t0, t1, u), EvaluateShaped(v0, v1, shape, u));
                }
            }

            var arrive = v1;
            var leaveNext = Mathf.Max(0f, timeline.LeaveValues[seg + 1]);
            var join = timeline.Joins[seg + 1];
            if (join == AnimSpeedJoinMode.Break && Mathf.Abs(arrive - leaveNext) > 1e-5f)
            {
                AddKey(keys, Mathf.Max(0f, t1 - BreakKeyEpsilon), arrive);
                AddKey(keys, t1, leaveNext);
            }
            else
            {
                AddKey(keys, t1, arrive);
            }
        }

        var tStar = timeline.FrontEndTime;
        if (tStar < 1f - 1e-5f)
        {
            AddKey(keys, tStar, Mathf.Max(0f, holdValueToEnd));
            AddKey(keys, 1f, Mathf.Max(0f, holdValueToEnd));
        }
        else
        {
            AddKey(keys, 1f, Mathf.Max(0f, holdValueToEnd));
        }

        var curve = new AnimationCurve(keys.ToArray());
        for (var i = 0; i < curve.length; i++)
        {
            SetLinearTangents(curve, i);
        }

        return curve;
    }

    static AnimationCurve BuildCurve(in AnimSpeedKnotTimeline timeline, float tailStart, float tailEnd)
    {
        var keys = new List<Keyframe>(timeline.KnotCount * 2 + 16);
        var k = timeline.KnotCount;
        const int shapeSamples = 4;

        for (var seg = 0; seg < timeline.FrontSegmentCount; seg++)
        {
            var t0 = timeline.Times[seg];
            var t1 = timeline.Times[seg + 1];
            var v0 = Mathf.Max(0f, timeline.LeaveValues[seg]);
            var v1 = Mathf.Max(0f, timeline.ArriveValues[seg + 1]);
            var shape = timeline.SegmentShapes[seg];

            if (seg == 0)
            {
                AddKey(keys, t0, v0);
            }

            if (shape != AnimSpeedSegmentShapePreset.Linear)
            {
                for (var s = 1; s <= shapeSamples; s++)
                {
                    var u = s / (float)(shapeSamples + 1);
                    AddKey(keys, Mathf.Lerp(t0, t1, u), EvaluateShaped(v0, v1, shape, u));
                }
            }

            var arrive = v1;
            var leaveNext = Mathf.Max(0f, timeline.LeaveValues[seg + 1]);
            var join = timeline.Joins[seg + 1];
            if (join == AnimSpeedJoinMode.Break && Mathf.Abs(arrive - leaveNext) > 1e-5f)
            {
                AddKey(keys, Mathf.Max(0f, t1 - BreakKeyEpsilon), arrive);
                AddKey(keys, t1, leaveNext);
            }
            else
            {
                AddKey(keys, t1, arrive);
            }
        }

        var tStar = timeline.FrontEndTime;
        if (timeline.TailJoinFromFront == AnimSpeedJoinMode.Break
            && Mathf.Abs(timeline.LeaveValues[k - 1] - tailStart) > 1e-5f)
        {
            AddKey(keys, tStar, Mathf.Max(0f, timeline.LeaveValues[k - 1]));
            AddKey(keys, Mathf.Min(1f, tStar + BreakKeyEpsilon), Mathf.Max(0f, tailStart));
        }
        else
        {
            // Continuous：t* 已由前段末键写入；确保 TailStart 一致
            AddKey(keys, tStar, Mathf.Max(0f, tailStart));
        }

        if (timeline.TailSolveShape == AnimSpeedTailSolveShape.EaseInBurst)
        {
            for (var i = 1; i <= shapeSamples; i++)
            {
                var u = i / (float)(shapeSamples + 1);
                AddKey(keys, Mathf.Lerp(tStar, 1f, u), Mathf.Lerp(tailStart, tailEnd, u * u));
            }
        }

        AddKey(keys, 1f, Mathf.Max(0f, tailEnd));

        var curve = new AnimationCurve(keys.ToArray());
        for (var i = 0; i < curve.length; i++)
        {
            SetLinearTangents(curve, i);
        }

        return curve;
    }

    static void AddKey(List<Keyframe> keys, float time, float value)
    {
        time = Mathf.Clamp01(time);
        value = Mathf.Max(0f, value);
        if (keys.Count > 0)
        {
            var last = keys[keys.Count - 1];
            if (Mathf.Abs(last.time - time) < 1e-6f && Mathf.Abs(last.value - value) < 1e-5f)
            {
                return;
            }
        }

        keys.Add(new Keyframe(time, value));
    }

    static void SetLinearTangents(AnimationCurve curve, int index)
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
        if (index <= 0)
        {
            return;
        }

        var prev = curve[index - 1];
        var dt = key.time - prev.time;
        var slope = dt > 1e-6f ? (key.value - prev.value) / dt : 0f;
        prev.outTangent = slope;
        key.inTangent = slope;
        curve.MoveKey(index - 1, prev);
        curve.MoveKey(index, key);
    }
}
