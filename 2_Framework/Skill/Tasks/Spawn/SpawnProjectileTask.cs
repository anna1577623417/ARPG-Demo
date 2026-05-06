using UnityEngine;

public sealed class SpawnProjectileTask : AbilityTask
{
    readonly SpawnProjectileTaskSO m_so;

    public SpawnProjectileTask(SpawnProjectileTaskSO so) => m_so = so;

    public override void OnWindowSignal(SkillContext ctx, ActionWindowRuntimeEventKind windowKind)
    {
        if (windowKind != ActionWindowRuntimeEventKind.HitFrame)
        {
            return;
        }

        if (m_so.projectilePrefab == null || ctx?.SelfTransform == null)
        {
            Fail();
            return;
        }

        var forward = ctx.SelfTransform.forward;
        var pos = ctx.SelfTransform.TransformPoint(m_so.spawnOffset);
        var go = Object.Instantiate(m_so.projectilePrefab, pos, Quaternion.LookRotation(forward, Vector3.up));
        var mover = go.GetComponent<SkillStraightProjectile>();
        if (mover == null)
        {
            mover = go.AddComponent<SkillStraightProjectile>();
        }

        var radius = SkillModifierShapes.ScaleCollisionRadius(m_so.collisionRadius, ctx.FinalModifiers);

        mover.Configure(new SkillStraightProjectileSpawn
        {
            Context = ctx,
            Direction = forward,
            Speed = m_so.speed,
            LifetimeSeconds = m_so.lifetime,
            CollisionRadius = radius,
        });

        Complete();
    }
}
