using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 归一化时间轴上的标签切片 — 动作行为的最小定义单元。
///
/// 窗口侧用 <see cref="WindowSlotMask"/>：bit 0–63 对应槽位 Init0 … Init63（与 <see cref="StateTag"/> 位布局无关）。
/// 运行时通过 <see cref="ToInternalTagMask"/> 按映射表 OR 成系统内部的 <see cref="StateTag"/> 掩码；不修改 <see cref="StateTag"/> 定义。
///
/// 示例（轻攻击）：
///   [0.0–0.3] PhaseStartup                          — 前摇，不可取消
///   [0.3–0.5] PhaseActive | Invulnerable             — 判定帧，霸体
///   [0.5–1.0] PhaseRecovery | CanCancelToLocomotion   — 后摇，可取消
///
/// Inspector 由 Editor/StateTagMaskPropertyDrawers：分组下拉多选（槽位 0–63 ↔ StateTag）。
/// </summary>
[Serializable]
public struct ActionWindow
{
    [Range(0f, 1f)] public float NormalizedStart;

    [Range(0f, 1f)] public float NormalizedEnd;

    /// <summary>槽位掩码：第 <c>i</c> 位（0 ≤ i &lt; 64）表示 Init<c>i</c> 启用。</summary>
    public ulong WindowSlotMask;

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
