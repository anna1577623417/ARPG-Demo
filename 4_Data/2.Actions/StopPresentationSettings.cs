using System;
using UnityEngine;

/// <summary>
/// 238.1 — Stop 对 Action 时间与 Clip 基准倍率的显式覆盖。
/// 动态速度、距离和时长仍只存在于 StopRuntimeContext，不写回本结构。
/// </summary>
[Serializable]
public struct StopPresentationSettings
{
    [Tooltip("Stop 会话有效时长来源。LegacyLease 保持 234.6 兼容；PhysicsStop 用物理完成时长。")]
    public StopDurationAuthority DurationAuthority;

    [Tooltip("Stop Clip 基准倍率来源。InheritAction 在 LegacyLease 下保持旧行为；AutoFit 用有效时长反算。")]
    public StopAnimSpeedAuthority AnimSpeedAuthority;

    [Min(0.01f)]
    [Tooltip("AnimSpeedAuthority=FixedOverride 时使用的 Clip 基准倍率。")]
    public float FixedAnimSpeed;

    [Tooltip("要求 PhysicsStop 与 Clip 窗口严格同钟；FixedOverride 不一致时回退 AutoFit 并报告 Rejected。")]
    public bool RequireSynchronization;

    public static StopPresentationSettings Default => new()
    {
        DurationAuthority = StopDurationAuthority.LegacyLease,
        AnimSpeedAuthority = StopAnimSpeedAuthority.InheritAction,
        FixedAnimSpeed = 1f,
        RequireSynchronization = false,
    };

    public float ResolveFixedAnimSpeed() => Mathf.Max(0.01f, FixedAnimSpeed);
}
