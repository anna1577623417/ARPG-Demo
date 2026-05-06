using UnityEngine;

public sealed class DashTask : AbilityTask
{
    readonly DashTaskSO m_so;
    float m_elapsed;
    Vector3 m_dir;
    float m_lastSample;

    public DashTask(DashTaskSO so) => m_so = so;

    public override void OnEnter(SkillContext ctx)
    {
        m_elapsed = 0f;
        m_lastSample = 0f;
        m_dir = ctx.SelfTransform != null ? ctx.SelfTransform.forward : Vector3.forward;
        m_dir.y = 0f;
        if (m_dir.sqrMagnitude < 1e-8f)
        {
            m_dir = Vector3.forward;
        }

        m_dir.Normalize();
    }

    public override void OnUpdate(SkillContext ctx, float deltaTime)
    {
        var dur = Mathf.Max(0.0001f, m_so.duration);
        m_elapsed += deltaTime;
        var t = Mathf.Clamp01(m_elapsed / dur);
        var curve = m_so.strengthByNorm != null ? m_so.strengthByNorm : AnimationCurve.Linear(0f, 1f, 1f, 0f);
        var s0 = curve.Evaluate(m_lastSample);
        var s1 = curve.Evaluate(t);
        m_lastSample = t;
        var deltaS = Mathf.Max(0f, s1 - s0);

        var planar = m_dir * (m_so.distance * deltaS);
        if (ctx.Self is Player p)
        {
            p.SetPlanarVelocity(planar / Mathf.Max(deltaTime, 1e-6f));
        }

        if (t >= 1f)
        {
            if (ctx.Self is Player pb)
            {
                pb.StopMove();
            }

            Complete();
        }
    }

    public override void OnExit(SkillContext ctx)
    {
        if (ctx.Self is Player pb)
        {
            pb.StopMove();
        }
    }
}
