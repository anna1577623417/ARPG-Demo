using UnityEngine;

/// <summary>Normal Route 运行时 — 最简单的"按下即释放"，遵循基类默认 Transition 推进。</summary>
public sealed class NormalRouteRuntime : SkillRouteRuntime
{
    public override RouteKind Kind => RouteKind.Normal;

    public override void OnEnter(in SkillRouteContext ctx)
    {
        base.OnEnter(in ctx);
    }

    public override void OnTick(in SkillRouteContext ctx)
    {
        base.OnTick(in ctx);
        var wasActive = IsActive;
        TryEndRouteWhenLastStageComplete(in ctx);
        if (Stage != null && Stage.Completed && wasActive != IsActive)
        {
            CombatGraphFinisherDiagnostics.LogStageComplete(ctx.Self as Player, this, Stage, IsActive);
        }
    }
}
