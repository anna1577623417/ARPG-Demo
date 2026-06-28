using UnityEngine;

/// <summary>
/// 179.1 §6 — Tier 自动切换（迟滞带防抖动）。
/// </summary>
public static class TierResolver
{
    /// <summary>给定当前 Tier 与速度，决定下一帧应处于哪个 Tier。</summary>
    public static LoopTier Resolve(float speed, LoopTier current, LocomotionRuntimeProfileSO cfg)
    {
        if (cfg == null) return current;
        var h = cfg.HysteresisFraction;

        switch (current)
        {
            case LoopTier.Walk:
                if (speed > cfg.WalkRange.y * (1f + h)) return LoopTier.Run;
                return LoopTier.Walk;

            case LoopTier.Run:
                if (speed < cfg.RunRange.x * (1f - h)) return LoopTier.Walk;
                if (speed > cfg.RunRange.y * (1f + h)) return LoopTier.Sprint;
                return LoopTier.Run;

            case LoopTier.Sprint:
                if (speed < cfg.SprintRange.x * (1f - h)) return LoopTier.Run;
                return LoopTier.Sprint;

            default:
                return current;
        }
    }
}
