using UnityEngine;

/// <summary>Normal Route 运行时 — 最简单的"按下即释放"，遵循基类默认 Transition 推进。</summary>
public sealed class NormalRouteRuntime : SkillRouteRuntime
{
    public override RouteKind Kind => RouteKind.Normal;

    public override void OnEnter(in SkillRouteContext ctx)
    {
        base.OnEnter(in ctx);
        if (ctx.Self is Player p && p.DebugSkillRoute)
        {
            Debug.Log(
                $"[Normal] OnEnter route={Definition?.name} stage={Stage?.Definition?.name ?? "<null>"} " +
                $"stageDur={Stage?.DurationSeconds:F2}s transitions={Stage?.Definition?.Transitions?.Length ?? 0}", p);
        }
    }

    public override void OnTick(in SkillRouteContext ctx)
    {
        base.OnTick(in ctx);
        var wasActive = IsActive;
        TryEndRouteWhenLastStageComplete(in ctx);
        if (wasActive && !IsActive && ctx.Self is Player p && p.DebugSkillRoute)
        {
            Debug.Log($"[Normal] LastStage完成 → IsActive=false route={Definition?.name}", p);
        }
    }
}
