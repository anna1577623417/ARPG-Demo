#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillRoute Inspector — SkillGroup 隶属与 Override 位（默认折叠，非日常必填）。
/// </summary>
public static class SkillRouteGroupMembershipDrawer
{
    static bool s_foldout;

    public static void Draw(SerializedObject serializedObject)
    {
        if (serializedObject == null)
        {
            return;
        }

        EditorGUILayout.Space(6f);
        s_foldout = EditorGUILayout.BeginFoldoutHeaderGroup(s_foldout, "SkillGroup 隶属（可选）");
        if (!s_foldout)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            return;
        }

        EditorGUILayout.HelpBox(
            "Route 独立运行时 ownerGroup 留空。收编进 Group 后 CD/Icon/Cost 默认读 Group；Override 勾选则使用本 Route 字段。",
            MessageType.None);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("ownerGroup"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overrideCooldown"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overrideIcon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overrideCost"));

        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}
#endif
