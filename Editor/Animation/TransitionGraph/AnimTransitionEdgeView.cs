#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Endpoint-only edge projection. No condition, priority, or duration fields.</summary>
public sealed class AnimTransitionEdgeView : EdgeView
{
    readonly AnimTransitionOrthogonalEdgeControl orthogonal = new AnimTransitionOrthogonalEdgeControl();
    readonly AnimTransitionLiveEdgePreview244 live = new AnimTransitionLiveEdgePreview244();
    readonly AnimTransitionGestureState244 gestures;
    IVisualElementScheduledItem liveRefresh;
    bool subscribed;
    bool hovered;

    public AnimTransitionEdgeView(AnimTransitionGestureState244 gestureState)
    {
        gestures = gestureState;
        AddToClassList("anim-transition-edge");
        tooltip = "Connection (endpoints only)";
        Add(orthogonal);
        Add(live);
        RegisterCallback<MouseEnterEvent>(_ => SetHovered(true));
        RegisterCallback<MouseLeaveEvent>(_ => SetHovered(false));
        RegisterCallback<MouseDownEvent>(OnEdgeMouseDown, TrickleDown.TrickleDown);
        RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
        RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
        liveRefresh = schedule.Execute(RefreshProjection).Every(16);
        liveRefresh.Pause();
    }

    public string EdgeGuid => serializedEdge != null ? serializedEdge.GUID : string.Empty;

    public override void OnPortChanged(bool isInput)
    {
        base.OnPortChanged(isInput);
        if (edgeControl != null)
        {
            edgeControl.style.opacity = 0f;
        }

        RefreshProjection();
    }

    void SetHovered(bool value)
    {
        hovered = value;
        EnableInClassList("anim-transition-edge--hover", value);
        RefreshProjection();
    }

    void Subscribe()
    {
        if (subscribed || gestures == null) return;
        gestures.Changed += OnGestureChanged;
        subscribed = true;
        RefreshProjection();
        UpdateLiveRefreshSchedule();
    }

    void Unsubscribe()
    {
        if (!subscribed || gestures == null) return;
        gestures.Changed -= OnGestureChanged;
        subscribed = false;
        liveRefresh?.Pause();
    }

    void OnGestureChanged(
        AnimTransitionGestureKind244 previous,
        AnimTransitionGestureKind244 current,
        string reason)
    {
        RefreshProjection();
        UpdateLiveRefreshSchedule();
    }

    void UpdateLiveRefreshSchedule()
    {
        var current = gestures != null ? gestures.Current : AnimTransitionGestureKind244.Idle;
        if (AnimTransitionLiveEdgePreview244.ResolveProjection(current, IsTransientEdge(), IsAffected())
            == AnimTransitionEdgeProjection244.LiveStraight)
        {
            liveRefresh?.Resume();
        }
        else
        {
            liveRefresh?.Pause();
            schedule.Execute(RefreshProjection).ExecuteLater(0);
        }
    }

    void RefreshProjection()
    {
        if (edgeControl == null) return;
        edgeControl.style.opacity = 0f;
        var transient = IsTransientEdge();
        var projection = AnimTransitionLiveEdgePreview244.ResolveProjection(
            gestures != null ? gestures.Current : AnimTransitionGestureKind244.Idle,
            transient,
            IsAffected());
        orthogonal.SetEndpoints(edgeControl.from, edgeControl.to, hovered || selected);
        orthogonal.SetVisible(projection == AnimTransitionEdgeProjection244.Orthogonal);
        live.SetEndpoints(edgeControl.from, edgeControl.to, hovered || selected);
        live.SetVisible(projection == AnimTransitionEdgeProjection244.LiveStraight);
    }

    bool IsTransientEdge()
    {
        return isGhostEdge || !isConnected || serializedEdge == null;
    }

    bool IsAffected()
    {
        return gestures != null && gestures.Affects(serializedEdge, IsTransientEdge());
    }

    void OnEdgeMouseDown(MouseDownEvent evt)
    {
        if (evt.clickCount != 2 || serializedEdge == null) return;
        var view = ((input ?? output) as PortView)?.owner?.owner as AnimTransitionGraphView;
        if (view == null) return;
        var canvasPos = AnimTransitionCanvasCoordinates244.PanelToCanvas(view, evt.mousePosition);
        var graphPos = AnimTransitionCanvasCoordinates244.CanvasToGraph(view, canvasPos);
        view.Window.InsertRerouteOnEdge(serializedEdge, graphPos);
        evt.StopImmediatePropagation();
    }
}
#endif
