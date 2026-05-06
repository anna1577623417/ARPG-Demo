using System.Collections.Generic;

/// <summary>
/// 并行执行一个阶段内全部 <see cref="AbilityTask"/>。
/// </summary>
public sealed class TaskExecutor
{
    readonly List<AbilityTask> _activeTasks = new List<AbilityTask>(8);

    public bool HasTasks => _activeTasks.Count > 0;

    public void StartAll(AbilityTaskSO[] taskDefs, SkillContext ctx)
    {
        ExitAll(ctx);
        if (taskDefs == null)
        {
            return;
        }

        for (var i = 0; i < taskDefs.Length; i++)
        {
            var def = taskDefs[i];
            if (def == null)
            {
                continue;
            }

            var task = def.CreateInstance();
            task.OnEnter(ctx);
            _activeTasks.Add(task);
        }
    }

    public void UpdateAll(SkillContext ctx, float deltaTime)
    {
        for (var i = _activeTasks.Count - 1; i >= 0; i--)
        {
            if (_activeTasks[i].Status == AbilityTask.TaskStatus.Running)
            {
                _activeTasks[i].OnUpdate(ctx, deltaTime);
            }
        }
    }

    public void BroadcastWindowSignal(SkillContext ctx, ActionWindowRuntimeEventKind kind)
    {
        foreach (var task in _activeTasks)
        {
            if (task.Status == AbilityTask.TaskStatus.Running)
            {
                task.OnWindowSignal(ctx, kind);
            }
        }
    }

    public void ExitAll(SkillContext ctx)
    {
        foreach (var task in _activeTasks)
        {
            task.OnExit(ctx);
        }

        _activeTasks.Clear();
    }

    public bool AllCompleted()
    {
        foreach (var task in _activeTasks)
        {
            if (task.Status == AbilityTask.TaskStatus.Running)
            {
                return false;
            }
        }

        return true;
    }

    public bool AnyFailed()
    {
        foreach (var task in _activeTasks)
        {
            if (task.Status == AbilityTask.TaskStatus.Failed)
            {
                return true;
            }
        }

        return false;
    }
}
