#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Flow 边 / Condition 资产 — 内联条件 IMGUI（SerializedObject 编辑 struct）。</summary>
public static class SkillTransitionConditionAuthoringDrawer
{
    sealed class ConditionScratch : ScriptableObject
    {
        public SkillTransitionCondition value;
    }

    static ConditionScratch s_scratch;

    public static void DrawArray(ref SkillTransitionCondition[] conditions)
    {
        var size = conditions?.Length ?? 0;
        EditorGUILayout.LabelField("Inline Conditions (AND)", EditorStyles.miniBoldLabel);
        var newSize = EditorGUILayout.IntField("Count", size);
        if (newSize < 0)
        {
            newSize = 0;
        }

        if (newSize != size)
        {
            var resized = new SkillTransitionCondition[newSize];
            if (conditions != null)
            {
                var copy = Mathf.Min(conditions.Length, newSize);
                for (var i = 0; i < copy; i++)
                {
                    resized[i] = conditions[i];
                }
            }

            conditions = resized;
        }

        if (conditions == null)
        {
            return;
        }

        EnsureScratch();
        for (var i = 0; i < conditions.Length; i++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Inline #{i}", EditorStyles.miniLabel);
            s_scratch.value = conditions[i];
            var so = new SerializedObject(s_scratch);
            so.Update();
            var prop = so.FindProperty(nameof(ConditionScratch.value));
            EditorGUILayout.PropertyField(prop, GUIContent.none, true);
            so.ApplyModifiedPropertiesWithoutUndo();
            conditions[i] = s_scratch.value;
            EditorGUILayout.EndVertical();
        }
    }

    static void EnsureScratch()
    {
        if (s_scratch != null)
        {
            return;
        }

        s_scratch = ScriptableObject.CreateInstance<ConditionScratch>();
        s_scratch.hideFlags = HideFlags.HideAndDontSave;
    }
}
#endif
