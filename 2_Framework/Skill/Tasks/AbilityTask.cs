/// <summary>
/// 技能阶段内可组合行为单元；由 <see cref="TaskExecutor"/> 驱动。
/// </summary>
public abstract class AbilityTask
{
    public enum TaskStatus
    {
        Running,
        Completed,
        Failed,
    }

    public TaskStatus Status { get; protected set; } = TaskStatus.Running;

    public virtual void OnEnter(SkillContext ctx) { }

    public virtual void OnUpdate(SkillContext ctx, float deltaTime) { }

    public virtual void OnExit(SkillContext ctx) { }

    public virtual void OnWindowSignal(SkillContext ctx, ActionWindowRuntimeEventKind windowKind) { }

    protected void Complete() => Status = TaskStatus.Completed;

    protected void Fail() => Status = TaskStatus.Failed;
}
