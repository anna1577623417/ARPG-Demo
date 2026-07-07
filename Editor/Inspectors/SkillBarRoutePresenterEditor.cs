#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(SkillBarRoutePresenter))]
public sealed class SkillBarRoutePresenterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        var presenter = (SkillBarRoutePresenter)target;
        var groupProp = serializedObject.FindProperty("groupWidgetsByEntry");
        var rootProp = serializedObject.FindProperty("widgetsRoot");

        if (groupProp != null && groupProp.boolValue && rootProp?.objectReferenceValue is Transform root)
        {
            if (root.GetComponent<GridLayoutGroup>() != null)
            {
                EditorGUILayout.HelpBox(
                    "Widgets Root 已挂 Grid Layout Group，且【Group Widgets By Entry】已勾选。\n" +
                    "运行时 HorizontalLayoutGroup 会与 Grid 冲突，易出现槽位/文字叠影。\n" +
                    "建议取消勾选，让 Grid 单独排布。",
                    MessageType.Warning);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
