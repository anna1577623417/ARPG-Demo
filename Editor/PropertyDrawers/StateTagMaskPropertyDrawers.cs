#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 不支持序列化 ulong 为底层的 <see cref="StateTag"/> 字段；用 ulong 存位 + 本抽屉在 Inspector 里按枚举勾选。
/// </summary>
internal static class StateTagMaskDrawerUtility
{
    static readonly StateTag[] s_singleBitTags = BuildSingleBitTags();

    static StateTag[] BuildSingleBitTags()
    {
        var list = new System.Collections.Generic.List<StateTag>();
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

            list.Add(e);
        }

        list.Sort((a, b) => ((ulong)a).CompareTo((ulong)b));
        return list.ToArray();
    }

    internal static ulong ReadULong(SerializedProperty prop)
    {
#if UNITY_2020_2_OR_NEWER
        return prop.ulongValue;
#else
        unchecked
        {
            return (ulong)prop.longValue;
        }
#endif
    }

    internal static void WriteULong(SerializedProperty prop, ulong value)
    {
#if UNITY_2020_2_OR_NEWER
        prop.ulongValue = value;
#else
        unchecked
        {
            prop.longValue = (long)value;
        }
#endif
    }

    internal static float GetMaskHeight()
    {
        const int cols = 2;
        var rows = Mathf.CeilToInt((float)s_singleBitTags.Length / cols);
        return EditorGUIUtility.singleLineHeight
            + rows * (EditorGUIUtility.singleLineHeight + 1f);
    }

    internal static void DrawMaskField(Rect rect, SerializedProperty ulongProp, GUIContent label)
    {
        var value = ReadULong(ulongProp);
        var lineH = EditorGUIUtility.singleLineHeight;

        var header = new Rect(rect.x, rect.y, rect.width, lineH);
        EditorGUI.PrefixLabel(header, label);

        const int cols = 2;
        var colW = (rect.width - 4f) / cols;
        var y = rect.y + lineH + 2f;

        EditorGUI.BeginChangeCheck();
        var newVal = value;

        for (var i = 0; i < s_singleBitTags.Length; i++)
        {
            var tag = s_singleBitTags[i];
            var bit = (ulong)tag;
            var col = i % cols;
            var row = i / cols;
            var r = new Rect(rect.x + col * colW, y + row * (lineH + 1f), colW - 2f, lineH);
            var on = (newVal & bit) != 0UL;
            var name = ObjectNames.NicifyVariableName(tag.ToString());
            on = EditorGUI.ToggleLeft(r, name, on);
            if (on)
            {
                newVal |= bit;
            }
            else
            {
                newVal &= ~bit;
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            WriteULong(ulongProp, newVal);
        }
    }
}

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

        var tagH = ActionWindowSlotMaskDrawerUtility.GetMaskHeight();
        ActionWindowSlotMaskDrawerUtility.DrawSlotMaskField(
            new Rect(position.x, y, position.width, tagH),
            pSlots,
            new GUIContent("Window slots (Init0–63)"));

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var line = EditorGUIUtility.singleLineHeight;
        var space = EditorGUIUtility.standardVerticalSpacing;
        return line + space + line + space + ActionWindowSlotMaskDrawerUtility.GetMaskHeight();
    }
}

/// <summary>ActionWindow：64 个 Init 槽位勾选，与内部 <see cref="StateTag"/> 位布局解耦。</summary>
internal static class ActionWindowSlotMaskDrawerUtility
{
    const int Cols = 4;

    internal static float GetMaskHeight()
    {
        var rows = Mathf.CeilToInt((float)ActionWindowTagSlots.SlotCount / Cols);
        return EditorGUIUtility.singleLineHeight
            + rows * (EditorGUIUtility.singleLineHeight + 1f);
    }

    internal static void DrawSlotMaskField(Rect rect, SerializedProperty ulongProp, GUIContent label)
    {
        var value = StateTagMaskDrawerUtility.ReadULong(ulongProp);
        var lineH = EditorGUIUtility.singleLineHeight;

        var header = new Rect(rect.x, rect.y, rect.width, lineH);
        EditorGUI.PrefixLabel(header, label);

        var colW = (rect.width - 4f) / Cols;
        var y = rect.y + lineH + 2f;

        EditorGUI.BeginChangeCheck();
        var newVal = value;

        for (var i = 0; i < ActionWindowTagSlots.SlotCount; i++)
        {
            var bit = 1UL << i;
            var col = i % Cols;
            var row = i / Cols;
            var r = new Rect(rect.x + col * colW, y + row * (lineH + 1f), colW - 2f, lineH);
            var mapped = ActionWindowTagSlots.GetSlotLabel(i);
            var name = mapped != null ? $"Init{i}: {ObjectNames.NicifyVariableName(mapped)}" : $"Init{i}";
            var on = (newVal & bit) != 0UL;
            on = EditorGUI.ToggleLeft(r, name, on);
            if (on)
            {
                newVal |= bit;
            }
            else
            {
                newVal &= ~bit;
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            StateTagMaskDrawerUtility.WriteULong(ulongProp, newVal);
        }
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
            if (child == null)
            {
                continue;
            }

            float h;
            if (child.name == nameof(ActionChargeConfig.ChargedPayloadTags))
            {
                h = StateTagMaskDrawerUtility.GetMaskHeight();
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
            if (child == null)
            {
                continue;
            }

            if (child.name == nameof(ActionChargeConfig.ChargedPayloadTags))
            {
                h += StateTagMaskDrawerUtility.GetMaskHeight();
            }
            else
            {
                h += EditorGUI.GetPropertyHeight(child, true);
            }

            h += space;
        }

        return h;
    }
}
#endif
