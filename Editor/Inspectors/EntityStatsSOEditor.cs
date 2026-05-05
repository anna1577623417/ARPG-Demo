#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EntityStatsSO), true)]
public sealed class EntityStatsSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var stats = target as EntityStatsSO;
        if (stats == null)
        {
            return;
        }

        var issues = Validate(stats);
        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("BaseStats validation: no issues.", MessageType.Info);
            return;
        }

        foreach (var issue in issues)
        {
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Auto Fix BaseStats"))
        {
            AutoFixBaseStats(serializedObject);
        }
    }

    static List<string> Validate(EntityStatsSO stats)
    {
        var issues = new List<string>(8);
        var seen = new HashSet<StatType>();
        var list = stats.BaseStats;
        for (var i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (!seen.Add(e.Type))
            {
                issues.Add($"Duplicate StatType at index {i}: {e.Type}.");
            }

            if (float.IsNaN(e.BaseValue) || float.IsInfinity(e.BaseValue))
            {
                issues.Add($"Invalid number at index {i}: {e.Type} = {e.BaseValue}.");
                continue;
            }

            if ((e.Type == StatType.MaxHealth || e.Type == StatType.MaxStamina) && e.BaseValue < 1f)
            {
                issues.Add($"{e.Type} should be >= 1 (index {i}, value={e.BaseValue}).");
            }

            if ((e.Type == StatType.WalkSpeed
                 || e.Type == StatType.RunSpeed
                 || e.Type == StatType.RotationSpeed
                 || e.Type == StatType.Poise)
                && e.BaseValue < 0f)
            {
                issues.Add($"{e.Type} should be >= 0 (index {i}, value={e.BaseValue}).");
            }
        }

        return issues;
    }

    static void AutoFixBaseStats(SerializedObject so)
    {
        if (so == null || so.targetObject == null)
        {
            return;
        }

        Undo.RecordObject(so.targetObject, "Auto Fix BaseStats");
        so.Update();

        var baseStats = so.FindProperty("baseStats");
        if (baseStats == null || !baseStats.isArray)
        {
            so.ApplyModifiedProperties();
            return;
        }

        // Deduplicate by StatType, keep the last occurrence as source of truth.
        var seen = new HashSet<int>();
        for (var i = baseStats.arraySize - 1; i >= 0; i--)
        {
            var element = baseStats.GetArrayElementAtIndex(i);
            var typeProp = element.FindPropertyRelative("Type");
            if (typeProp == null)
            {
                continue;
            }

            var typeRaw = typeProp.intValue;
            if (!seen.Add(typeRaw))
            {
                baseStats.DeleteArrayElementAtIndex(i);
            }
        }

        // Sanitize illegal values.
        for (var i = 0; i < baseStats.arraySize; i++)
        {
            var element = baseStats.GetArrayElementAtIndex(i);
            var typeProp = element.FindPropertyRelative("Type");
            var valueProp = element.FindPropertyRelative("BaseValue");
            if (typeProp == null || valueProp == null)
            {
                continue;
            }

            var type = (StatType)typeProp.intValue;
            var value = valueProp.floatValue;
            valueProp.floatValue = Sanitize(type, value);
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(so.targetObject);
    }

    static float Sanitize(StatType type, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            value = 0f;
        }

        if (type == StatType.MaxHealth || type == StatType.MaxStamina)
        {
            return Mathf.Max(1f, value);
        }

        if (type == StatType.WalkSpeed
            || type == StatType.RunSpeed
            || type == StatType.RotationSpeed
            || type == StatType.Poise)
        {
            return Mathf.Max(0f, value);
        }

        return value;
    }
}
#endif
