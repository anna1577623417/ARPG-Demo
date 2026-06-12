#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>150.3 P2 — Condition 资产拖放到 Flow 边。</summary>
public static class CombatFlowConditionDragDrop
{
    public static bool DrawDropZoneAndPoolPicker(
        CombatFlowConditionDefinition[] pool,
        ref CombatFlowConditionDefinition[] refs)
    {
        var changed = false;

        var dropRect = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
        GUI.Box(
            dropRect,
            "将 FlowCondition 资产拖到此处（须在 Graph.conditionPool）",
            EditorStyles.helpBox);

        changed |= HandleDragOnRect(dropRect, pool, ref refs);
        changed |= DrawPoolQuickPick(pool, ref refs);
        return changed;
    }

    static bool HandleDragOnRect(
        Rect rect,
        CombatFlowConditionDefinition[] pool,
        ref CombatFlowConditionDefinition[] refs)
    {
        var evt = Event.current;
        if (!rect.Contains(evt.mousePosition))
        {
            return false;
        }

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!TryGetDraggedCondition(pool, out var def))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    return false;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    AppendRef(ref refs, def);
                    evt.Use();
                    return true;
                }

                evt.Use();
                return false;
            default:
                return false;
        }
    }

    static bool DrawPoolQuickPick(
        CombatFlowConditionDefinition[] pool,
        ref CombatFlowConditionDefinition[] refs)
    {
        if (pool == null || pool.Length == 0)
        {
            return false;
        }

        EditorGUILayout.LabelField("从 conditionPool 添加", EditorStyles.miniBoldLabel);
        var changed = false;
        const int perRow = 3;
        for (var i = 0; i < pool.Length; i += perRow)
        {
            EditorGUILayout.BeginHorizontal();
            for (var j = 0; j < perRow && i + j < pool.Length; j++)
            {
                var def = pool[i + j];
                if (def == null)
                {
                    continue;
                }

                var label = string.IsNullOrEmpty(def.DisplayName) ? def.name : def.DisplayName;
                if (GUILayout.Button(label, EditorStyles.miniButton))
                {
                    AppendRef(ref refs, def);
                    changed = true;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        return changed;
    }

    static bool TryGetDraggedCondition(
        CombatFlowConditionDefinition[] pool,
        out CombatFlowConditionDefinition def)
    {
        def = null;
        var refs = DragAndDrop.objectReferences;
        if (refs == null || refs.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < refs.Length; i++)
        {
            if (refs[i] is not CombatFlowConditionDefinition candidate)
            {
                continue;
            }

            if (pool != null && pool.Length > 0 && !Contains(pool, candidate))
            {
                continue;
            }

            def = candidate;
            return true;
        }

        return false;
    }

    public static void AppendRef(ref CombatFlowConditionDefinition[] refs, CombatFlowConditionDefinition def)
    {
        if (def == null)
        {
            return;
        }

        if (refs != null)
        {
            for (var i = 0; i < refs.Length; i++)
            {
                if (refs[i] == def)
                {
                    return;
                }
            }
        }

        var size = refs?.Length ?? 0;
        var resized = new CombatFlowConditionDefinition[size + 1];
        if (refs != null)
        {
            Array.Copy(refs, resized, size);
        }

        resized[size] = def;
        refs = resized;
    }

    static bool Contains(CombatFlowConditionDefinition[] pool, CombatFlowConditionDefinition def)
    {
        for (var i = 0; i < pool.Length; i++)
        {
            if (pool[i] == def)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
