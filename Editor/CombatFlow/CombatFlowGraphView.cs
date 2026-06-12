#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>150.3 / 152.1 — GraphView 选中同步 + 边/节点点击即时提交 Inspector。</summary>
public sealed class CombatFlowGraphView : BaseGraphView
{
    const int SelectionSyncDelayMs = 20;
    const int SelectionSyncFallbackMs = 80;
    const int RebindSelectionHooksDelayMs = 0;
    const int RebindSelectionHooksFallbackMs = 50;

    readonly CombatFlowGraphWindow _host;
    string _searchFilter = string.Empty;

    public CombatFlowGraphView(CombatFlowGraphWindow host)
        : base(host)
    {
        _host = host;
        focusable = true;

        RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
        RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
    }

    void OnPointerDown(PointerDownEvent evt)
    {
        CombatFlowGraphInputDebug.Log(
            $"GraphView PointerDown target={CombatFlowGraphInputDebug.Describe(evt.target as VisualElement)} " +
            $"pos={evt.position}");
    }

    public void BindEdgeSelectionHooks()
    {
        RebindAllSelectionHooks();

        if (graph != null)
        {
            graph.onGraphChanges -= OnGraphEdgesChanged;
            graph.onGraphChanges += OnGraphEdgesChanged;
        }
    }

    void OnGraphEdgesChanged(GraphChanges changes)
    {
        if (changes.addedEdge != null)
        {
            if (graph is CombatFlowProcessorGraph processorGraph
                && !string.IsNullOrEmpty(changes.addedEdge.GUID))
            {
                processorGraph.GetOrCreateEdgeMeta(changes.addedEdge.GUID);
                UnityEditor.EditorUtility.SetDirty(processorGraph);
            }

            ScheduleRebindSelectionHooks();
        }

        if (changes.removedEdge != null
            && graph is CombatFlowProcessorGraph removedGraph
            && !string.IsNullOrEmpty(changes.removedEdge.GUID))
        {
            removedGraph.RemoveEdgeMeta(changes.removedEdge.GUID);
            UnityEditor.EditorUtility.SetDirty(removedGraph);
        }

        if (changes.addedNode != null)
        {
            ScheduleRebindSelectionHooks();
        }
    }

    void ScheduleRebindSelectionHooks()
    {
        schedule.Execute(RebindAllSelectionHooks).ExecuteLater(RebindSelectionHooksDelayMs);
        schedule.Execute(RebindAllSelectionHooks).ExecuteLater(RebindSelectionHooksFallbackMs);
    }

    void RebindAllSelectionHooks()
    {
        for (var i = 0; i < edgeViews.Count; i++)
        {
            RegisterEdgeSelectionCallback(edgeViews[i]);
        }

        for (var i = 0; i < nodeViews.Count; i++)
        {
            RegisterNodeSelectionCallback(nodeViews[i]);
        }
    }

    void RegisterEdgeSelectionCallback(EdgeView edgeView)
    {
        if (edgeView == null)
        {
            return;
        }

        edgeView.UnregisterCallback<MouseDownEvent>(OnEdgeMouseDown);
        edgeView.UnregisterCallback<PointerDownEvent>(OnEdgePointerDown);
        edgeView.RegisterCallback<MouseDownEvent>(OnEdgeMouseDown);
        edgeView.RegisterCallback<PointerDownEvent>(OnEdgePointerDown);
    }

    void RegisterNodeSelectionCallback(BaseNodeView nodeView)
    {
        if (nodeView == null)
        {
            return;
        }

        nodeView.UnregisterCallback<MouseDownEvent>(OnNodeMouseDown);
        nodeView.RegisterCallback<MouseDownEvent>(OnNodeMouseDown);
    }

    void OnNodeMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        if (evt.currentTarget is BaseNodeView nodeView && nodeView.nodeTarget != null)
        {
            _host.CommitInspectorNode(nodeView.nodeTarget);
        }

        ScheduleSelectionSync();
    }

    void OnEdgeMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        TryCommitEdgeFromEvent(evt.currentTarget as EdgeView);
        ScheduleSelectionSync();
    }

    void OnEdgePointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        var edgeView = evt.currentTarget as EdgeView
            ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<EdgeView>();
        TryCommitEdgeFromEvent(edgeView);
    }

    void TryCommitEdgeFromEvent(EdgeView edgeView)
    {
        if (edgeView == null)
        {
            return;
        }

        if (CombatFlowGraphEdgeSelectionUtility.TryGetSerializableEdge(edgeView, out var serialEdge))
        {
            _host.CommitInspectorEdge(serialEdge);
        }
    }

    public void SetSearchFilter(string filter)
    {
        _searchFilter = filter ?? string.Empty;
        CombatFlowGraphNodeSearch.ApplyToGraphView(this, _searchFilter);
    }

    void OnMouseUp(MouseUpEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        ScheduleSelectionSync();
    }

    void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Escape)
        {
            ScheduleSelectionSync(clearIfEmpty: true);
        }
    }

    void ScheduleSelectionSync(bool clearIfEmpty = true)
    {
        schedule.Execute(() => _host.NotifyGraphSelectionChanged(clearIfEmpty))
            .ExecuteLater(SelectionSyncDelayMs);
        schedule.Execute(() => _host.NotifyGraphSelectionChanged(clearIfEmpty))
            .ExecuteLater(SelectionSyncFallbackMs);
    }
}
#endif
