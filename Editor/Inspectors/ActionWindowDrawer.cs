#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ActionWindow 折叠分区绘制（140.1）；时间轴编辑器用专用属性栏，此处服务其它 Inspector。
/// </summary>
[CustomPropertyDrawer(typeof(ActionWindow))]
internal sealed class ActionWindowDrawer : PropertyDrawer
{
    static bool s_foldTiming = true;
    static bool s_foldInterrupt = true;
    static bool s_foldCombat = true;
    static bool s_foldEvents;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var y = position.y;
        var width = position.width;
        var line = EditorGUIUtility.singleLineHeight;
        var space = EditorGUIUtility.standardVerticalSpacing;

        if (!string.IsNullOrEmpty(label.text))
        {
            var headerRect = new Rect(position.x, y, width, line);
            EditorGUI.LabelField(headerRect, label, EditorStyles.boldLabel);
            y += line + space;
        }

        y = DrawTimingSection(property, position.x, y, width, line, space);
        y = DrawInterruptSection(property, position.x, y, width, line, space);
        y = DrawCombatSection(property, position.x, y, width, line, space);
        DrawEventsSection(property, position.x, y, width, line, space);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var line = EditorGUIUtility.singleLineHeight;
        var space = EditorGUIUtility.standardVerticalSpacing;
        var h = string.IsNullOrEmpty(label.text) ? 0f : line + space;

        h += SectionHeight(s_foldTiming, 1, line, space);
        h += SectionHeight(s_foldInterrupt, 2, line, space);
        h += SectionHeight(s_foldCombat, GetCombatContentLines(property), line, space);
        h += SectionHeight(s_foldEvents, GetEventsLines(property), line, space);
        return h;
    }

    static float SectionHeight(bool expanded, int lines, float line, float space) =>
        (expanded ? EditorGUIUtility.singleLineHeight + space : 0f)
        + (expanded ? lines * (line + space) : 0f);

    static int GetCombatContentLines(SerializedProperty property)
    {
        var pSlots = property.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask));
        var tagH = ActionWindowSlotMaskDrawerUtility.GetCompactMaskHeight(pSlots);
        return Mathf.Max(1, Mathf.CeilToInt(tagH / EditorGUIUtility.singleLineHeight));
    }

    static int GetEventsLines(SerializedProperty property)
    {
        var pEvents = property.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents));
        if (!s_foldEvents || pEvents == null)
        {
            return 0;
        }

        return Mathf.CeilToInt(EditorGUI.GetPropertyHeight(pEvents, true) / EditorGUIUtility.singleLineHeight);
    }

    static float DrawTimingSection(
        SerializedProperty property,
        float x,
        float y,
        float width,
        float line,
        float space)
    {
        var foldRect = new Rect(x, y, width, line);
        s_foldTiming = EditorGUI.Foldout(foldRect, s_foldTiming, "时间", true);
        y += line + space;
        if (!s_foldTiming)
        {
            return y;
        }

        var pStart = property.FindPropertyRelative(nameof(ActionWindow.NormalizedStart));
        var pEnd = property.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd));
        var half = (width - 8f) * 0.5f;
        EditorGUI.PropertyField(new Rect(x, y, half, line), pStart, new GUIContent("起点"));
        EditorGUI.PropertyField(new Rect(x + half + 8f, y, half, line), pEnd, new GUIContent("终点"));
        return y + line + space;
    }

    static float DrawInterruptSection(
        SerializedProperty property,
        float x,
        float y,
        float width,
        float line,
        float space)
    {
        var foldRect = new Rect(x, y, width, line);
        s_foldInterrupt = EditorGUI.Foldout(foldRect, s_foldInterrupt, "打断", true);
        y += line + space;
        if (!s_foldInterrupt)
        {
            return y;
        }

        var pCategories = property.FindPropertyRelative(nameof(ActionWindow.InterruptibleByCategories));
        var catH = ActionTimelineEditorUI.CompactPropertyContext
            ? line
            : EditorGUI.GetPropertyHeight(pCategories, new GUIContent("允许类别"), true);
        EditorGUI.PropertyField(new Rect(x, y, width, catH), pCategories, new GUIContent("允许类别"));
        y += catH + space;

        if (!ActionTimelineEditorUI.CompactPropertyContext)
        {
            const float helpH = 36f;
            EditorGUI.HelpBox(
                new Rect(x, y, width, helpH),
                "IdleFallback 类不会被本字段消费（FSM 兜底不走打断路径）。WASD 打断勾 Movement / Locomotion。",
                MessageType.Info);
            y += helpH + space;
        }

        var pMinPri = property.FindPropertyRelative(nameof(ActionWindow.MinIncomingPriority));
        var half = (width - 8f) * 0.5f;
        EditorGUI.PropertyField(new Rect(x, y, half, line), pMinPri, new GUIContent("最低优先级"));
        var pSelf = property.FindPropertyRelative(nameof(ActionWindow.AllowSelfInterrupt));
        EditorGUI.PropertyField(new Rect(x + half + 8f, y, half, line), pSelf, new GUIContent("自打断"));
        return y + line + space;
    }

    static float DrawCombatSection(
        SerializedProperty property,
        float x,
        float y,
        float width,
        float line,
        float space)
    {
        var foldRect = new Rect(x, y, width, line);
        s_foldCombat = EditorGUI.Foldout(foldRect, s_foldCombat, "战斗状态", true);
        y += line + space;
        if (!s_foldCombat)
        {
            return y;
        }

        var pSlots = property.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask));
        var tagH = ActionWindowSlotMaskDrawerUtility.GetCompactMaskHeight(pSlots);
        ActionWindowSlotMaskDrawerUtility.DrawCompactSlotMaskField(
            new Rect(x, y, width, tagH),
            pSlots,
            GUIContent.none);
        return y + tagH + space;
    }

    static float DrawEventsSection(
        SerializedProperty property,
        float x,
        float y,
        float width,
        float line,
        float space)
    {
        var foldRect = new Rect(x, y, width, line);
        s_foldEvents = EditorGUI.Foldout(foldRect, s_foldEvents, "窗内事件", true);
        y += line + space;
        if (!s_foldEvents)
        {
            return y;
        }

        var pEvents = property.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents));
        if (pEvents == null)
        {
            return y;
        }

        var evH = EditorGUI.GetPropertyHeight(pEvents, true);
        EditorGUI.PropertyField(new Rect(x, y, width, evH), pEvents, GUIContent.none, true);
        return y + evH;
    }
}
#endif
