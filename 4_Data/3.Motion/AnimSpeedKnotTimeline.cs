using System;
using UnityEngine;

/// <summary>
/// 共享结点时间轴快照（228）：权威在 MotionProfileSO 序列化字段；本结构供读写/校验/Bake。
/// 禁止每段独立全局 Start+End 双写。
/// </summary>
[Serializable]
public struct AnimSpeedKnotTimeline
{
    public const float DefaultMinSegmentLength = 0.01f;
    public const float DefaultTailMinLength = 0.05f;
    public const int SoftMaxFrontSegments = 16;

    public float[] Times;
    public float[] ArriveValues;
    public float[] LeaveValues;
    public AnimSpeedJoinMode[] Joins;
    public AnimSpeedSegmentShapePreset[] SegmentShapes;
    public AnimSpeedTailSolveShape TailSolveShape;
    public AnimSpeedJoinMode TailJoinFromFront;
    public float TailStartValue;
    public float TailMinLength;

    /// <summary>
    /// 是否启用自适应 AutoTail。关闭后前段可占满 [0,1]，不再求解末段 End。
    /// 前段积分已 ≥1 时通常应关闭。
    /// </summary>
    public bool AutoTailEnabled;

    public int KnotCount => Times != null ? Times.Length : 0;
    public int FrontSegmentCount => Mathf.Max(0, KnotCount - 1);
    public float FrontEndTime => KnotCount > 0 ? Times[KnotCount - 1] : 0f;

    public static AnimSpeedKnotTimeline CreateDefault()
    {
        return new AnimSpeedKnotTimeline
        {
            Times = new[] { 0f, 0.5f },
            ArriveValues = new[] { 1f, 1f },
            LeaveValues = new[] { 1f, 1f },
            Joins = new[] { AnimSpeedJoinMode.Continuous, AnimSpeedJoinMode.Continuous },
            SegmentShapes = new[] { AnimSpeedSegmentShapePreset.Linear },
            TailSolveShape = AnimSpeedTailSolveShape.Linear,
            TailJoinFromFront = AnimSpeedJoinMode.Continuous,
            TailStartValue = 1f,
            TailMinLength = DefaultTailMinLength,
            AutoTailEnabled = true,
        };
    }

    public bool TryValidate(out string error, float minSegmentLength = DefaultMinSegmentLength)
    {
        error = null;
        if (Times == null || ArriveValues == null || LeaveValues == null || Joins == null || SegmentShapes == null)
        {
            error = "结点数组为 null。";
            return false;
        }

        var k = Times.Length;
        if (k < 2)
        {
            error = "至少需要 2 个结点（t=0 与 t*）。";
            return false;
        }

        if (ArriveValues.Length != k || LeaveValues.Length != k || Joins.Length != k)
        {
            error = $"结点数组长度不一致 Times={k} Arrive={ArriveValues.Length} Leave={LeaveValues.Length} Joins={Joins.Length}。";
            return false;
        }

        if (SegmentShapes.Length != k - 1)
        {
            error = $"段形状数应为 KnotCount-1={k - 1}，实际 {SegmentShapes.Length}。";
            return false;
        }

        if (FrontSegmentCount > SoftMaxFrontSegments)
        {
            error = $"前段数 {FrontSegmentCount} 超过软上限 {SoftMaxFrontSegments}。";
            return false;
        }

        var lMin = Mathf.Max(0.001f, TailMinLength);
        if (Times[0] > 1e-4f)
        {
            error = $"首结点时间须为 0，实际 {Times[0]:F4}。";
            return false;
        }

        for (var i = 0; i < k; i++)
        {
            if (ArriveValues[i] < 0f || LeaveValues[i] < 0f)
            {
                error = $"结点[{i}] 倍率不可为负。";
                return false;
            }

            if (Joins[i] == AnimSpeedJoinMode.Continuous
                && Mathf.Abs(ArriveValues[i] - LeaveValues[i]) > 1e-4f)
            {
                error = $"结点[{i}] Continuous 要求 Arrive==Leave（{ArriveValues[i]:F3}≠{LeaveValues[i]:F3}）。";
                return false;
            }
        }

        for (var i = 1; i < k; i++)
        {
            var dt = Times[i] - Times[i - 1];
            if (dt < minSegmentLength)
            {
                error = $"段[{i - 1}] 长度 {dt:F4} < δ={minSegmentLength:F4}。";
                return false;
            }
        }

        var tStar = Times[k - 1];
        if (AutoTailEnabled)
        {
            if (tStar > 1f - lMin + 1e-5f)
            {
                error = $"t*={tStar:F4} 使 Tail 长度 < L_min={lMin:F4}。可关闭 AutoTail 以允许前段占满到 1。";
                return false;
            }
        }
        else if (tStar > 1f + 1e-5f)
        {
            error = $"AutoTail 已关闭时末结点时间不可 >1，实际 {tStar:F4}。";
            return false;
        }

        if (TailStartValue < 0f)
        {
            error = "TailStartValue 不可为负。";
            return false;
        }

        return true;
    }

    /// <summary>Continuous 结点将 Leave 对齐 Arrive（规范化，不改时间）。</summary>
    public void NormalizeContinuousJoins()
    {
        if (Times == null || ArriveValues == null || LeaveValues == null || Joins == null)
        {
            return;
        }

        var k = Mathf.Min(Times.Length, Mathf.Min(ArriveValues.Length, Mathf.Min(LeaveValues.Length, Joins.Length)));
        for (var i = 0; i < k; i++)
        {
            if (Joins[i] == AnimSpeedJoinMode.Continuous)
            {
                LeaveValues[i] = ArriveValues[i];
            }
        }
    }

    public float ResolveTailStartValue()
    {
        if (KnotCount == 0)
        {
            return Mathf.Max(0f, TailStartValue);
        }

        if (TailJoinFromFront == AnimSpeedJoinMode.Continuous)
        {
            return Mathf.Max(0f, LeaveValues[KnotCount - 1]);
        }

        return Mathf.Max(0f, TailStartValue);
    }
}
