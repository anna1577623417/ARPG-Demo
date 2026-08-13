#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 224.1 L2 — Contact↔CO 反向索引。按 EventId 稳定，不按数组 index 缓存。
/// 大范围 AssetDatabase 扫描只在失效后执行一次。
/// </summary>
public static class CombatObjectReferenceIndex
{
    static bool s_dirty = true;
    static readonly Dictionary<string, List<ContactReference>> s_byDefinition =
        new Dictionary<string, List<ContactReference>>(64);
    static readonly Dictionary<string, string> s_eventToDefinition =
        new Dictionary<string, string>(128);

    public readonly struct ContactReference
    {
        public readonly string ActionPath;
        public readonly string ActionName;
        public readonly string EventId;
        public readonly string DebugName;

        public ContactReference(string actionPath, string actionName, string eventId, string debugName)
        {
            ActionPath = actionPath ?? string.Empty;
            ActionName = actionName ?? string.Empty;
            EventId = eventId ?? string.Empty;
            DebugName = debugName ?? string.Empty;
        }
    }

    public static void Invalidate() => s_dirty = true;

    public static IReadOnlyList<ContactReference> GetContactsForDefinition(CombatObjectDefinitionSO definition)
    {
        EnsureBuilt();
        if (definition == null) return System.Array.Empty<ContactReference>();
        var key = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition));
        if (string.IsNullOrEmpty(key)) key = definition.name;
        return s_byDefinition.TryGetValue(key, out var list)
            ? list
            : (IReadOnlyList<ContactReference>)System.Array.Empty<ContactReference>();
    }

    public static int CountContactsForDefinition(CombatObjectDefinitionSO definition) =>
        GetContactsForDefinition(definition).Count;

    static void EnsureBuilt()
    {
        if (!s_dirty) return;
        s_byDefinition.Clear();
        s_eventToDefinition.Clear();

        var guids = AssetDatabase.FindAssets("t:ActionDataSO");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var action = AssetDatabase.LoadAssetAtPath<ActionDataSO>(path);
            if (action?.ContactEvents == null) continue;

            for (var e = 0; e < action.ContactEvents.Count; e++)
            {
                var contact = action.ContactEvents[e];
                if (contact.Definition == null || !ContactEventId.IsValid(contact.EventId)) continue;

                var defPath = AssetDatabase.GetAssetPath(contact.Definition);
                var defKey = AssetDatabase.AssetPathToGUID(defPath);
                if (string.IsNullOrEmpty(defKey)) defKey = contact.Definition.name;

                if (!s_byDefinition.TryGetValue(defKey, out var list))
                {
                    list = new List<ContactReference>(4);
                    s_byDefinition[defKey] = list;
                }

                list.Add(new ContactReference(path, action.name, contact.EventId, contact.DebugName));
                s_eventToDefinition[contact.EventId] = defKey;
            }
        }

        s_dirty = false;
    }
}
#endif
