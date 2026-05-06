using UnityEngine;

/// <summary>
/// 假人配置数据。
/// </summary>
[CreateAssetMenu(menuName = "Testing/Combat/Test Dummy Config", fileName = "TestDummyConfig")]
public class TestDummyConfigSO : ScriptableObject
{
    [Header("Base Stats | 基础属性")]
    [Tooltip("baseStats（基础属性列表）：定义 MaxHealth/Defense 等初始值。")]
    public StatBaseEntry[] baseStats;

    [Header("Faction | 阵营")]
    [Tooltip("faction（阵营）：用于陷阱过滤与炮台选敌。")]
    public FactionTag faction = FactionTag.Enemy;

    [Header("Behavior | 行为")]
    [Tooltip("autoRegenerate（自动回血）：开启后脱战自动恢复 HP。")]
    public bool autoRegenerate;

    [Tooltip("regenDelay（回血延迟秒）：受击后等待多久开始回血。")]
    public float regenDelay = 3f;

    [Tooltip("regenRate（每秒回血量）：增大=恢复更快。")]
    public float regenRate = 50f;

    [Header("Debug | 调试")]
    [Tooltip("logDamageToConsole（打印受击日志）：用于核对结算结果。")]
    public bool logDamageToConsole = true;
}
