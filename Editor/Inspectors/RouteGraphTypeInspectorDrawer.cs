#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>153.2 — GraphType / 入口 Action 只读预览（Normal / MultiStage / Derivative）。</summary>
public static class RouteGraphTypeInspectorDrawer
{
    public static void Draw(SkillRouteDefinition route)
    {
        if (route == null || route.GraphType == RouteGraphType.Unsupported)
        {
            return;
        }

        EditorGUILayout.HelpBox($"Combat Graph · GraphType: {route.GraphType}", MessageType.None);

        if (route.TryResolveGraphEntryAction(out var action, out _))
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Graph Entry Action", action, typeof(ActionDataSO), false);
            }
        }
        else
        {
            EditorGUILayout.HelpBox($"{route.name} 无有效 Stage[0].Action", MessageType.Warning);
        }

        if (route is NormalRouteDefinition normal && !normal.IsSingleStageForGraph)
        {
            EditorGUILayout.HelpBox(
                "NormalRoute 含多 Stage 且未开启 Single Action Mode；被 Flow 边引用时 Validate 将报错。",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4);
    }
}
#endif
