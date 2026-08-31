using System.Collections.Generic;
using GraphProcessor;
using UnityEditor.Experimental.GraphView;

/// <summary>243.8 single selection fact shared by canvas, panels and diagnostics. It is never an Undo item.</summary>
public sealed class AnimTransitionGraphSelectionController
{
    readonly List<GraphElement> selection = new List<GraphElement>();
    readonly List<string> nodeGuids = new List<string>();

    public IReadOnlyList<GraphElement> Current => selection;
    public IReadOnlyList<string> NodeGuids => nodeGuids;
    public GraphElement Primary => selection.Count > 0 ? selection[0] : null;

    public void Set(IEnumerable<GraphElement> elements)
    {
        selection.Clear();
        nodeGuids.Clear();
        if (elements == null) return;
        var seen = new HashSet<string>();
        foreach (var element in elements)
        {
            Add(element, seen);
        }
    }

    public void SetSelectables(IEnumerable<ISelectable> elements)
    {
        selection.Clear();
        nodeGuids.Clear();
        if (elements == null) return;
        var seen = new HashSet<string>();
        foreach (var selectable in elements)
        {
            if (selectable is GraphElement element) Add(element, seen);
        }
    }

    void Add(GraphElement element, HashSet<string> seen)
    {
        if (element == null) return;
        selection.Add(element);
        if (element is BaseNodeView nodeView && nodeView.nodeTarget != null && seen.Add(nodeView.nodeTarget.GUID))
        {
            nodeGuids.Add(nodeView.nodeTarget.GUID);
        }
    }

    public void Clear()
    {
        selection.Clear();
        nodeGuids.Clear();
    }
}
