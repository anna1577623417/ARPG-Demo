#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataTimelineEditor
{
    // 172.2 W0/W1：右属性面板改为垂直 SectionSeparator + VerticalField；旧 Foldout 折叠状态字段不再需要。

    void DrawWindowClipProperties(SerializedProperty elem, int index)
    {
        var s = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedStart)).floatValue;
        var e = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd)).floatValue;
        ActionTimelineEditorUI.SectionHeader($"片段 [{index}]", $"{s:0.00} – {e:0.00} 归一化时间");

        DrawCombatTagToggles(elem);
        EditorGUILayout.Space(4f);

        // 172.2 W0/W1：右属性面板垂直化 —— label 顶置 / 控件全宽 / SectionSeparator 替代 Foldout
        ActionTimelineEditorUI.SectionSeparator("时间 / Phase");
        ActionTimelineEditorUI.VerticalField(
            elem.FindPropertyRelative(nameof(ActionWindow.NormalizedStart)),
            new GUIContent("起点 (Start)", "归一化时间 0~1"));
        ActionTimelineEditorUI.VerticalField(
            elem.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd)),
            new GUIContent("终点 (End)", "归一化时间 0~1"));

        ActionTimelineEditorUI.SectionSeparator("打断 (Interrupt)");
        ActionTimelineEditorUI.VerticalField(
            elem.FindPropertyRelative(nameof(ActionWindow.InterruptibleByCategories)),
            new GUIContent("允许类别", "Movement / Offense / Defensive / Utility"));
        ActionTimelineEditorUI.VerticalField(
            elem.FindPropertyRelative(nameof(ActionWindow.MinIncomingPriority)),
            new GUIContent("最低优先级", "传入意图需 ≥ 此值才能打断"));
        EditorGUILayout.PropertyField(
            elem.FindPropertyRelative(nameof(ActionWindow.AllowSelfInterrupt)),
            new GUIContent("允许自打断", "允许同一 Action 类自打断本窗"));
        GUILayout.Space(ActionTimelineEditorUI.VerticalFieldSpacing);

        ActionTimelineEditorUI.SectionSeparator("战斗状态（Hurtbox / Invuln / …）");
        var pSlots = elem.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask));
        EditorGUILayout.LabelField(new GUIContent("状态标签", "写入 GameplayTag 时间轨"), EditorStyles.miniLabel);
        GUILayout.Space(ActionTimelineEditorUI.LabelToControlSpacing);
        {
            var tagH = ActionWindowSlotMaskDrawerUtility.GetCompactMaskHeight(pSlots);
            var rect = EditorGUILayout.GetControlRect(false, tagH);
            ActionWindowSlotMaskDrawerUtility.DrawCompactSlotMaskField(rect, pSlots, GUIContent.none);
        }
        GUILayout.Space(ActionTimelineEditorUI.VerticalFieldSpacing);

        ActionTimelineEditorUI.SectionSeparator("Runtime Event（窗内事件）");
        var pEvents = elem.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents));
        if (pEvents != null)
        {
            EditorGUILayout.PropertyField(pEvents, GUIContent.none, true);
        }
    }

    void DrawTeleportProperties(SerializedProperty elem, int index)
    {
        ActionTimelineEditorUI.SectionHeader($"瞬移 [{index}]");
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(TeleportTrigger.TriggerTime)),
            new GUIContent("时刻"));
        EditorGUILayout.PropertyField(elem.FindPropertyRelative(nameof(TeleportTrigger.Distance)),
            new GUIContent("距离 (m)"));
    }

    void DrawEmptyPropertiesHint()
    {
        EditorGUILayout.HelpBox(
            "在时间轴上单击片段、◆ 标记或瞬移点。\n" +
            "拖拽边缘改起止，拖拽条带平移，双击空轨创建。\n" +
            "选中后按 Delete / Backspace 或点 − 删除。",
            MessageType.Info);
    }
}
#endif
