#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatGraphAsset))]
public sealed class CombatGraphAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var graph = (CombatGraphAsset)target;
        if (graph == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("136.1 Edge Preview", EditorStyles.boldLabel);

        var edges = graph.Edges;
        if (edges == null || edges.Length == 0)
        {
            EditorGUILayout.HelpBox("无边配置。", MessageType.Info);
            return;
        }

        var idle = graph.IdleNodeId;
        var fromIdle = 0;
        for (var i = 0; i < edges.Length; i++)
        {
            if (edges[i].FromNodeId == idle)
            {
                fromIdle++;
            }
        }

        EditorGUILayout.HelpBox(
            $"Idle 节点「{idle}」出边 {fromIdle} 条；运行时按 priority 升序求值，CanCast=false 时尝试下一条。",
            MessageType.None);

        for (var i = 0; i < edges.Length; i++)
        {
            DrawEdgeSummary(graph, edges[i], i);
        }
    }

    static void DrawEdgeSummary(CombatGraphAsset graph, CombatGraphEdge edge, int index)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                $"#{index} pri={edge.Priority} {edge.FromNodeId}→{edge.ToNodeId}",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"Trigger: {edge.TriggerSlot} + {edge.TriggerSemantic}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Route: {(edge.TargetRoute != null ? edge.TargetRoute.name : "(null)")}",
                EditorStyles.miniLabel);

            var condText = SummarizeConditions(edge.Conditions);
            EditorGUILayout.LabelField($"Conditions: {condText}", EditorStyles.miniLabel);

            if (edge.TriggerSemantic == InputSemanticType.Directional
                && HasAlwaysCondition(edge.Conditions))
            {
                EditorGUILayout.HelpBox(
                    "警告：Directional 边含 Always 条件，可能抢占其它方向边；四向主链应走 ContextGroup→Group。",
                    MessageType.Warning);
            }

            if (edge.TargetRoute != null && !graph.ContainsRoute(edge.TargetRoute))
            {
                EditorGUILayout.HelpBox("目标 Route 不在 registeredRoutes 池内。", MessageType.Error);
            }
        }
    }

    static string SummarizeConditions(SkillTransitionCondition[] conditions)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return "(none)";
        }

        var sb = new StringBuilder();
        for (var i = 0; i < conditions.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(" AND ");
            }

            var c = conditions[i];
            switch (c.Kind)
            {
                case SkillTransitionConditionKind.Always:
                    sb.Append("Always");
                    break;
                case SkillTransitionConditionKind.OnGrounded:
                    sb.Append("Grounded");
                    break;
                case SkillTransitionConditionKind.OnAirborne:
                    sb.Append("Airborne");
                    break;
                case SkillTransitionConditionKind.WithMoveDirection:
                    sb.Append($"MoveDir={c.RequiredMoveDirection}");
                    break;
                default:
                    sb.Append(c.Kind);
                    break;
            }
        }

        return sb.ToString();
    }

    static bool HasAlwaysCondition(SkillTransitionCondition[] conditions)
    {
        if (conditions == null)
        {
            return false;
        }

        for (var i = 0; i < conditions.Length; i++)
        {
            if (conditions[i].Kind == SkillTransitionConditionKind.Always)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
