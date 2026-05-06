using UnityEngine;

public sealed class SpawnSummonTask : AbilityTask
{
    readonly SpawnSummonTaskSO m_so;

    public SpawnSummonTask(SpawnSummonTaskSO so) => m_so = so;

    public override void OnWindowSignal(SkillContext ctx, ActionWindowRuntimeEventKind windowKind)
    {
        if (windowKind != ActionWindowRuntimeEventKind.HitFrame)
        {
            return;
        }

        if (m_so.summonPrefab == null || ctx?.SelfTransform == null)
        {
            Fail();
            return;
        }

        Object.Instantiate(
            m_so.summonPrefab,
            ctx.SelfTransform.TransformPoint(m_so.localOffset),
            ctx.SelfTransform.rotation);
        Complete();
    }
}
