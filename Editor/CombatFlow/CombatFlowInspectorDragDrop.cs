#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Graph Inspector IMGUI 区拖放（GraphView 同窗时 ObjectField 默认常失效）。</summary>
public static class CombatFlowInspectorDragDrop
{
    public static T DrawObjectField<T>(
        string label,
        T value,
        UnityEngine.Object[] pool = null,
        bool allowSceneObjects = false) where T : UnityEngine.Object
    {
        var rect = EditorGUILayout.GetControlRect();
        var newValue = (T)EditorGUI.ObjectField(
            rect,
            label,
            value,
            typeof(T),
            allowSceneObjects);

        if (TryAcceptDragOnRect(rect, pool, typeof(T), out var dragged))
        {
            newValue = (T)dragged;
            GUI.changed = true;
        }

        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Arrow);
        return newValue;
    }

    public static bool DrawDropZone(
        string hint,
        UnityEngine.Object[] pool,
        Type acceptedType,
        out UnityEngine.Object assigned)
    {
        assigned = null;
        var rect = GUILayoutUtility.GetRect(0f, 36f, GUILayout.ExpandWidth(true));
        GUI.Box(rect, hint, EditorStyles.helpBox);
        return TryAcceptDragOnRect(rect, pool, acceptedType, out assigned);
    }

    public static bool TryAcceptDragOnRect(
        Rect rect,
        UnityEngine.Object[] pool,
        Type acceptedType,
        out UnityEngine.Object assigned)
    {
        assigned = null;
        var evt = Event.current;
        if (!rect.Contains(evt.mousePosition))
        {
            return false;
        }

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!TryGetDraggedObject(acceptedType, pool, out assigned))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    return false;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    evt.Use();
                    GUI.changed = true;
                    return true;
                }

                evt.Use();
                return false;
            default:
                return false;
        }
    }

    static bool TryGetDraggedObject(
        Type acceptedType,
        UnityEngine.Object[] pool,
        out UnityEngine.Object obj)
    {
        obj = null;
        var refs = DragAndDrop.objectReferences;
        if (refs == null || refs.Length == 0 || acceptedType == null)
        {
            return false;
        }

        for (var i = 0; i < refs.Length; i++)
        {
            var candidate = refs[i];
            if (candidate == null || !acceptedType.IsInstanceOfType(candidate))
            {
                continue;
            }

            obj = candidate;
            return true;
        }

        return false;
    }

    static bool Contains(UnityEngine.Object[] pool, UnityEngine.Object obj)
    {
        for (var i = 0; i < pool.Length; i++)
        {
            if (pool[i] == obj)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
