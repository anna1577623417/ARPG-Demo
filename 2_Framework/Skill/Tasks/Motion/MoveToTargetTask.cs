using UnityEngine;

public sealed class MoveToTargetTask : AbilityTask
{
    readonly MoveToTargetTaskSO m_so;

    public MoveToTargetTask(MoveToTargetTaskSO so) => m_so = so;

    public override void OnUpdate(SkillContext ctx, float deltaTime)
    {
        if (ctx.Self is not Player p || ctx.SelfTransform == null)
        {
            Fail();
            return;
        }

        var targetPos = ResolveTargetWorld(ctx);
        var toFlat = Vector3.ProjectOnPlane(targetPos - ctx.Self.Position, Vector3.up);
        var dist = toFlat.magnitude;
        if (dist <= Mathf.Max(0.05f, m_so.stopDistance))
        {
            p.StopMove();
            Complete();
            return;
        }

        if (!m_so.allowTracking)
        {
            return;
        }

        if (dist > m_so.maxChaseRange)
        {
            Fail();
            return;
        }

        var dir = toFlat / dist;
        p.SetPlanarVelocity(dir * m_so.speed);
    }

    Vector3 ResolveTargetWorld(SkillContext ctx)
    {
        if (ctx.Target.Entity != null)
        {
            return ctx.Target.Entity.Position;
        }

        return ctx.Target.Position != default ? ctx.Target.Position : ctx.Self.Position + ctx.Self.Forward * 5f;
    }
}
