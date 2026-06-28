using System;
using UnityEngine;

/// <summary>
/// 180.1 §2.2 — 统一 Effect 资产（替代旧 BuffDefinitionSO；旧资产兼容路径见 W0 迁移工具）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Effect/Effect Definition", fileName = "Effect_")]
public sealed class EffectDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string Id;
    public string DisplayName;
    public Sprite Icon;
    [TextArea] public string Description;

    [Header("Source / Category")]
    public EffectSourceKind SourceKind = EffectSourceKind.Skill;
    public EffectCategory Category = EffectCategory.Buff;
    public GameplayTagMask SelfTags;
    public GameplayTagMask ExclusiveWith;

    [Header("Timing")]
    public DurationPolicy Duration = DurationPolicy.Timed;
    [Min(0f)] public float DurationSeconds = 5f;
    [Min(0f)] public float PeriodSeconds;

    [Tooltip("true = 不受 TimeScale 影响（HitStop 期间 Buff 仍走）。")]
    public bool RealtimeClock;

    [Header("Modifier Payload")]
    public ModifierEntry[] Modifiers = Array.Empty<ModifierEntry>();

    [Header("Periodic Action（W3 接 Trigger 之后细化）")]
    public bool ApplyPeriodicResourceDelta;
    [Tooltip("周期触发的资源 delta（正=回复 / 负=DOT）。")]
    public float PeriodicAmount;

    [Header("Stack Policy (180.1 §4)")]
    public StackPolicy StackPolicy = StackPolicy.Refresh;

    [Min(1)] public int MaxStack = 1;

    public StackMergeMode MergeMode = StackMergeMode.Multiply;

    [Header("Snapshot vs Dynamic (180.1 §8)")]
    [Tooltip("true = Apply 时锁定 Source.Stats；false = 每次 Period 重新采样。")]
    public bool SnapshotOnApply;

    [Header("Cleanse")]
    public GameplayTagMask CleanseTags;
    public bool IgnoredByCleanse;

    [Header("数值排序（ReplaceLower 用）")]
    [Tooltip("ReplaceLower 比较：同 SelfTag 内只保留 Power 最高。")]
    public float Power = 1f;
}
