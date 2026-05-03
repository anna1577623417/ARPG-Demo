#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
//   标签掩码 Inspector 绘制器（v3.1.2）
// ───────────────────────────────────────────────────────────────────────────────
//   核心 UI：按 StateTag bit 分区；每组一行「分类标题 + 下拉按钮」，点击弹出 GenericMenu
//   多选（带勾，行为类似 Layer/Mask）。标签文案主信息在前（如 Jump — Interrupt）。
//     · Physical / Phase / Ability / Interrupt / Reserved
//   顶部一行仍显示全局已选摘要。
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
            var display = FormatTagDisplay(e, cat);
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

    /// <summary>
    /// Inspector 窄列友好：主语义在前（Jump / Dodge），修饰在后（Interrupt / Can）。
    /// </summary>
    static string FormatTagDisplay(StateTag tag, TagCategory cat)
    {
        switch (tag)
        {
            case StateTag.CanJump:
                return "Jump — Can";
            case StateTag.CanDodge:
                return "Dodge — Can";
            case StateTag.CanLightAttack:
                return "Light — Can";
            case StateTag.CanHeavyAttack:
                return "Heavy — Can";
            case StateTag.CanCancelToLocomotion:
                return "Cancel Loco — Can";
            case StateTag.Invulnerable:
                return "Invuln — Can";
            case StateTag.CanSwordDash:
                return "Sword Dash — Can";
            case StateTag.AttackCharged:
                return "Charged — Tag";
            case StateTag.AllowInterruptByDodge:
                return "Dodge — Interrupt";
            case StateTag.AllowInterruptBySwordDash:
                return "Sword Dash — Interrupt";
            case StateTag.AllowInterruptByLight:
                return "Light — Interrupt";
            case StateTag.AllowInterruptByHeavy:
                return "Heavy — Interrupt";
            case StateTag.AllowInterruptByCharged:
                return "Charged — Interrupt";
            case StateTag.AllowInterruptByJump:
                return "Jump — Interrupt";
            case StateTag.PhaseStartup:
                return "Startup — Phase";
            case StateTag.PhaseActive:
                return "Active — Phase";
            case StateTag.PhaseRecovery:
                return "Recovery — Phase";
            case StateTag.Stunned:
                return "Stunned — Phase";
            default:
                return ObjectNames.NicifyVariableName(tag.ToString());
        }
    }

    public static string CategoryHeader(TagCategory c) => c switch
    {
        TagCategory.Physical  => "Physical",
        TagCategory.Phase     => "Phase",
        TagCategory.Ability   => "Ability",
        TagCategory.Interrupt => "Interrupt",
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
        return GroupedTagDrawer.GetHeight();
    }

    internal static void DrawMaskField(Rect rect, SerializedProperty prop, GUIContent label)
    {
        GroupedTagDrawer.Draw(rect, prop, label, isSlotMode: false);
    }
}

// ───────────────────────────────────────────────────────────────────────────────
//   分组下拉多选（仿 Layer/Mask 弹出菜单 + 勾选项）
// ───────────────────────────────────────────────────────────────────────────────
internal static class GroupedTagDrawer
{
    static readonly TagCategory[] s_order =
    {
        TagCategory.Physical, TagCategory.Phase, TagCategory.Ability,
        TagCategory.Interrupt, TagCategory.Reserved,
    };

    /// <summary>
    /// <paramref name="isSlotMode"/>：true 时字段存 Slot 位，读写时与 StateTag 互转。
    /// </summary>
    public static void Draw(Rect rect, SerializedProperty ulongProp, GUIContent label, bool isSlotMode)
    {
        EditorGUI.BeginProperty(rect, label, ulongProp);

        var rawMask = StateTagMaskDrawerUtility.ReadULong(ulongProp);
        var stateTagMask = isSlotMode ? SlotMaskToStateTagMask(rawMask) : rawMask;

        var lineH = EditorGUIUtility.singleLineHeight;
        var space = 2f;

        var headRect = new Rect(rect.x, rect.y, rect.width, lineH);
        var labelRect = new Rect(headRect.x, headRect.y, EditorGUIUtility.labelWidth, lineH);
        var summaryRect = new Rect(headRect.x + EditorGUIUtility.labelWidth, headRect.y,
            headRect.width - EditorGUIUtility.labelWidth, lineH);
        EditorGUI.LabelField(labelRect, label);
        DrawSelectionSummary(summaryRect, stateTagMask);

        var y = rect.y + lineH + space;

        foreach (var cat in s_order)
        {
            var entries = TagCatalog.ByCategory[cat];
            if (entries.Count == 0) continue;

            var rowRect = new Rect(rect.x, y, rect.width, lineH);
            var prefix = TagCatalog.CategoryHeader(cat);
            var countSel = CountSelected(entries, stateTagMask);
            var prefixW = Mathf.Min(128f, rowRect.width * 0.32f);
            var prefixRect = new Rect(rowRect.x, rowRect.y, prefixW, lineH);
            var dropRect = new Rect(rowRect.x + prefixW + 4f, rowRect.y,
                Mathf.Max(100f, rowRect.width - prefixW - 4f), lineH);

            var prefixStyle = EditorStyles.miniLabel;
            EditorGUI.LabelField(prefixRect,
                new GUIContent($"{prefix}  ({countSel}/{entries.Count})", prefix), prefixStyle);

            var summaryBtn = BuildCategorySelectionSummary(entries, stateTagMask);
            var gc = new GUIContent(string.IsNullOrEmpty(summaryBtn) ? "(choose…)" : summaryBtn);

            if (EditorGUI.DropdownButton(dropRect, gc, FocusType.Passive))
            {
                ShowCategoryDropdown(dropRect, ulongProp, entries, isSlotMode);
            }

            y += lineH + space;
        }

        EditorGUI.EndProperty();
    }

    public static float GetHeight()
    {
        var lineH = EditorGUIUtility.singleLineHeight;
        var space = 2f;
        var h = lineH + space;
        foreach (var cat in s_order)
        {
            var entries = TagCatalog.ByCategory[cat];
            if (entries.Count == 0) continue;
            h += lineH + space;
        }
        return h;
    }

    static void ShowCategoryDropdown(Rect buttonRect, SerializedProperty ulongProp,
        List<TagEntry> entries, bool isSlotMode)
    {
        var menu = new GenericMenu();
        foreach (var e in entries)
        {
            var entry = e;
            var on = (ReadSemanticMask(ulongProp, isSlotMode) & (ulong)entry.Tag) != 0UL;
            menu.AddItem(new GUIContent(entry.Display), on, () =>
            {
                Undo.RecordObject(ulongProp.serializedObject.targetObject, "Toggle Gameplay Tag");
                var semantic = ReadSemanticMask(ulongProp, isSlotMode);
                semantic ^= (ulong)entry.Tag;
                WriteSemanticMask(ulongProp, semantic, isSlotMode);
                ulongProp.serializedObject.ApplyModifiedProperties();
                GUI.changed = true;
            });
        }

        menu.DropDown(buttonRect);
    }

    static ulong ReadSemanticMask(SerializedProperty prop, bool isSlotMode)
    {
        var raw = StateTagMaskDrawerUtility.ReadULong(prop);
        return isSlotMode ? SlotMaskToStateTagMask(raw) : raw;
    }

    static void WriteSemanticMask(SerializedProperty prop, ulong semantic, bool isSlotMode)
    {
        var newRaw = isSlotMode ? StateTagMaskToSlotMask(semantic) : semantic;
        StateTagMaskDrawerUtility.WriteULong(prop, newRaw);
    }

    static int CountSelected(List<TagEntry> entries, ulong mask)
    {
        var n = 0;
        foreach (var e in entries)
            if ((mask & (ulong)e.Tag) != 0UL) n++;
        return n;
    }

    /// <summary>按钮上显示的已选项短列表（主信息在前，与 TagEntry.Display 一致）。</summary>
    static string BuildCategorySelectionSummary(List<TagEntry> entries, ulong stateTagMask)
    {
        var selected = new List<string>(8);
        foreach (var e in entries)
            if ((stateTagMask & (ulong)e.Tag) != 0UL) selected.Add(e.Display);

        if (selected.Count == 0) return string.Empty;
        if (selected.Count == 1) return selected[0];
        if (selected.Count == 2) return $"{selected[0]}, {selected[1]}";
        return $"{selected[0]}, {selected[1]}  +{selected.Count - 2}";
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
        return GroupedTagDrawer.GetHeight();
    }

    internal static void DrawSlotMaskField(Rect rect, SerializedProperty prop, GUIContent label)
    {
        GroupedTagDrawer.Draw(rect, prop, label, isSlotMode: true);
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
// ───────────────────────────────────────────────────────────────────────────────
//   [StateTagMask] 属性抽屉 — 让任意 ulong/long 字段获得分组折叠 UI
// ───────────────────────────────────────────────────────────────────────────────
[CustomPropertyDrawer(typeof(StateTagMaskAttribute))]
internal sealed class StateTagMaskAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        StateTagMaskDrawerUtility.DrawMaskField(position, property, label);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return StateTagMaskDrawerUtility.GetMaskHeight(property, label);
    }
}

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
