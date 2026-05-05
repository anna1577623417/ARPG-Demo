#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
//   标签掩码 Inspector 绘制器（v3.1.2）
// ───────────────────────────────────────────────────────────────────────────────
//   核心 UI：按 StateTag bit 分区；每组一行「分类标题 + 下拉按钮」，点击弹出 GenericMenu
//   多选（带勾，行为类似 Layer/Mask）。
//   条目文案：完整英文 + 后缀语义（AllowInterrupt* → name_Interrupt；其它见 FormatTagDisplay）。
//   · Full：Physical / Phase / Ability / Interrupt / time_WindowSemantics / Reserved
//   · InterruptOnly：仅 AllowInterrupt* — PlayerStateManager 连续状态打断掩码
//   · WindowTimeline：打断 + time_WindowBehavior（连续状态掩码，与 ActionDataSO 第二～三组一致）
//   · ActionWindow：三行 = interruption + phase_Window + time_WindowBehavior；另见 ActionWindow.RuntimeEvents
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
    /// <summary>动作窗时间语义（ComboInput_Window）；不含 Invulnerable（仍在 Ability 分组）。</summary>
    WindowTimelineSemantic = 4,
    Reserved = 5,
}

/// <summary>Inspector 分组与可选标签范围。</summary>
internal enum TagDrawerMode
{
    /// <summary>全部分组（ChargedPayloadTags 等）。</summary>
    Full,

    /// <summary>仅 AllowInterrupt*（连续状态打断掩码）。</summary>
    InterruptOnly,

    /// <summary>Locomotion / Airborne 连续状态：两行掩码（与 ActionWindow 时间语义列一致，不含 Phase）。</summary>
    WindowTimeline,

    /// <summary>ActionDataSO 切片：打断 + 战斗 Phase + 时间行为（Hitbox/RootMotion 等）。</summary>
    ActionWindow,
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

    /// <summary>第三行：无敌 / 缓冲 / Hitbox / RootMotion（排除 Phase 与 Interrupt）。</summary>
    public static readonly List<TagEntry> ActionWindowTimeBehaviorEntries;

    /// <summary>ActionWindow 第二行：startup / active / recovery Phase。</summary>
    public static readonly List<TagEntry> ActionWindowPhaseEntries;

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

        ActionWindowTimeBehaviorEntries = new List<TagEntry>();
        foreach (var e in All)
        {
            if (e.Tag == StateTag.Invulnerable
                || e.Tag == StateTag.ComboInput_Window
                || e.Tag == StateTag.HitboxActive_Window
                || e.Tag == StateTag.RootMotion_Window)
            {
                ActionWindowTimeBehaviorEntries.Add(e);
            }
        }

        ActionWindowTimeBehaviorEntries.Sort((a, b) => a.BitIndex.CompareTo(b.BitIndex));

        ActionWindowPhaseEntries = new List<TagEntry>();
        foreach (var e in All)
        {
            if (e.Tag == StateTag.PhaseStartup
                || e.Tag == StateTag.PhaseActive
                || e.Tag == StateTag.PhaseRecovery)
            {
                ActionWindowPhaseEntries.Add(e);
            }
        }

        ActionWindowPhaseEntries.Sort((a, b) => a.BitIndex.CompareTo(b.BitIndex));
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
        if (bit >= 40 && bit <= 45) return TagCategory.Interrupt;
        if (bit == 46 || bit == 47) return TagCategory.WindowTimelineSemantic;
        if (bit < 40) return TagCategory.Ability;
        if (bit < 48) return TagCategory.Interrupt;
        return TagCategory.Reserved;
    }

    /// <summary>完整英文 snake_case；打断许可统一 *_Interrupt。</summary>
    static string FormatTagDisplay(StateTag tag, TagCategory cat)
    {
#pragma warning disable CS0618 // 遗留 Can* 枚举位仍参与槽位与全量掩码显示
        switch (tag)
        {
            case StateTag.Grounded:
                return "grounded_Physical";
            case StateTag.Airborne:
                return "airborne_Physical";
            case StateTag.Climbing:
                return "climbing_Physical";
            case StateTag.Swimming:
                return "swimming_Physical";
            case StateTag.PhaseStartup:
                return "startup_Phase";
            case StateTag.PhaseActive:
                return "active_Phase";
            case StateTag.PhaseRecovery:
                return "recovery_Phase";
            case StateTag.Stunned:
                return "stunned_Phase";
            case StateTag.HitboxActive_Window:
                return "hitbox_active_Window";
            case StateTag.RootMotion_Window:
                return "root_motion_Window";
            case StateTag.CanJump:
                return "can_jump_Ability_legacy";
            case StateTag.CanDodge:
                return "can_dodge_Ability_legacy";
            case StateTag.CanLightAttack:
                return "can_light_attack_Ability_legacy";
            case StateTag.CanHeavyAttack:
                return "can_heavy_attack_Ability_legacy";
            case StateTag.CanCancelToLocomotion:
                return "can_cancel_to_locomotion_Ability_legacy";
            case StateTag.Invulnerable:
                return "invulnerable";
            case StateTag.CanSwordDash:
                return "can_sword_dash_Ability_legacy";
            case StateTag.AttackCharged:
                return "attack_charged_Payload";
            case StateTag.AllowInterruptByDodge:
                return "dodge_Interrupt";
            case StateTag.AllowInterruptBySwordDash:
                return "sword_dash_Interrupt";
            case StateTag.AllowInterruptByLight:
                return "light_attack_Interrupt";
            case StateTag.AllowInterruptByHeavy:
                return "heavy_attack_Interrupt";
            case StateTag.AllowInterruptByCharged:
                return "charged_attack_Interrupt";
            case StateTag.AllowInterruptByJump:
                return "jump_Interrupt";
            case StateTag.ComboInput_Window:
                return "combo_input_Window";
            case StateTag.Dead:
                return "dead_Reserved";
            default:
                return ObjectNames.NicifyVariableName(tag.ToString()).Replace(' ', '_');
        }
#pragma warning restore CS0618
    }

    public static string CategoryHeader(TagCategory c) => c switch
    {
        TagCategory.Physical               => "Physical",
        TagCategory.Phase                  => "Phase",
        TagCategory.Ability                => "Ability (legacy)",
        TagCategory.Interrupt              => "Interrupt",
        TagCategory.WindowTimelineSemantic => "time_WindowSemantics",
        TagCategory.Reserved               => "Reserved",
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
        return GetMaskHeight(prop, label, TagDrawerMode.Full);
    }

    internal static float GetMaskHeight(SerializedProperty prop, GUIContent label, TagDrawerMode mode)
    {
        return GroupedTagDrawer.GetHeight(isSlotMode: false, mode);
    }

    internal static void DrawMaskField(Rect rect, SerializedProperty prop, GUIContent label)
    {
        DrawMaskField(rect, prop, label, TagDrawerMode.Full);
    }

    internal static void DrawMaskField(Rect rect, SerializedProperty prop, GUIContent label, TagDrawerMode mode)
    {
        GroupedTagDrawer.Draw(rect, prop, label, isSlotMode: false, mode);
    }
}

// ───────────────────────────────────────────────────────────────────────────────
//   分组下拉多选（仿 Layer/Mask 弹出菜单 + 勾选项）
// ───────────────────────────────────────────────────────────────────────────────
internal static class GroupedTagDrawer
{
    static readonly TagCategory[] s_orderFull =
    {
        TagCategory.Physical, TagCategory.Phase, TagCategory.Ability,
        TagCategory.Interrupt, TagCategory.WindowTimelineSemantic, TagCategory.Reserved,
    };

    static readonly TagCategory[] s_orderInterruptOnly =
    {
        TagCategory.Interrupt,
    };

    /// <summary>
    /// <paramref name="isSlotMode"/>：true 时字段存 Slot 位（ActionWindow），读写时与 StateTag 互转。
    /// </summary>
    public static void Draw(Rect rect, SerializedProperty ulongProp, GUIContent label, bool isSlotMode,
        TagDrawerMode drawerMode)
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
        DrawSelectionSummary(summaryRect, stateTagMask, drawerMode);

        var y = rect.y + lineH + space;

        if (drawerMode == TagDrawerMode.ActionWindow)
        {
            y = DrawTagMaskRow(rect, y, lineH, space, ulongProp, stateTagMask, isSlotMode,
                "interruption_mechanism",
                "AllowInterrupt* only. Not Can* / Ability.",
                TagCatalog.ByCategory[TagCategory.Interrupt]);
            y = DrawTagMaskRow(rect, y, lineH, space, ulongProp, stateTagMask, isSlotMode,
                "combat_phase",
                "startup_Phase / active_Phase / recovery_Phase — animation time stage.",
                TagCatalog.ActionWindowPhaseEntries);
            DrawTagMaskRow(rect, y, lineH, space, ulongProp, stateTagMask, isSlotMode,
                "time_WindowBehavior",
                "invulnerable, combo_input_Window, hitbox_active_Window, root_motion_Window.",
                TagCatalog.ActionWindowTimeBehaviorEntries);
            EditorGUI.EndProperty();
            return;
        }

        if (drawerMode == TagDrawerMode.WindowTimeline)
        {
            y = DrawTagMaskRow(rect, y, lineH, space, ulongProp, stateTagMask, isSlotMode,
                "interruption_mechanism",
                "AllowInterrupt* only (dodge_Interrupt, jump_Interrupt, …). Not ability gates.",
                TagCatalog.ByCategory[TagCategory.Interrupt]);
            y = DrawTagMaskRow(rect, y, lineH, space, ulongProp, stateTagMask, isSlotMode,
                "time_WindowBehavior",
                "Same time-behavior bits as ActionWindow row 3 (no combat_phase on locomotion).",
                TagCatalog.ActionWindowTimeBehaviorEntries);
            EditorGUI.EndProperty();
            return;
        }

        var order = drawerMode == TagDrawerMode.InterruptOnly ? s_orderInterruptOnly : s_orderFull;

        foreach (var cat in order)
        {
            var entries = ResolveEntries(cat, drawerMode);
            if (entries.Count == 0) continue;

            y = DrawTagMaskRow(rect, y, lineH, space, ulongProp, stateTagMask, isSlotMode,
                drawerMode == TagDrawerMode.InterruptOnly ? "interruption_mechanism" : TagCatalog.CategoryHeader(cat),
                drawerMode == TagDrawerMode.InterruptOnly
                    ? "Only AllowInterrupt* bits (e.g. jump_Interrupt, dodge_Interrupt)."
                    : null,
                entries);
        }

        EditorGUI.EndProperty();
    }

    static float DrawTagMaskRow(Rect rect, float y, float lineH, float space, SerializedProperty ulongProp,
        ulong stateTagMask, bool isSlotMode, string prefix, string rowTipOrNull, List<TagEntry> entries)
    {
        var rowRect = new Rect(rect.x, y, rect.width, lineH);
        var countSel = CountSelected(entries, stateTagMask);
        var prefixW = Mathf.Min(168f, rowRect.width * 0.38f);
        var prefixRect = new Rect(rowRect.x, rowRect.y, prefixW, lineH);
        var dropRect = new Rect(rowRect.x + prefixW + 4f, rowRect.y,
            Mathf.Max(100f, rowRect.width - prefixW - 4f), lineH);

        var prefixStyle = EditorStyles.miniLabel;
        var tip = string.IsNullOrEmpty(rowTipOrNull) ? prefix : rowTipOrNull;
        EditorGUI.LabelField(prefixRect,
            new GUIContent($"{prefix}  ({countSel}/{entries.Count})", tip), prefixStyle);

        var summaryBtn = BuildCategorySelectionSummary(entries, stateTagMask);
        var gc = new GUIContent(string.IsNullOrEmpty(summaryBtn) ? "(choose…)" : summaryBtn);

        if (EditorGUI.DropdownButton(dropRect, gc, FocusType.Passive))
        {
            ShowCategoryDropdown(dropRect, ulongProp, entries, isSlotMode);
        }

        return y + lineH + space;
    }

    static List<TagEntry> ResolveEntries(TagCategory cat, TagDrawerMode drawerMode)
    {
        if (drawerMode == TagDrawerMode.InterruptOnly)
        {
            return cat == TagCategory.Interrupt ? TagCatalog.ByCategory[TagCategory.Interrupt] : new List<TagEntry>();
        }

        return TagCatalog.ByCategory[cat];
    }

    public static float GetHeight(bool isSlotMode, TagDrawerMode drawerMode)
    {
        var lineH = EditorGUIUtility.singleLineHeight;
        var space = 2f;
        var h = lineH + space;
        if (drawerMode == TagDrawerMode.ActionWindow)
        {
            return h + (lineH + space) * 3f;
        }

        if (drawerMode == TagDrawerMode.WindowTimeline)
        {
            return h + (lineH + space) * 2f;
        }

        var order = drawerMode == TagDrawerMode.InterruptOnly ? s_orderInterruptOnly : s_orderFull;
        foreach (var cat in order)
        {
            var entries = ResolveEntries(cat, drawerMode);
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

    // ── 摘要：InterruptOnly 只统计 AllowInterrupt*（与下拉选项一致），避免旧序列化掩码混入其它位误导策划 ──
    static void DrawSelectionSummary(Rect rect, ulong stateTagMask, TagDrawerMode drawerMode)
    {
        var selected = new List<string>(8);
        if (drawerMode == TagDrawerMode.InterruptOnly)
        {
            foreach (var e in TagCatalog.ByCategory[TagCategory.Interrupt])
            {
                if ((stateTagMask & (ulong)e.Tag) != 0UL)
                {
                    selected.Add(e.Display);
                }
            }
        }
        else if (drawerMode == TagDrawerMode.WindowTimeline)
        {
            foreach (var e in TagCatalog.ByCategory[TagCategory.Interrupt])
            {
                if ((stateTagMask & (ulong)e.Tag) != 0UL)
                {
                    selected.Add(e.Display);
                }
            }

            foreach (var e in TagCatalog.ActionWindowTimeBehaviorEntries)
            {
                if ((stateTagMask & (ulong)e.Tag) != 0UL)
                {
                    selected.Add(e.Display);
                }
            }
        }
        else if (drawerMode == TagDrawerMode.ActionWindow)
        {
            foreach (var e in TagCatalog.ByCategory[TagCategory.Interrupt])
            {
                if ((stateTagMask & (ulong)e.Tag) != 0UL)
                {
                    selected.Add(e.Display);
                }
            }

            foreach (var e in TagCatalog.ActionWindowPhaseEntries)
            {
                if ((stateTagMask & (ulong)e.Tag) != 0UL)
                {
                    selected.Add(e.Display);
                }
            }

            foreach (var e in TagCatalog.ActionWindowTimeBehaviorEntries)
            {
                if ((stateTagMask & (ulong)e.Tag) != 0UL)
                {
                    selected.Add(e.Display);
                }
            }
        }
        else
        {
            foreach (var e in TagCatalog.All)
            {
                if ((stateTagMask & (ulong)e.Tag) != 0UL)
                {
                    selected.Add(e.Display);
                }
            }
        }

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
        return GroupedTagDrawer.GetHeight(isSlotMode: true, TagDrawerMode.ActionWindow);
    }

    internal static void DrawSlotMaskField(Rect rect, SerializedProperty prop, GUIContent label)
    {
        GroupedTagDrawer.Draw(rect, prop, label, isSlotMode: true, TagDrawerMode.ActionWindow);
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
            new GUIContent("timeline_StateTags", "Interrupt + combat_phase + time_WindowBehavior. Not GameplayTag."));

        y += tagH + space;
        var pEvents = property.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents));
        if (pEvents != null)
        {
            var evH = EditorGUI.GetPropertyHeight(pEvents, new GUIContent("timeline_RuntimeEvents"), true);
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, evH),
                pEvents,
                new GUIContent("timeline_RuntimeEvents", "HitFrame / PlaySfx / SpawnVfx — not Tags; see ActionWindowTimelineEvents."),
                true);
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var line = EditorGUIUtility.singleLineHeight;
        var space = EditorGUIUtility.standardVerticalSpacing;
        var pSlots = property.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask));
        var pEvents = property.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents));
        var tagH = ActionWindowSlotMaskDrawerUtility.GetMaskHeight(pSlots);
        var evH = pEvents != null
            ? EditorGUI.GetPropertyHeight(pEvents, new GUIContent("timeline_RuntimeEvents"), true)
            : 0f;
        return line + space + line + space + tagH + (pEvents != null ? space + evH : 0f);
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
        var mode = TagDrawerMode.Full;
        if (attribute is StateTagMaskAttribute st)
        {
            mode = st.Usage switch
            {
                StateTagMaskUsage.InterruptOnly => TagDrawerMode.InterruptOnly,
                StateTagMaskUsage.WindowTimeline => TagDrawerMode.WindowTimeline,
                _ => TagDrawerMode.Full,
            };
        }

        StateTagMaskDrawerUtility.DrawMaskField(position, property, label, mode);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var mode = TagDrawerMode.Full;
        if (attribute is StateTagMaskAttribute st)
        {
            mode = st.Usage switch
            {
                StateTagMaskUsage.InterruptOnly => TagDrawerMode.InterruptOnly,
                StateTagMaskUsage.WindowTimeline => TagDrawerMode.WindowTimeline,
                _ => TagDrawerMode.Full,
            };
        }

        return StateTagMaskDrawerUtility.GetMaskHeight(property, label, mode);
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
