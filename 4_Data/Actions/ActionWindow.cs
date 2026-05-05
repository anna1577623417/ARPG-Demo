using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 归一化时间轴片段：战斗过程 <b>Phase</b>（前摇/判定/后摇）、<b>打断许可</b>、<b>时间行为</b>（无敌/缓冲/Hitbox/RootMotion），
/// 以及 <b>RuntimeEvents</b>（非 Tag，不进 State 掩码）。
/// 实体「能不能做」仍由 <see cref="EntityCapabilityTag"/>。
///
/// <see cref="WindowSlotMask"/> 经 <see cref="ActionWindowTagSlots"/> 映射后，与 <see cref="ActionWindowTimelineMask"/> 按位与写入 State。
/// </summary>
[Serializable]
public struct ActionWindow
{
    [Range(0f, 1f)] public float NormalizedStart;

    [Range(0f, 1f)] public float NormalizedEnd;

    /// <summary>槽位掩码：第 <c>i</c> 位（0 ≤ i &lt; 64）表示 Init<c>i</c> 启用。</summary>
    public ulong WindowSlotMask;

    /// <summary>本时间段内触发的事件（HitFrame / SFX / VFX）；运行时见 <see cref="ActionWindowTimelineEvents"/>。</summary>
    public List<ActionWindowEvent> RuntimeEvents;

    [SerializeField, HideInInspector]
    [FormerlySerializedAs("Tags")]
    ulong _legacyDirectStateTagMask;

    /// <summary>转为系统内部 <see cref="StateTag"/> 位掩码（ulong）。</summary>
    public readonly ulong ToInternalTagMask()
    {
        ulong result = 0UL;
        var map = ActionWindowTagSlots.SlotToInternalBit;
        for (var i = 0; i < ActionWindowTagSlots.SlotCount; i++)
        {
            if ((WindowSlotMask & (1UL << i)) != 0UL)
            {
                result |= map[i];
            }
        }

        return result;
    }

#if UNITY_EDITOR
    /// <summary>将旧版序列化字段 Tags（直接 <see cref="StateTag"/> 位）迁入 <see cref="WindowSlotMask"/> 并清空遗留字段。</summary>
    public bool TryMigrateLegacySerializedTags()
    {
        if (_legacyDirectStateTagMask == 0UL)
        {
            return false;
        }

        WindowSlotMask = ActionWindowTagSlots.InternalMaskToSlotMask(_legacyDirectStateTagMask);
        _legacyDirectStateTagMask = 0UL;
        return true;
    }
#endif
}

/// <summary>
/// 窗口合并进 State 轨时的策略位（与实体 Ability 轨解耦）。
/// </summary>
public static class ActionWindowMergePolicy
{
    /// <summary>
    /// 从窗口合并结果中移除遗留 <see cref="StateTag"/> Can* 位；实体能力改由 <see cref="EntityCapabilityTag"/> 写入容器 Ability 轨。
    /// </summary>
    public const ulong StripLegacyCapabilityStateBits =
        (1UL << 32) | (1UL << 33) | (1UL << 34) | (1UL << 35) | (1UL << 36) | (1UL << 38);
}

/// <summary>
/// 动作时间窗对「打断」的唯一位域：6 个 AllowInterrupt*。合并进 State 时与 <see cref="ActionWindow.ToInternalTagMask"/> 做按位与。
/// </summary>
public static class ActionWindowInterruptMask
{
    public const ulong AllAllowInterruptBits =
        (ulong)(StateTag.AllowInterruptByDodge
              | StateTag.AllowInterruptBySwordDash
              | StateTag.AllowInterruptByLight
              | StateTag.AllowInterruptByHeavy
              | StateTag.AllowInterruptByCharged
              | StateTag.AllowInterruptByJump);
}

/// <summary>无敌 / 输入缓冲 / Hitbox 开关 / RootMotion 等「时间行为」位（非 Ability）。</summary>
public static class ActionWindowTimeBehaviorMask
{
    public const ulong Bits =
        (ulong)StateTag.Invulnerable
      | (ulong)StateTag.ComboInput_Window
      | (ulong)StateTag.HitboxActive_Window
      | (ulong)StateTag.RootMotion_Window;
}

/// <summary>战斗动作 Phase：前摇 / 判定段 / 后摇（时间阶段标签，非能力）。</summary>
public static class ActionWindowPhaseMask
{
    public const ulong Bits =
        (ulong)(StateTag.PhaseStartup | StateTag.PhaseActive | StateTag.PhaseRecovery);
}

/// <summary>
/// ActionWindow 槽位允许写入 State 轨的并集：Interrupt + Phase + 时间行为（仍剔除 Can*）。
/// </summary>
public static class ActionWindowTimelineMask
{
    public const ulong AllContributableBits =
        ActionWindowInterruptMask.AllAllowInterruptBits
      | ActionWindowPhaseMask.Bits
      | ActionWindowTimeBehaviorMask.Bits;
}

/// <summary>
/// Init0–Init63 槽位 → 内部 <see cref="StateTag"/> 单比特。槽位顺序为所有单比特枚举按 ulong 升序，便于与策划表对齐。
/// </summary>
public static class ActionWindowTagSlots
{
    public const int SlotCount = 64;

    public static readonly ulong[] SlotToInternalBit = BuildSlotToInternalBit();

    static ulong[] BuildSlotToInternalBit()
    {
        var map = new ulong[SlotCount];
        var list = new System.Collections.Generic.List<ulong>();
        foreach (StateTag e in Enum.GetValues(typeof(StateTag)))
        {
            var u = (ulong)e;
            if (u == 0UL)
            {
                continue;
            }

            if ((u & (u - 1UL)) != 0UL)
            {
                continue;
            }

            list.Add(u);
        }

        list.Sort();
        for (var i = 0; i < list.Count && i < SlotCount; i++)
        {
            map[i] = list[i];
        }

        return map;
    }

    /// <summary>将「系统内部 <see cref="StateTag"/> 掩码」转为 Init 槽位掩码（每位至多对应一个已注册单比特标签）。</summary>
    public static ulong InternalMaskToSlotMask(ulong internalMask)
    {
        if (internalMask == 0UL)
        {
            return 0UL;
        }

        ulong slots = 0UL;
        for (var s = 0; s < SlotCount; s++)
        {
            var bit = SlotToInternalBit[s];
            if (bit != 0UL && (internalMask & bit) != 0UL)
            {
                slots |= 1UL << s;
            }
        }

        return slots;
    }

    /// <summary>槽位若映射到某个单比特 <see cref="StateTag"/> 则返回该枚举名，否则为 null。</summary>
    public static string GetSlotLabel(int slotIndex)
    {
        if ((uint)slotIndex >= SlotCount)
        {
            return null;
        }

        var bit = SlotToInternalBit[slotIndex];
        if (bit == 0UL)
        {
            return null;
        }

        foreach (StateTag e in Enum.GetValues(typeof(StateTag)))
        {
            if ((ulong)e == bit)
            {
                return e.ToString();
            }
        }

        return null;
    }
}
