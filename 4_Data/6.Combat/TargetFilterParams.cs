using System;
using UnityEngine;

/// <summary>
/// 214.4 — CombatObject 目标过滤（纯数据 struct；裁决逻辑见 TargetFilterEvaluator）。
/// </summary>
[Serializable]
public struct TargetFilterParams
{
    public TargetFilterKind Kind;

    [Tooltip("IncludeDead=true 时允许命中已死亡 Entity。")]
    public bool IncludeDead;

    public static TargetFilterParams Default => new TargetFilterParams
    {
        Kind = TargetFilterKind.AnyExceptSelf,
        IncludeDead = false,
    };

    public static TargetFilterParams Hostile => new TargetFilterParams
    {
        Kind = TargetFilterKind.HostileOnly,
        IncludeDead = false,
    };
}
