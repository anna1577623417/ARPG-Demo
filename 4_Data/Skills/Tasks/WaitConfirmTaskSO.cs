using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Wait Confirm")]
public sealed class WaitConfirmTaskSO : AbilityTaskSO
{
    public bool cancelOnTimeout;
    public float timeout = 10f;

    public override AbilityTask CreateInstance() => new WaitConfirmTask(this);
}
