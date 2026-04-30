#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
//   标签掩码 Inspector 绘制器（v3.1.2）
// ───────────────────────────────────────────────────────────────────────────────
//   核心改造：抛弃 64 个 checkbox 平铺，按 StateTag bit 分区折叠分组：
//     · Physical (Bit  0–15)
//     · Phase    (Bit 16–31)
//     · Ability  (Bit 32–39)
//     · Interrupt(Bit 40–47)
//     · Reserved (Bit 48–63)
//   未映射的槽位完全隐藏；顶部一行显示当前已选标签摘要。
//
//   存储兼容：
//     · ChargedPayloadTags (ulong, StateTag 直接位)   — DrawStateTagMaskField
//     · ActionWindow.WindowSlotMask (ulong, 槽位位)   — DrawWindowSlotMaskField
//   两条入口共用同一份分组数据与渲染主体，仅在读写时做位转换。
// ═══════════════════════════════════════════════════════════════════════════════

internal enum TagCategory
{
    Physical = 0,
    Phase = 1,
    Ability = 2,
    Interrupt = 3,
    Reserved = 4,
}

internal readonly struct TagEntry
{
    public readonly StateTag Tag;
    public readonly int BitIndex;
    public readonly int SlotIndex;
    public readonly TagCategory Category;
    public readonly string Display;

    public TagEntry(StateTag tag, int bitIndex, int slotIndex, TagCategory cat, string display)
    {
        Tag = tag;
        BitIndex = bitIndex;
        SlotIndex = slotIndex;
        Category = cat;
        Display = display;
    }
}

internal static class TagCatalog
{
    public static readonly TagEntry[] All;
    public static readonly Dictionary<TagCategory, List<TagEntry>> ByCategory;

    static TagCatalog()
    {
        var list = new List<TagEntry>();
        foreach (StateTag e in Enum.GetValues(typeof(StateTag)))
        {
            var u = (ulong)e;
            if (u == 0UL) continue;
            if ((u & (u - 1UL)) != 0UL) continue;          // 跳过组合位（如 None / 未来出现的 alias）

            var bit = BitIndexOf(u);
            var slot = FindSlotIndex(u);                     // -1 = 未在 ActionWindowTagSlots 中（极端情况）
            var cat = InferCategory(bit);
            var display = ObjectNames.NicifyVariableName(e.ToString());
            list.Add(new TagEntry(e, bit, slot, cat, display));
        }

        list.Sort((a, b) => a.BitIndex.CompareTo(b.BitIndex));
        All = list.ToArray();

        ByCategory = new Dictionary<TagCategory, List<TagEntry>>();
        foreach (TagCategory c in Enum.GetValues(typeof(TagCategory))) ByCategory[c] = new List<TagEntry>();
        foreach (var e in All) ByCategory[e.Category].Add(e);
    }

    static int BitIndexOf(ulong singleBit)
    {
        for (var i = 0; i < 64; i++) if (singleBit == 1UL << i) return i;
        return -1;
    }

    static int FindSlotIndex(ulong stateTagBit)
    {
        var map = ActionWindowTagSlots.SlotToInternalBit;
        for (var i = 0; i < ActionWindowTagSlots.SlotCount; i++)
            if (map[i] == stateTagBit) return i;
        return -1;
    }

    static TagCategory InferCategory(int bit)
    {
        if (bit < 16) return TagCategory.Physical;
        if (bit < 32) return TagCategory.Phase;
        if (bit < 40) return TagCategory.Ability;
        if (bit < 48) return TagCategory.Interrupt;
        return TagCategory.Reserved;
    }

    public static string CategoryHeader(TagCategory c) => c switch
    {
        TagCategory.Physical  => "Physical (姿态)",
        TagCategory.Phase     => "Phase (动作阶段)",
        TagCategory.Ability   => "Ability gates (实体能力闸门)",
        TagCategory.Interrupt => "Interrupt permissions (打断许可)",
        TagCategory.Reserved  => "Reserved",
        _ => c.ToString(),
    };
}

// ───────────────────────────────────────────────────────────────────────────────
//   底层 ulong 序列化读写（屏蔽 Unity 2020.2 之前的 longValue fallback）
// ───────────────────────────────────────────────────────────────────────────────
internal static class StateTagMaskDrawerUtility
{
    internal static ulong ReadULong(SerializedProperty prop)
    {
#if UNITY_2020_2_OR_NEWER
        return prop.ulongValue;
#else
        unchecked { return (ulong)prop.longValue; }
#endif
    }

    internal static void WriteULong(SerializedProperty prop, ulong value)
    {
#if UNITY_2020_2_OR_NEWER
        prop.ulongValue = value;
#else
        unchecked { prop.longValue = (long)value; }
#endif
    }

    // ── 给 ActionChargeConfig.ChargedPayloadTags 用：ulong 直接是 StateTag 位 ──
    internal static float GetMaskHeight(SerializedProperty prop, GUIContent label)
    {
        var stateTagMask = ReadULong(prop);
        return GroupedTagDrawer.GetHeight(prop.propertyPath, stateTagMask);
    }

    internal static void DrawMaskField(Rect rect, SerializedProperty prop, GUIContent label)
    {
        var raw = ReadULong(prop);
        var changed = GroupedTagDrawer.Draw(rect, prop.propertyPath, label, raw,
            isSlotMode: false, out var newVal);
        if (changed) WriteULong(prop, newVal);
    }
}

// ───────────────────────────────────────────────────────────────────────────────
//   分组折叠绘制器主体
// ───────────────────────────────────────────────────────────────────────────────
internal static class GroupedTagDrawer
{
    const int Cols = 2;
    static readonly TagCategory[] s_order =
    {
        TagCategory.Physical, TagCategory.Phase, TagCategory.Ability,
        TagCategory.Interrupt, TagCategory.Reserved,
    };

    /// <summary>统一入口。<paramref name="isSlotMode"/>=true 时 rawMask 是 Slot 位（需 Slot↔StateTag 转换）。</summary>
    public static bool Draw(Rect rect, string propertyPath, GUIContent label, ulong rawMask,
        bool isSlotMode, out ulong newRawMask)
    {
        var stateTagMask = isSlotMode ? SlotMaskToStateTagMask(rawMask) : rawMask;
        var lineH = EditorGUIUtility.singleLineHeight;
        var space = 2f;

        // ── 标题行：label + 选中摘要 ─────────────────────────────────────
        var headRect = new Rect(rect.x, rect.y, rect.width, lineH);
        var labelRect = new Rect(headRect.x, headRect.y, EditorGUIUtility.labelWidth, lineH);
        var summaryRect = new Rect(headRect.x + EditorGUIUtility.labelWidth, headRect.y,
            headRect.width - EditorGUIUtility.labelWidth, lineH);
        EditorGUI.LabelField(labelRect, label);
        DrawSelectionSummary(summaryRect, stateTagMask);

        var y = rect.y + lineH + space;
        var changed = false;

        // ── 每个分组一个 Foldout（状态存 SessionState，不污染 Asset）──
        foreach (var cat in s_order)
        {
            var entries = TagCatalog.ByCategory[cat];
            if (entries.Count == 0) continue;

            var hasSelected = AnyEntrySelected(entries, stateTagMask);
            var foldKey = $"TagDrawer.Foldout::{propertyPath}::{cat}";
            var defaultExpanded = hasSelected;
            var expanded = SessionState.GetBool(foldKey, defaultExpanded);

            var foldRect = new Rect(rect.x, y, rect.width, lineH);
            var newExpanded = EditorGUI.Foldout(foldRect, expanded,
                BuildCategoryHeader(cat, entries, stateTagMask), true);
            if (newExpanded != expanded) SessionState.SetBool(foldKey, newExpanded);
            y += lineH + space;

            if (!newExpanded) continue;

            EditorGUI.indentLevel++;
            var indentX = rect.x + 14f;
            var contentW = rect.width - 14f;
            var colW = contentW / Cols;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var col = i % Cols;
                var row = i / Cols;
                var rr = new Rect(indentX + col * colW, y + row * (lineH + 1f),
                    colW - 4f, lineH);

                var on = (stateTagMask & (ulong)entry.Tag) != 0UL;
                var newOn = EditorGUI.ToggleLeft(rr, entry.Display, on);
                if (newOn != on)
                {
                    if (newOn) stateTagMask |= (ulong)entry.Tag;
                    else stateTagMask &= ~(ulong)entry.Tag;
                    changed = true;
                }
            }

            var rows = Mathf.CeilToInt((float)entries.Count / Cols);
            y += rows * (lineH + 1f) + space;
            EditorGUI.indentLevel--;
        }

        newRawMask = isSlotMode ? StateTagMaskToSlotMask(stateTagMask) : stateTagMask;
        return changed;
    }

    public static float GetHeight(string propertyPath, ulong stateTagMask)
    {
        var lineH = EditorGUIUtility.singleLineHeight;
        var space = 2f;
        var h = lineH + space;                                 // 标题行
        foreach (var cat in s_order)
        {
            var entries = TagCatalog.ByCategory[cat];
            if (entries.Count == 0) continue;

            h += lineH + space;                                // foldout header

            var hasSelected = AnyEntrySelected(entries, stateTagMask);
            var defaultExpanded = hasSelected;
            var foldKey = $"TagDrawer.Foldout::{propertyPath}::{cat}";
            var expanded = SessionState.GetBool(foldKey, defaultExpanded);
            if (!expanded) continue;

            var rows = Mathf.CeilToInt((float)entries.Count / Cols);
            h += rows * (lineH + 1f) + space;
        }
        return h;
    }

    // ── 摘要：右侧显示 [3 selected] Grounded, CanLightAttack, +1 ──
    static void DrawSelectionSummary(Rect rect, ulong stateTagMask)
    {
        var selected = new List<string>(8);
        foreach (var e in TagCatalog.All)
            if ((stateTagMask & (ulong)e.Tag) != 0UL) selected.Add(e.Display);

        string text;
        if (selected.Count == 0)
            text = "<i>(none)</i>";
        else if (selected.Count <= 2)
            text = string.Join(", ", selected);
        else
            text = $"{selected[0]}, {selected[1]}, +{selected.Count - 2}";

        var style = new GUIStyle(EditorStyles.label) { richText = true };
        EditorGUI.LabelField(rect, $"[{selected.Count} selected] {text}", style);
    }

    static GUIContent BuildCategoryHeader(TagCategory cat, List<TagEntry> entries, ulong mask)
    {
        var count = 0;
        foreach (var e in entries) if ((mask & (ulong)e.Tag) != 0UL) count++;
        var head = TagCatalog.CategoryHeader(cat);
        return new GUIContent(count > 0 ? $"{head}  ({count}/{entries.Count})" : $"{head}  ({entries.Count})");
    }

    static bool AnyEntrySelected(List<TagEntry> entries, ulong mask)
    {
        foreach (var e in entries) if ((mask & (ulong)e.Tag) != 0UL) return true;
        return false;
    }

    // ── Slot ↔ StateTag 互转（Window 模式专用）──
    static ulong SlotMaskToStateTagMask(ulong slotMask)
    {
        if (slotMask == 0UL) return 0UL;
        ulong result = 0UL;
        var map = ActionWindowTagSlots.SlotToInternalBit;
        for (var i = 0; i < ActionWindowTagSlots.SlotCount; i++)
            if ((slotMask & (1UL << i)) != 0UL) result |= map[i];
        return result;
    }

    static ulong StateTagMaskToSlotMask(ulong stateTagMask)
    {
        return ActionWindowTagSlots.InternalMaskToSlotMask(stateTagMask);
    }
}

// ───────────────────────────────────────────────────────────────────────────────
//   ActionWindow 专用入口：内部存 Slot 位，UI 显示 StateTag 语义
// ───────────────────────────────────────────────────────────────────────────────
internal static class ActionWindowSlotMaskDrawerUtility
{
    internal static float GetMaskHeight(SerializedProperty prop)
    {
        var slotMask = StateTagMaskDrawerUtility.ReadULong(prop);
        var stateTagMask = SlotMaskToStateTagMask(slotMask);
        return GroupedTagDrawer.GetHeight(prop.propertyPath, stateTagMask);
    }

    internal static void DrawSlotMaskField(Rect rect, SerializedProperty prop, GUIContent label)
    {
        var slotMask = StateTagMaskDrawerUtility.ReadULong(prop);
        var changed = GroupedTagDrawer.Draw(rect, prop.propertyPath, label, slotMask,
            isSlotMode: true, out var newSlotMask);
        if (changed) StateTagMaskDrawerUtility.WriteULong(prop, newSlotMask);
    }

    static ulong SlotMaskToStateTagMask(ulong slotMask)
    {
        if (slotMask == 0UL) return 0UL;
        ulong result = 0UL;
        var map = ActionWindowTagSlots.SlotToInternalBit;
        for (var i = 0; i < ActionWindowTagSlots.SlotCount; i++)
            if ((slotMask & (1UL << i)) != 0UL) result |= map[i];
        return result;
    }
}

// ───────────────────────────────────────────────────────────────────────────────
//   ActionWindow 整体绘制：Start/End 滑条 + 分组标签
// ───────────────────────────────────────────────────────────────────────────────
[CustomPropertyDrawer(typeof(ActionWindow))]
internal sealed class ActionWindowDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var line = EditorGUIUtility.singleLineHeight;
        var space = EditorGUIUtility.standardVerticalSpacing;
        var y = position.y;

        var pStart = property.FindPropertyRelative(nameof(ActionWindow.NormalizedStart));
        var pEnd = property.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd));
        var pSlots = property.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask));

        EditorGUI.PropertyField(new Rect(position.x, y, position.width, line), pStart);
        y += line + space;
        EditorGUI.PropertyField(new Rect(position.x, y, position.width, line), pEnd);
        y += line + space;

        var tagH = ActionWindowSlotMaskDrawerUtility.GetMaskHeight(pSlots);
        ActionWindowSlotMaskDrawerUtility.DrawSlotMaskField(
            new Rect(position.x, y, position.width, tagH),
            pSlots,
            new GUIContent("Tags"));

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var line = EditorGUIUtility.singleLineHeight;
        var space = EditorGUIUtility.standardVerticalSpacing;
        var pSlots = property.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask));
        return line + space + line + space
            + ActionWindowSlotMaskDrawerUtility.GetMaskHeight(pSlots);
    }
}

// ───────────────────────────────────────────────────────────────────────────────
//   ActionChargeConfig：ChargedPayloadTags 字段使用 StateTag 直接位
// ───────────────────────────────────────────────────────────────────────────────
[CustomPropertyDrawer(typeof(ActionChargeConfig))]
internal sealed class ActionChargeConfigDrawer : PropertyDrawer
{
    static readonly string[] s_fieldOrder =
    {
        nameof(ActionChargeConfig.CanCharge),
        nameof(ActionChargeConfig.ChargeHoldPoint),
        nameof(ActionChargeConfig.ChargeStartupSpeed),
        nameof(ActionChargeConfig.MaxChargeHoldTime),
        nameof(ActionChargeConfig.ExecutionSpeed),
        nameof(ActionChargeConfig.MinHoldTimeForChargedTag),
        nameof(ActionChargeConfig.ChargedPayloadTags),
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var y = position.y;
        var space = EditorGUIUtility.standardVerticalSpacing;

        for (var i = 0; i < s_fieldOrder.Length; i++)
        {
            var child = property.FindPropertyRelative(s_fieldOrder[i]);
            if (child == null) continue;

            float h;
            if (child.name == nameof(ActionChargeConfig.ChargedPayloadTags))
            {
                h = StateTagMaskDrawerUtility.GetMaskHeight(child, new GUIContent(child.displayName));
                StateTagMaskDrawerUtility.DrawMaskField(
                    new Rect(position.x, y, position.width, h),
                    child,
                    new GUIContent(child.displayName));
            }
            else
            {
                h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
            }

            y += h + space;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var space = EditorGUIUtility.standardVerticalSpacing;
        float h = 0f;

        for (var i = 0; i < s_fieldOrder.Length; i++)
        {
            var child = property.FindPropertyRelative(s_fieldOrder[i]);
            if (child == null) continue;

            if (child.name == nameof(ActionChargeConfig.ChargedPayloadTags))
                h += StateTagMaskDrawerUtility.GetMaskHeight(child, new GUIContent(child.displayName));
            else
                h += EditorGUI.GetPropertyHeight(child, true);

            h += space;
        }

        return h;
    }
}
#endif
