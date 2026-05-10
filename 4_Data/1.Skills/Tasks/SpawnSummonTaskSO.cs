using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Task/Spawn Summon")]
public sealed class SpawnSummonTaskSO : AbilityTaskSO
{
    public GameObject summonPrefab;
    public Vector3 localOffset;

    public override AbilityTask CreateInstance() => new SpawnSummonTask(this);
}
