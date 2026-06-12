#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor;
using UnityEngine;

/// <summary>非 Flow 边 / Relay 等不可编辑选中项的右侧说明。</summary>
public static class CombatFlowGraphInspectorFeedback
{
    public static void DrawUtilityEdge(SerializableEdge edge)
    {
        EditorGUILayout.LabelField("Graph 工具边", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            CombatFlowGraphSelectionClassifier.FormatEdgeConnection(edge),
            EditorStyles.miniLabel);

        var involvesRelay = CombatFlowGraphSelectionClassifier.IsRelayNode(edge?.outputNode)
            || CombatFlowGraphSelectionClassifier.IsRelayNode(edge?.inputNode);

        var body = involvesRelay
            ? "此连线经过 Relay，不参与 Combat Flow 编译（Sync && Compile 会跳过）。\n\n" +
              "Late Window、Transition、Target Route 等请在 Start、Flow Action、Route Switch、End 之间的连线上编辑。\n\n" +
              "建议：删除 Relay，改用 Flow 节点直连；或保留 Relay 仅作画布整理，不指望其进入运行时图。"
            : "此连线至少一端不是 Combat Flow 节点，不会写入 flowEdges。\n\n" +
              "请只在 Start / Flow Action / Route Switch / End 之间连边并编辑属性。";

        EditorGUILayout.HelpBox(body, MessageType.Info);
    }

    public static void DrawRelayNode(RelayNode relay)
    {
        EditorGUILayout.LabelField("Relay 节点", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Relay 是 GraphProcessor 的画布布线工具，不是 Combat Flow 节点。\n\n" +
            "此处不能配置 Transition、Late Window 或 Target Route。\n" +
            "请选中 Start↔Action 等 Flow 节点之间的白色/蓝色连线，在右侧编辑 Flow Edge。\n\n" +
            "不需要 Relay 时：选中本节点 → Delete 删除，再把 Action 的 Out 直接连到下一 Flow 节点。",
            MessageType.Info);

        if (relay == null)
        {
            return;
        }

        EditorGUILayout.LabelField("工具选项（GraphProcessor）", EditorStyles.miniBoldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.Toggle("Unpack Output", relay.unpackOutput);
        EditorGUILayout.Toggle("Pack Input", relay.packInput);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.LabelField(
            "双击画布空白处可插入 Relay；Combat Flow 运行时不会读取 Relay。",
            EditorStyles.wordWrappedMiniLabel);
    }

    public static void DrawUtilityNode(BaseNode node)
    {
        EditorGUILayout.LabelField("Graph 工具节点", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"节点类型「{node?.GetType().Name ?? "?"}」不属于 Combat Flow。\n\n" +
            "请使用右键菜单 Combat Flow / Start、Flow Action、Route Switch、End 搭建流转图。",
            MessageType.Warning);
    }
}
#endif
