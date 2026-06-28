using System;
using UnityEngine;

/// <summary>
/// 175.2 W3 — Jump Modifier（雨 2 风格道具/Buff payload）。
/// <para>一个 Modifier = 一类道具；MaxStack 控制叠层；FieldMods 与 Ability 开关一并作用于 <see cref="JumpAbilityConfig"/>。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Jump/Jump Modifier", fileName = "JumpModifier_")]
public sealed class JumpModifierSO : ScriptableObject
{
    [Header("展示")]
    public string DisplayName = "Modifier";
    public Sprite Icon;

    [Header("叠层")]
    [Tooltip("-1 = 无限叠；>0 = 上限层数。")]
    public int MaxStack = -1;

    [Header("数值修饰（按 Stack 倍率作用）")]
    public JumpFieldModifier[] FieldMods = Array.Empty<JumpFieldModifier>();

    [Header("能力开关")]
    [Tooltip("Hopoo Feather：每层 +1 空中可跳次数。")]
    public bool AddOneAirJumpPerStack;

    [Tooltip("启用滑翔（双子座等）。")]
    public bool EnableGlide;

    [Tooltip("Wax Quail：启用冲刺跳横向爆发。")]
    public bool EnableDashJump;

    [Tooltip("Wax Quail：每层增加 N m/s 冲刺跳横向冲量。")]
    public float DashJumpHorizontalPerStack;

    [Header("强制 Variant")]
    [Tooltip("BackflipDevice 等：起手强制切换为指定 Variant；Normal = 不强制。")]
    public JumpVariantId ForceVariant = JumpVariantId.Normal;

    [Header("反馈")]
    [Tooltip("每次起跳额外播放的 VFX/事件。")]
    public ScriptableObject PerJumpEffect;

    [Tooltip("叠加授予的能力 Tag。")]
    public GameplayTagMask GrantedTags;
}

/// <summary>175.2 W3 — 单条字段修饰（一个 Modifier 可有多个 FieldMod）。</summary>
[Serializable]
public struct JumpFieldModifier
{
    public JumpField Field;
    public ModifierOp Op;

    [Tooltip("数值。按 Op 与 Stack 应用：Add=+v*n；MulCurrent=*(1+v*n)；MulBase=+base*v*n。")]
    public float Value;
}
