/// <summary>
/// Derivative Route 运行时 — 派生招（闪避反击、处决等）。
///
/// 解锁 / 触发由 SkillEntryService 在父 Route Active 期间检查；本 Runtime 只在被 Resolve 后承担"释放"。
/// </summary>
public sealed class DerivativeRouteRuntime : SkillRouteRuntime
{
    public override RouteKind Kind => RouteKind.Derivative;

    public override void OnEnter(in SkillRouteContext ctx)
    {
        base.OnEnter(in ctx);

        var def = Definition as DerivativeRouteDefinition;
        if (def == null) return;

        // ShareParentCd / InheritResource：减少额外扣减
        if ((def.Flags & DerivativeFlags.ShareParentCd) != 0)
        {
            SuppressNextCooldown = true;
        }
        // InheritResource 已在父 Route 的 ConsumeRouteCosts 时考虑（这里若 ConsumeOnlyOnHit 也已挡掉）
    }

    public override void OnTick(in SkillRouteContext ctx)
    {
        base.OnTick(in ctx);
        TryEndRouteWhenLastStageComplete(in ctx);
    }
}
