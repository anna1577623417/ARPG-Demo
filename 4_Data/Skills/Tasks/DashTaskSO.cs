using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Dash")]
public sealed class DashTaskSO : AbilityTaskSO
{
    public float distance = 4f;
    public float duration = 0.12f;
    public AnimationCurve strengthByNorm = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    public override AbilityTask CreateInstance() => new DashTask(this);
}
