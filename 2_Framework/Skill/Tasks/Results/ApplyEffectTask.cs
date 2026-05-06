using UnityEngine;

public sealed class ApplyEffectTask : AbilityTask
{
    readonly ApplyEffectTaskSO m_so;

    public ApplyEffectTask(ApplyEffectTaskSO so) => m_so = so;

    Entity ResolveVictim(SkillContext ctx)
    {
        switch (m_so.applyTo)
        {
            case TargetType.SingleTarget when ctx.Target.Entity != null:
                return ctx.Target.Entity;
            default:
                return ctx.Self;
        }
    }

    public override void OnWindowSignal(SkillContext ctx, ActionWindowRuntimeEventKind windowKind)
    {
        if (windowKind != ActionWindowRuntimeEventKind.HitFrame || m_so.buff == null || ctx?.Self == null)
        {
            return;
        }

        var victim = ResolveVictim(ctx);
        if (victim == null || victim.IsDead)
        {
            Fail();
            return;
        }

        victim.Buffs?.Apply(m_so.buff, ctx.Self ?? victim);
        Complete();
    }
}
