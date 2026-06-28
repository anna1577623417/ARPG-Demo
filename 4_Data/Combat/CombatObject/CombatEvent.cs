using System;
using UnityEngine;

/// <summary>
/// 188.3 W7 — 时间轴上的 CombatObject 生成事件（嵌入 ActionDataSO.CombatTrack）。
/// </summary>
[Serializable]
public struct CombatEvent
{
    [Tooltip("Action 时间轴上的归一化时刻（0~1）。穿越此时刻时 Spawn 一次。")]
    [Range(0f, 1f)] public float NormalizedTime;

    [Tooltip("生成的 CombatObject 定义资产。空 = 跳过。")]
    public CombatObjectDefinitionSO Definition;

    [Header("Spawn Override (可选)")]
    [Tooltip("勾选后用本字段覆盖 Definition.SpawnSource/Offset；用于不同 Action 复用同一 Definition。")]
    public bool OverrideSpawn;

    public SpawnSource SpawnSourceOverride;
    public Vector3 LocalOffsetOverride;
    public Vector3 LocalEulerOffsetOverride;

    [Tooltip("调试标签（Console 日志显示）。")]
    public string DebugLabel;
}
