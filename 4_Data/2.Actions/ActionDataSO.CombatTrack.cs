using System;
using UnityEngine;

/// <summary>
/// 188.3 W7 — ActionDataSO 的 Combat Track partial。
/// <para>平行于既有 Windows[].RuntimeEvents 旧通道；策划新做的攻击全走 CombatTrack。</para>
/// <para>默认空数组 → 旧 Action 资产行为完全不变。</para>
/// </summary>
public partial class ActionDataSO
{
    [Header("Combat Track (188.3)")]
    [Tooltip("CombatObject 生成事件列表。\n" +
             "时间轴穿越 NormalizedTime 时 Spawn 对应 CombatObjectDefinitionSO；\n" +
             "由 CombatObjectRuntime 自己持续判定（Movement+Shape+Damage+Lifecycle）。")]
    public CombatEvent[] CombatTrack = Array.Empty<CombatEvent>();
}
