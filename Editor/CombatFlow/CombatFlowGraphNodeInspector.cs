#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor;
using UnityEngine;

/// <summary>150.3 — 单选 Flow 节点时的右侧 Inspector。</summary>
public static class CombatFlowGraphNodeInspector
{
    public static void Draw(BaseNode node)
    {
        if (node == null)
        {
            return;
        }

        var prevLabel = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = CombatFlowGraphInspectorLayout.LabelWidth;

        EditorGUILayout.LabelField(node.name, EditorStyles.boldLabel);

        switch (node)
        {
            case CombatFlowStartNode start:
                start.nodeId = EditorGUILayout.TextField("Node Id", start.nodeId);
                break;
            case CombatFlowActionNode action:
                action.nodeId = EditorGUILayout.TextField("Node Id", action.nodeId);
                action.action = (ActionDataSO)EditorGUILayout.ObjectField(
                    "Action",
                    action.action,
                    typeof(ActionDataSO),
                    false);
                DrawActionGraphParticipationHints(action.action);
                break;
            case CombatFlowRouteSwitchNode routeSwitch:
                routeSwitch.nodeId = EditorGUILayout.TextField("Node Id", routeSwitch.nodeId);
                routeSwitch.route = (SkillRouteDefinition)EditorGUILayout.ObjectField(
                    "Route",
                    routeSwitch.route,
                    typeof(SkillRouteDefinition),
                    false);
                EditorGUILayout.HelpBox(
                    "【153.2】RouteSwitch 为旧式「Route 节点」；连招请改用 Flow 边（Action→Action + TargetRoute）。\n" +
                    "MultiStage / 打断请改用 Interrupt 边（Action→End + TargetRoute）。",
                    MessageType.Info);
                break;
            case CombatFlowEndNode end:
                end.nodeId = EditorGUILayout.TextField("Node Id", end.nodeId);
                EditorGUILayout.HelpBox("End 节点 Node Id 编译为 IdleNodeId（段末/无匹配时游标回落点）。", MessageType.Info);
                break;
            default:
                EditorGUILayout.HelpBox($"未识别的节点类型：{node.GetType().Name}", MessageType.Warning);
                break;
        }

        EditorGUIUtility.labelWidth = prevLabel;
    }

    static void DrawActionGraphParticipationHints(ActionDataSO actionData)
    {
        if (actionData == null)
        {
            return;
        }

        var resolved = ActionIntentRouting.ResolveGraphParticipation(actionData);
        var autoNote = actionData.GraphParticipation == GraphParticipation.Auto
            ? $" (Auto→{resolved})"
            : string.Empty;
        EditorGUILayout.LabelField("Intent Lane (A)", actionData.IntentCategory.ToString());
        EditorGUILayout.LabelField("Graph Part. (C)", $"{actionData.GraphParticipation}{autoNote}");

        if (resolved == GraphParticipation.SourceOnly)
        {
            EditorGUILayout.HelpBox(
                "SourceOnly：可作 Graph 派生来源；Resolve 目标免 Gate/CanCast；Consume 免 Graph 双闸门。",
                MessageType.Info);
        }
        else if (resolved == GraphParticipation.Full)
        {
            EditorGUILayout.HelpBox(
                "Full：Combat 双闸门 + Resolve Gate/CanCast 均生效。",
                MessageType.None);
        }
    }
}
#endif
