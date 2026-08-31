#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using GraphProcessor;

public enum AnimTransitionGestureKind244
{
    Idle = 0,
    NodeDrag = 1,
    Connect = 2,
    Reconnect = 3,
    BoxSelect = 4,
    Pan = 5,
}

/// <summary>Editor-session gesture truth. It is never serialized into the graph.</summary>
public sealed class AnimTransitionGestureState244
{
    public event Action<AnimTransitionGestureKind244, AnimTransitionGestureKind244, string> Changed;

    public AnimTransitionGestureKind244 Current { get; private set; } = AnimTransitionGestureKind244.Idle;
    public int Version { get; private set; }
    public bool IsActive => Current != AnimTransitionGestureKind244.Idle;
    public string ActiveEdgeGuid { get; private set; } = string.Empty;
    public IReadOnlyCollection<string> ActiveNodeGuids => affectedNodeGuids;

    readonly HashSet<string> affectedNodeGuids = new HashSet<string>(StringComparer.Ordinal);

    public bool Begin(AnimTransitionGestureKind244 next, string reason)
    {
        return Begin(next, reason, (IEnumerable<string>)null, string.Empty);
    }

    public bool Begin(AnimTransitionGestureKind244 next, string reason, string nodeGuid, string edgeGuid = "")
    {
        var nodes = string.IsNullOrEmpty(nodeGuid) ? null : new[] { nodeGuid };
        return Begin(next, reason, nodes, edgeGuid);
    }

    public bool Begin(AnimTransitionGestureKind244 next, string reason, IEnumerable<string> nodeGuids, string edgeGuid = "")
    {
        if (next == AnimTransitionGestureKind244.Idle) return End(reason);
        affectedNodeGuids.Clear();
        if (nodeGuids != null)
        {
            foreach (var guid in nodeGuids)
            {
                if (!string.IsNullOrEmpty(guid)) affectedNodeGuids.Add(guid);
            }
        }

        ActiveEdgeGuid = edgeGuid ?? string.Empty;
        return Set(next, reason);
    }

    public bool End(string reason)
    {
        var changed = Set(AnimTransitionGestureKind244.Idle, reason);
        affectedNodeGuids.Clear();
        ActiveEdgeGuid = string.Empty;
        return changed;
    }

    public bool Affects(SerializableEdge edge, bool transient)
    {
        if (Current == AnimTransitionGestureKind244.NodeDrag)
        {
            if (edge == null) return false;
            edge.OnBeforeSerialize();
            return affectedNodeGuids.Contains(edge.outputNode != null ? edge.outputNode.GUID : string.Empty)
                || affectedNodeGuids.Contains(edge.inputNode != null ? edge.inputNode.GUID : string.Empty);
        }

        if (Current == AnimTransitionGestureKind244.Connect) return transient;
        if (Current == AnimTransitionGestureKind244.Reconnect)
        {
            if (transient) return true;
            if (edge != null && string.Equals(edge.GUID, ActiveEdgeGuid, StringComparison.Ordinal)) return true;
            if (edge == null) return false;
            edge.OnBeforeSerialize();
            return affectedNodeGuids.Contains(edge.outputNode != null ? edge.outputNode.GUID : string.Empty)
                || affectedNodeGuids.Contains(edge.inputNode != null ? edge.inputNode.GUID : string.Empty);
        }

        return false;
    }

    bool Set(AnimTransitionGestureKind244 next, string reason)
    {
        if (Current == next) return false;
        var previous = Current;
        Current = next;
        Version++;
        Changed?.Invoke(previous, next, reason ?? string.Empty);
        return true;
    }
}
#endif
