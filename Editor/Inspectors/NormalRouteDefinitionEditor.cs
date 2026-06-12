#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NormalRouteDefinition))]
public sealed class NormalRouteDefinitionEditor : Editor
{
    static readonly string[] s_excludeFields =
    {
        "m_Script",
        "singleAction",
        "stages",
        "ownerGroup",
        "overrideCooldown",
        "overrideIcon",
        "overrideCost",
    };

    SerializedProperty _singleAction;
    SerializedProperty _stages;

    void OnEnable()
    {
        _singleAction = serializedObject.FindProperty("singleAction");
        _stages = serializedObject.FindProperty("stages");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var route = (NormalRouteDefinition)target;
        RouteGraphTypeInspectorDrawer.Draw(route);

        DrawPropertiesExcluding(serializedObject, s_excludeFields);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Graph / Stage", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            _singleAction,
            new GUIContent(
                "Single Action Mode",
                "开启时 Stage 数组锁定为 1，Graph Flow 边仅使用 Stage[0]。"));

        if (_singleAction.boolValue)
        {
            if (_stages.arraySize != 1)
            {
                _stages.arraySize = 1;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Stages.Length (locked)", _stages.arraySize);
            }
        }

        EditorGUILayout.PropertyField(_stages, new GUIContent("Stages"), true);

        SkillRouteGroupMembershipDrawer.Draw(serializedObject);
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
