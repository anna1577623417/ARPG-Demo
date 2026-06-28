using System;
using UnityEngine;

/// <summary>
/// 188.3 W5 — CombatObject 生命周期参数（嵌入 CombatObjectDefinitionSO）。
/// <para>5 字段表达：单击 / 多段 / DOT / 链式 / 嵌套爆炸 全部用同一套。</para>
/// </summary>
[Serializable]
public struct LifecycleParams
{
    [Tooltip("存活秒数。0=单帧；<0=∞（直到被外部销毁）。")]
    public float Duration;

    [Tooltip("周期触发间隔（秒）。0=瞬时一次；>0=每 N 秒触发一次 Damage（DOT/火焰地板）。")]
    [Min(0f)] public float TickInterval;

    [Tooltip("每个目标最多打几次。1=单击；>=2=多段；DOT 设 999。")]
    [Min(1)] public int MaxHitsPerTarget;

    [Tooltip("整个 CombatObject 最多打几个目标。1=单体追踪；999=全场 AOE。")]
    [Min(1)] public int MaxTargets;

    [Tooltip("过期时再 Spawn 子 CombatObject（爆炸 → 火焰地板）。链深由 Spawner 内部限制 ≤ 3。")]
    public CombatObjectDefinitionSO OnExpireSpawn;

    /// <summary>近战一次性默认值（Duration=0.1s/Tick=0/MaxHits=1/MaxTargets=999）。</summary>
    public static LifecycleParams DefaultMeleeOneShot => new()
    {
        Duration = 0.1f,
        TickInterval = 0f,
        MaxHitsPerTarget = 1,
        MaxTargets = 999,
    };

    /// <summary>DOT 默认值（Duration=5s/Tick=0.5s/MaxHits=999/MaxTargets=999）。</summary>
    public static LifecycleParams DefaultDot(float duration = 5f, float tick = 0.5f) => new()
    {
        Duration = duration,
        TickInterval = tick,
        MaxHitsPerTarget = 999,
        MaxTargets = 999,
    };
}
