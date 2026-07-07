#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillRouteDefinition), true)]
public sealed class SkillRouteDefinitionEditor : Editor
{
    static readonly string[] s_excludeGroupFields =
    {
        "m_Script",
        "ownerGroup",
        "overrideCooldown",
        "overrideIcon",
        "overrideCost",
        "showOnHud",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (target is SkillRouteDefinition route)
        {
            RouteGraphTypeInspectorDrawer.Draw(route);
        }

        DrawPropertiesExcluding(serializedObject, s_excludeGroupFields);
        DrawShowOnHudField();
        SkillRouteGroupMembershipDrawer.Draw(serializedObject);
        serializedObject.ApplyModifiedProperties();
    }

    void DrawShowOnHudField()
    {
        var ownerGroup = serializedObject.FindProperty("ownerGroup");
        var showOnHud = serializedObject.FindProperty("showOnHud");
        if (showOnHud == null)
        {
            return;
        }

        var inGroup = ownerGroup != null && ownerGroup.objectReferenceValue != null;
        using (new EditorGUI.DisabledScope(inGroup))
        {
            EditorGUILayout.PropertyField(
                showOnHud,
                new GUIContent(
                    "Show On Hud",
                    inGroup
                        ? "属于 SkillGroup 时由组的 Show On Hud 统一控制，本项无效。"
                        : "是否在 HUD 显示该 Route。"));
        }

        if (inGroup)
        {
            EditorGUILayout.HelpBox("组内 Route 的 HUD 显隐由 SkillGroupDefinition 控制。", MessageType.Info);
        }
    }
}
#endif
