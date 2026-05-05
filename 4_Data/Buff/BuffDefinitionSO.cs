using UnityEngine;

[CreateAssetMenu(fileName = "BuffDefinition", menuName = "GameMain/Buff/Buff Definition")]
public class BuffDefinitionSO : ScriptableObject
{
    [Min(0f)] public float Duration = 5f;
    [Min(0f)] public float PeriodSeconds;
    public BuffEffectEntry[] Effects;

    [Header("Periodic (optional)")]
    [Tooltip("启用后在每次周期触发时执行资源变化（可用于 DOT / HOT）。")]
    public bool ApplyPeriodicResourceDelta;

    [Tooltip("周期作用目标资源。DOT 默认选 HP。")]
    public ResourceType PeriodicResource = ResourceType.HP;

    [Tooltip("每跳变化值：正数=Drain，负数=Refill。")]
    public float PeriodicAmount = 5f;
}
