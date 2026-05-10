using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Move To Target")]
public sealed class MoveToTargetTaskSO : AbilityTaskSO
{
    public float speed = 8f;
    public float stopDistance = 1.2f;
    public float maxChaseRange = 20f;
    public bool allowTracking = true;

    public override AbilityTask CreateInstance() => new MoveToTargetTask(this);
}
