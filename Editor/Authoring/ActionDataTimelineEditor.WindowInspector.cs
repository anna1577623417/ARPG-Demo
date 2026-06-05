#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataTimelineEditor
{
    bool _foldWinTiming = true;
    bool _foldWinInterrupt = true;
    bool _foldWinCombat = true;
    bool _foldWinEvents;

    void DrawWindowClipProperties(SerializedProperty elem, int index)
    {
        var s = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedStart)).floatValue;
        var e = elem.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd)).floatValue;
        ActionTimelineEditorUI.SectionHeader($"片段 [{index}]", $"{s:0.00} – {e:0.00} 归一化时间");

        DrawCombatTagToggles(elem);
        EditorGUILayout.Space(4f);

        _foldWinTiming = ActionTimelineEditorUI.Foldout(_foldWinTiming, "时间 / Phase");
        if (_foldWinTiming)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionWindow.NormalizedStart)),
                    new GUIContent("起点"));
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionWindow.NormalizedEnd)),
                    new GUIContent("终点"));
            }
        }

        _foldWinInterrupt = ActionTimelineEditorUI.Foldout(_foldWinInterrupt, "打断");
        if (_foldWinInterrupt)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionWindow.InterruptibleByCategories)),
                    new GUIContent("允许类别", "Movement / Offense / Defensive / Utility"));
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionWindow.MinIncomingPriority)),
                    new GUIContent("最低优先级"));
                EditorGUILayout.PropertyField(
                    elem.FindPropertyRelative(nameof(ActionWindow.AllowSelfInterrupt)),
                    new GUIContent("允许自打断"));
            }
        }

        _foldWinCombat = ActionTimelineEditorUI.Foldout(_foldWinCombat, "战斗状态（Hitbox / Hurtbox / …）");
        if (_foldWinCombat)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                var pSlots = elem.FindPropertyRelative(nameof(ActionWindow.WindowSlotMask));
                var tagH = ActionWindowSlotMaskDrawerUtility.GetCompactMaskHeight(pSlots);
                var rect = EditorGUILayout.GetControlRect(false, tagH);
                ActionWindowSlotMaskDrawerUtility.DrawCompactSlotMaskField(
                    rect,
                    pSlots,
                    new GUIContent("状态标签", "写入 GameplayTag 时间轨"));
            }
        }

        _foldWinEvents = ActionTimelineEditorUI.Foldout(_foldWinEvents, "Runtime Event（窗内事件）");
        if (_foldWinEvents)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                var pEvents = elem.FindPropertyRelative(nameof(ActionWindow.RuntimeEvents));
                if (pEvents != null)
                {
                    EditorGUILayout.PropertyField(pEvents, GUIContent.none, true);
                }
            }
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
