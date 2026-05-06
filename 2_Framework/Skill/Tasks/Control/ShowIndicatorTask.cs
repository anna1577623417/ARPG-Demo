/// <summary>占位指示器模块：完整流程需 Presentation 层接驳。</summary>
public sealed class ShowIndicatorTask : AbilityTask
{
    public ShowIndicatorTask(ShowIndicatorTaskSO so)
    {
        _ = so;
    }

    public override void OnEnter(SkillContext ctx)
    {
        Complete();
    }
}
