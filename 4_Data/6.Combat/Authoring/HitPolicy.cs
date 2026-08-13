using System;
using UnityEngine;

/// <summary>
/// 216.3 M2 — 命中策略种类（单一真相：命中几次 / 何时可再命中）。
/// </summary>
public enum HitPolicyKind : byte
{
    /// <summary>每目标一次（默认近战；与 M1 行为对齐）。</summary>
    PerTarget = 0,

    /// <summary>整个 Active 只命中一次（任意目标命中后整窗锁定）。</summary>
    Single = 1,

    /// <summary>每次挥击每目标一次；Contact 窗口重开时由 <c>HitRegistry.ResetSwing</c> 重置。</summary>
    PerSwing = 2,

    /// <summary>每 <see cref="HitPolicyParams.IntervalSeconds"/> 秒开一窗，窗内每目标一次。</summary>
    Interval = 3,

    /// <summary>逐帧可命中（持续伤害）；受 MaxHitsPerTarget / MaxTargets 上限约束。</summary>
    Continuous = 4,

    /// <summary>每目标最多 <see cref="HitPolicyParams.MaxHitsPerTarget"/> 次。</summary>
    Multi = 5,
}

/// <summary>
/// Contact 命中策略参数。
/// </summary>
[Serializable]
public struct HitPolicyParams
{
    [Tooltip("命中策略。默认 PerTarget（每目标一次）。")]
    public HitPolicyKind Kind;

    [Tooltip("Interval 策略：两次开窗间隔（秒）。")]
    [Min(0.01f)]
    public float IntervalSeconds;

    [Tooltip("Multi / Continuous：每目标最大命中次数。PerTarget/PerSwing/Single 忽略（视为 1）。")]
    [Min(1)]
    public int MaxHitsPerTarget;

    [Tooltip("本 Active 内最多命中多少个不同目标。")]
    [Min(1)]
    public int MaxTargets;

    public static HitPolicyParams Default => new HitPolicyParams
    {
        Kind = HitPolicyKind.PerTarget,
        IntervalSeconds = 0.2f,
        MaxHitsPerTarget = 1,
        MaxTargets = 999,
    };

    public static HitPolicyParams MeleeSingle => new HitPolicyParams
    {
        Kind = HitPolicyKind.Single,
        IntervalSeconds = 0.2f,
        MaxHitsPerTarget = 1,
        MaxTargets = 1,
    };

    public static HitPolicyParams IntervalDot(float intervalSec) => new HitPolicyParams
    {
        Kind = HitPolicyKind.Interval,
        IntervalSeconds = Mathf.Max(0.01f, intervalSec),
        MaxHitsPerTarget = 999,
        MaxTargets = 999,
    };
}
