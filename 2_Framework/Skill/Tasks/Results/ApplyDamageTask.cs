using UnityEngine;

public sealed class ApplyDamageTask : AbilityTask
{
    static readonly Collider[] s_buffer = new Collider[24];

    readonly ApplyDamageTaskSO m_so;

    public ApplyDamageTask(ApplyDamageTaskSO so) => m_so = so;

    public override void OnWindowSignal(SkillContext ctx, ActionWindowRuntimeEventKind windowKind)
    {
        if (windowKind != ActionWindowRuntimeEventKind.HitFrame || ctx == null || ctx.Self == null)
        {
            return;
        }

        var self = ctx.Self;
        var tf = ctx.SelfTransform != null ? ctx.SelfTransform : self.transform;
        var pos = tf.position;
        var forward = tf.forward;

        var baseDmg = ctx.Runtime != null
            ? ctx.Runtime.GetScaledDamage(ctx.ResolvedSkillSheet) * m_so.damageMultiplier
            : 0f;
        baseDmg *= ctx.FinalModifiers.DamageScale;
        var r = Mathf.Max(0.01f, m_so.radius) * ctx.FinalModifiers.RadiusScale + ctx.FinalModifiers.RadiusAdd;

        var count = Physics.OverlapSphereNonAlloc(
            pos + forward * (r * 0.35f),
            r,
            s_buffer,
            m_so.hitMask,
            QueryTriggerInteraction.Collide);

        var srcGo = tf.gameObject;

        for (var i = 0; i < count; i++)
        {
            var col = s_buffer[i];
            if (col == null)
            {
                continue;
            }

            var victim = col.GetComponentInParent<IDamageable>();
            if (victim == null)
            {
                continue;
            }

            var ent = col.GetComponentInParent<Entity>();
            if (ent != null && ent == self)
            {
                continue;
            }

            victim.TakeDamage(new DamageInfo
            {
                Amount = Mathf.Max(0f, baseDmg),
                HitPoint = col.bounds.center,
                Force = Vector3.zero,
                Source = srcGo,
            });
        }
    }
}
