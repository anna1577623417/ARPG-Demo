#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>224.1 L2 — 共享 CO 引用影响与复制提示。</summary>
public static class ContactAuthoringSharedImpactUI
{
    public static void Draw(CombatObjectDefinitionSO definition)
    {
        if (definition == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("References / Shared Impact", EditorStyles.boldLabel);
        var refs = CombatObjectReferenceIndex.GetContactsForDefinition(definition);
        EditorGUILayout.LabelField($"Contact references: {refs.Count}");
        if (refs.Count > 1)
        {
            EditorGUILayout.HelpBox(
                "多个 ContactEvent 共享此 CO。修改 Binding/Pose 会影响全部引用预览与运行。",
                MessageType.Warning);
        }

        for (var i = 0; i < refs.Count && i < 8; i++)
        {
            var r = refs[i];
            EditorGUILayout.LabelField($"• {r.ActionName} / {r.DebugName} ({r.EventId})");
        }

        if (refs.Count > 8)
        {
            EditorGUILayout.LabelField($"… and {refs.Count - 8} more");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Reference Index"))
            {
                CombatObjectReferenceIndex.Invalidate();
            }

            if (GUILayout.Button("Duplicate CO Variant"))
            {
                var copy = CombatObjectAuthoringService.DuplicateForContact(definition, null, null);
                if (copy != null)
                {
                    Selection.activeObject = copy;
                    EditorGUIUtility.PingObject(copy);
                }
            }
        }
    }
}
#endif
