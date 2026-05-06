public sealed class ModifyCooldownTask : AbilityTask
{
    readonly ModifyCooldownTaskSO m_so;

    public ModifyCooldownTask(ModifyCooldownTaskSO so) => m_so = so;

    public override void OnWindowSignal(SkillContext ctx, ActionWindowRuntimeEventKind windowKind)
    {
        if (windowKind != ActionWindowRuntimeEventKind.HitFrame)
        {
            return;
        }

        if (ctx.Self is not Player p)
        {
            Fail();
            return;
        }

        if (!p.TryGetSkillRuntime(m_so.targetSlot, out var rt))
        {
            Fail();
            return;
        }

        rt.ModifyCooldown(m_so.op, m_so.value);
        Complete();
    }
}
