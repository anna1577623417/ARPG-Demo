#if UNITY_EDITOR
using UnityEditor;

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
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (target is SkillRouteDefinition route)
        {
            RouteGraphTypeInspectorDrawer.Draw(route);
        }

        DrawPropertiesExcluding(serializedObject, s_excludeGroupFields);
        SkillRouteGroupMembershipDrawer.Draw(serializedObject);
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
