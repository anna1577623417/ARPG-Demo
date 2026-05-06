public sealed class WaitWindowSignalTask : AbilityTask
{
    readonly WaitWindowSignalTaskSO m_so;

    public WaitWindowSignalTask(WaitWindowSignalTaskSO so) => m_so = so;

    public override void OnWindowSignal(SkillContext ctx, ActionWindowRuntimeEventKind windowKind)
    {
        if (Status != TaskStatus.Running)
        {
            return;
        }

        if (windowKind == m_so.waitFor)
        {
            Complete();
        }
    }
}
