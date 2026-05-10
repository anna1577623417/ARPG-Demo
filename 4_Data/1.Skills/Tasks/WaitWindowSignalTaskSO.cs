using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Wait Window Signal")]
public sealed class WaitWindowSignalTaskSO : AbilityTaskSO
{
    public ActionWindowRuntimeEventKind waitFor = ActionWindowRuntimeEventKind.HitFrame;

    public override AbilityTask CreateInstance() => new WaitWindowSignalTask(this);
}
