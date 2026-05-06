using UnityEngine;

/// <summary>占位：需在输入层派发确认语义后完成任务。</summary>
public sealed class WaitConfirmTask : AbilityTask
{
    readonly WaitConfirmTaskSO m_so;
    float m_elapsed;

    public WaitConfirmTask(WaitConfirmTaskSO so) => m_so = so;

    public override void OnEnter(SkillContext ctx)
    {
        m_elapsed = 0f;
    }

    public override void OnUpdate(SkillContext ctx, float deltaTime)
    {
        if (m_so.cancelOnTimeout)
        {
            m_elapsed += deltaTime;
            if (m_so.timeout > 0f && m_elapsed >= m_so.timeout)
            {
                Fail();
            }
        }
    }
}
