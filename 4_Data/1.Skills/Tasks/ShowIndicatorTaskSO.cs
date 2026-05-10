using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Show Indicator")]
public sealed class ShowIndicatorTaskSO : AbilityTaskSO
{
    [Tooltip("占位：运行时接入指示器Prefab / UI后可在此拉起实例。")]
    public GameObject indicatorPrefab;

    public override AbilityTask CreateInstance() => new ShowIndicatorTask(this);
}
