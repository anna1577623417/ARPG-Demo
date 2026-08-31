#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>243.8 Editor-only shell. GraphHistory is the sole authoring mutation entry for this window.</summary>
public sealed class AnimTransitionGraphWindow : BaseGraphWindow
{
    const float ToolbarHeight = 36f;
    const float LibraryWidth = 260f;
    const float InspectorWidth = 380f;
    const float DiagnosticsHeight = 24f;

    readonly AnimTransitionGraphSelectionController selection = new AnimTransitionGraphSelectionController();
    readonly AnimTransitionGraphHistory history = new AnimTransitionGraphHistory();
    readonly AnimTransitionGraphClipboard clipboard = new AnimTransitionGraphClipboard();
    readonly AnimTransitionFocusController focusController = new AnimTransitionFocusController();
    readonly List<AnimTransitionAuthoringGraph> graphStack = new List<AnimTransitionAuthoringGraph>();
    readonly List<Button> libraryButtons = new List<Button>();
    Label statusLabel;
    Label diagnosticsLabel;
    Label inspectorLabel;
    Label breadcrumbLabel;
    Label libraryHits;
    VisualElement libraryPanel;
    VisualElement inspectorPanel;
    AnimTransitionInspectorBinder inspectorBinder;
    AnimTransitionValidationOverlay validationOverlay;
    AnimTransitionTraceOverlay traceOverlay;
    AnimTransitionWorkbench244 workbench;
    bool chromeBuilt;

    public AnimTransitionAuthoringGraph AuthoringGraph => graph as AnimTransitionAuthoringGraph;
    public AnimTransitionGraphHistory History => history;
    public AnimTransitionFocusController FocusController => focusController;
    public AnimTransitionGraphView TransitionCanvas => graphView as AnimTransitionGraphView;
    AnimTransitionGraphView TransitionView => TransitionCanvas;

    [MenuItem("Window/Animation/Transition Graph")]
    public static void OpenEmpty()
    {
        var window = GetWindow<AnimTransitionGraphWindow>();
        window.titleContent = new GUIContent("Anim Transition Graph");
        window.Show();
    }

    [MenuItem("Assets/GameMain/Open Animation Transition Graph", true)]
    static bool ValidateOpenSelected() => Selection.activeObject is AnimTransitionAuthoringGraph;

    [MenuItem("Assets/GameMain/Open Animation Transition Graph")]
    static void OpenSelected()
    {
        Open(Selection.activeObject as AnimTransitionAuthoringGraph);
    }

    public static void Open(AnimTransitionAuthoringGraph asset)
    {
        if (asset == null) return;
        var window = GetWindow<AnimTransitionGraphWindow>();
        window.titleContent = new GUIContent("Anim Graph — " + asset.name);
        window.InitializeGraph(asset);
        window.Show();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (graph != null)
        {
            graph.onGraphChanges -= OnGraphChanged;
            graph.onGraphChanges += OnGraphChanged;
            history.Bind(AuthoringGraph);
        }
    }

    protected override void OnDisable()
    {
        if (graph != null) graph.onGraphChanges -= OnGraphChanged;
        base.OnDisable();
    }

    protected override void InitializeWindow(BaseGraph loadedGraph)
    {
        EnsureChrome();
        history.Bind(loadedGraph as AnimTransitionAuthoringGraph);
        var view = new AnimTransitionGraphView(this, selection);
        view.style.position = Position.Absolute;
        view.style.left = LibraryWidth;
        view.style.right = InspectorWidth;
        view.style.top = ToolbarHeight;
        view.style.bottom = DiagnosticsHeight;
        rootView.Add(view);
        BringChromeToFront();
        RefreshPanels();
    }

    protected override void InitializeGraphView(BaseGraphView view)
    {
        if (view is AnimTransitionGraphView transitionView)
        {
            transitionView.SetReadOnly(EditorApplication.isPlaying);
            transitionView.schedule.Execute(RefreshPanels).Every(120);
        }
    }

    public void UndoAuthoring()
    {
        if (!history.Undo(AuthoringGraph)) return;
        ValidateAfterHistory("Undo");
    }

    public void RedoAuthoring()
    {
        if (!history.Redo(AuthoringGraph)) return;
        ValidateAfterHistory("Redo");
    }

    public void ApplyLayoutAsCommand()
    {
        if (AuthoringGraph == null || EditorApplication.isPlaying) return;
        history.Execute(AuthoringGraph, AnimTransitionApplyLayoutCommand.Capture(AuthoringGraph));
        AfterMutation();
    }

    public void AddRerouteAt(Vector2 position)
    {
        if (AuthoringGraph == null || EditorApplication.isPlaying) return;
        history.Execute(AuthoringGraph, AnimTransitionCreateNodeCommand.Create<AnimGraphRerouteNode>(position));
        AfterMutation();
    }

    public void DeleteSelectionAsCommand()
    {
        if (AuthoringGraph == null || EditorApplication.isPlaying) return;
        var selected = AnimTransitionSelectedAuthoring.From(AuthoringGraph, selection.Current);
        if (selected.NodeGuids.Count == 0 && selected.Edges.Count == 0) return;
        if (selected.NodeGuids.Count > 0)
        {
            history.Execute(AuthoringGraph, AnimTransitionDeleteNodesCommand.Capture(AuthoringGraph, selected.NodeGuids));
        }
        else
        {
            history.Execute(AuthoringGraph, new AnimTransitionDeleteEdgesCommand(selected.Edges));
        }

        selection.Clear();
        AfterMutation();
    }

    public void CopySelectionToClipboard()
    {
        var selected = AnimTransitionSelectedAuthoring.From(AuthoringGraph, selection.Current);
        if (selected.NodeGuids.Count == 0) return;
        clipboard.Set(
            AnimTransitionGraphMutation.CaptureNodes(AuthoringGraph, selected.NodeGuids),
            AnimTransitionGraphMutation.CaptureIncidentEdges(AuthoringGraph, selected.NodeGuids));
    }

    public void PasteClipboardAsCommand()
    {
        if (!clipboard.HasContent || AuthoringGraph == null || EditorApplication.isPlaying) return;
        history.Execute(
            AuthoringGraph,
            new AnimTransitionPasteSubgraphCommand(
                new List<AnimTransitionNodeSnapshot>(clipboard.Nodes),
                new List<AnimTransitionEdgeSnapshot>(clipboard.Edges),
                new Vector2(40f, 40f)));
        AfterMutation();
    }

    public void DuplicateSelectionAsCommand()
    {
        CopySelectionToClipboard();
        PasteClipboardAsCommand();
    }

    /// <summary>
    /// Resolves the non-destructive Escape layers. Returns false only when the canvas is already idle,
    /// allowing the input router to close this EditorWindow without creating a graph-history entry.
    /// </summary>
    public bool CancelTransientInteraction()
    {
        if (focusController.IsCanvasGestureActive)
        {
            focusController.IsCanvasGestureActive = false;
            RebuildCanvas("Connection preview cancelled.");
            return true;
        }

        if (selection.Current.Count == 0) return false;
        TransitionView?.ClearSelection();
        selection.Clear();
        RefreshPanels();
        return true;
    }

    public void RebuildCanvas(string diagnostics)
    {
        TransitionView?.RebuildProjection();
        if (!string.IsNullOrEmpty(diagnostics) && diagnosticsLabel != null)
        {
            diagnosticsLabel.text = diagnostics;
        }

        RefreshPanels();
    }

    public bool TryCommitPortConnection(PortView output, PortView input, SerializableEdge dragged)
    {
        if (AuthoringGraph == null || EditorApplication.isPlaying)
        {
            RebuildCanvas("PlayMode is read-only for authoring graphs.");
            return false;
        }

        if (output?.owner?.nodeTarget == null || input?.owner?.nodeTarget == null)
        {
            RebuildCanvas("Connection preview is incomplete.");
            return false;
        }

        var decision = AnimTransitionConnectionPolicy.Evaluate(
            AuthoringGraph,
            output.owner.nodeTarget.GUID,
            output.fieldName,
            output.portData != null ? output.portData.identifier : string.Empty,
            input.owner.nodeTarget.GUID,
            input.fieldName,
            input.portData != null ? input.portData.identifier : string.Empty,
            dragged != null ? dragged.GUID : string.Empty);
        if (!decision.Allowed)
        {
            RebuildCanvas(decision.Reason);
            return false;
        }

        var command = AnimTransitionConnectionPolicy.ToCommand(decision);
        if (command == null || !history.Execute(AuthoringGraph, command))
        {
            RebuildCanvas("Connection was not committed.");
            return false;
        }

        AfterMutation();
        if (diagnosticsLabel != null) diagnosticsLabel.text = decision.Reason;
        return true;
    }

    public void ShowQuickAdd(PortView port, Vector2 screenPosition)
    {
        if (AuthoringGraph == null || EditorApplication.isPlaying || port == null) return;
        AnimTransitionQuickAddProvider.Open(this, port, screenPosition);
    }

    public bool QuickAddFromPort(PortView port, Type nodeType, Vector2 graphPosition)
    {
        if (AuthoringGraph == null || EditorApplication.isPlaying || port?.owner?.nodeTarget == null || nodeType == null)
        {
            return false;
        }

        var wantInput = port.direction == Direction.Output;
        if (!AnimTransitionNodeCatalog.FindCompatibleField(nodeType, port.portType, wantInput, out var fieldName))
        {
            RebuildCanvas("No compatible port on " + AnimTransitionNodeCatalog.DisplayName(nodeType) + ".");
            return false;
        }

        var node = BaseNode.CreateFromType(nodeType, graphPosition);
        if (node == null) return false;
        var nodeSnapshot = AnimTransitionNodeSnapshot.Capture(node);
        AnimTransitionEdgeSnapshot edge;
        if (wantInput)
        {
            edge = new AnimTransitionEdgeSnapshot(
                Guid.NewGuid().ToString(),
                port.owner.nodeTarget.GUID,
                port.fieldName,
                port.portData != null ? port.portData.identifier : string.Empty,
                node.GUID,
                fieldName,
                string.Empty);
        }
        else
        {
            edge = new AnimTransitionEdgeSnapshot(
                Guid.NewGuid().ToString(),
                node.GUID,
                fieldName,
                string.Empty,
                port.owner.nodeTarget.GUID,
                port.fieldName,
                port.portData != null ? port.portData.identifier : string.Empty);
        }

        if (!history.Execute(AuthoringGraph, new AnimTransitionQuickAddCommand(nodeSnapshot, edge)))
        {
            return false;
        }

        AfterMutation();
        if (diagnosticsLabel != null) diagnosticsLabel.text = "Quick Add " + AnimTransitionNodeCatalog.DisplayName(nodeType);
        return true;
    }

    public void InsertRerouteOnEdge(SerializableEdge edge, Vector2 graphPosition)
    {
        if (AuthoringGraph == null || EditorApplication.isPlaying || edge == null) return;
        if (!AnimTransitionInsertRerouteCommand.TryCreate(AuthoringGraph, edge, graphPosition, out var command, out var reason))
        {
            RebuildCanvas(reason);
            return;
        }

        if (!history.Execute(AuthoringGraph, command)) return;
        AfterMutation();
        if (diagnosticsLabel != null) diagnosticsLabel.text = "Insert Reroute";
    }

    public void CommitMoveIfChanged(Dictionary<string, Rect> dragStartPositions)
    {
        if (AuthoringGraph == null || dragStartPositions == null || dragStartPositions.Count == 0) return;
        var guids = new List<string>();
        var oldPositions = new List<Rect>();
        var newPositions = new List<Rect>();
        foreach (var pair in dragStartPositions)
        {
            if (!AuthoringGraph.nodesPerGUID.TryGetValue(pair.Key, out var node)) continue;
            if (node.position == pair.Value) continue;
            guids.Add(pair.Key);
            oldPositions.Add(pair.Value);
            newPositions.Add(node.position);
        }

        if (guids.Count == 0) return;
        var command = new AnimTransitionMoveNodesCommand(guids.ToArray(), oldPositions.ToArray(), newPositions.ToArray());
        if (!command.HasChange) return;
        history.Execute(AuthoringGraph, command);
        AfterMutation(rebuild: false);
    }

    void EnsureChrome()
    {
        if (chromeBuilt) return;
        chromeBuilt = true;
        rootView.style.backgroundColor = new Color(0.067f, 0.094f, 0.118f);

        var toolbar = new Toolbar { name = "AnimTransitionToolbar" };
        toolbar.style.height = ToolbarHeight;
        toolbar.Add(new ToolbarButton(() => TransitionView?.FrameAll()) { text = "Frame" });
        toolbar.Add(new ToolbarButton(ApplyLayoutAsCommand) { text = "Auto Layout" });
        toolbar.Add(new ToolbarButton(ValidateGraph) { text = "Validate" });
        toolbar.Add(new ToolbarButton(CompileGraph) { text = "Compile" });
        var debugToggle = new ToolbarToggle { text = "UI Debug", name = "AnimTransitionUIDebugToggle" };
        debugToggle.value = AnimTransitionEditorDebug244.Enabled;
        debugToggle.RegisterValueChangedCallback(evt => AnimTransitionEditorDebug244.Enabled = evt.newValue);
        toolbar.Add(debugToggle);
        toolbar.Add(new ToolbarButton(UndoAuthoring) { text = "Undo" });
        toolbar.Add(new ToolbarButton(RedoAuthoring) { text = "Redo" });
        toolbar.Add(new ToolbarButton(() =>
        {
            TransitionView?.ClearSelection();
            selection.Clear();
            RefreshPanels();
        }) { text = "Clear Selection" });
        toolbar.Add(new ToolbarButton(GoBackSubGraph) { text = "Back" });
        toolbar.Add(new ToolbarButton(() => workbench?.Toggle()) { text = "Workbench" });
        breadcrumbLabel = new Label("Root") { name = "AnimTransitionBreadcrumb" };
        breadcrumbLabel.style.marginLeft = 8;
        toolbar.Add(breadcrumbLabel);
        statusLabel = new Label { name = "AnimTransitionCompileStatus" };
        statusLabel.style.marginLeft = 8;
        toolbar.Add(statusLabel);
        rootView.Add(toolbar);

        libraryPanel = BuildLibrary();
        inspectorPanel = BuildInspector();
        rootView.Add(libraryPanel);
        rootView.Add(inspectorPanel);
        diagnosticsLabel = new Label { name = "AnimTransitionDiagnostics" };
        diagnosticsLabel.style.position = Position.Absolute;
        diagnosticsLabel.style.left = 0;
        diagnosticsLabel.style.right = 0;
        diagnosticsLabel.style.bottom = 0;
        diagnosticsLabel.style.height = DiagnosticsHeight;
        diagnosticsLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        diagnosticsLabel.style.paddingLeft = 8;
        diagnosticsLabel.style.backgroundColor = new Color(0.094f, 0.106f, 0.118f);
        rootView.Add(diagnosticsLabel);
        validationOverlay = new AnimTransitionValidationOverlay(diagnosticsLabel);
        traceOverlay = new AnimTransitionTraceOverlay(diagnosticsLabel);
        workbench = new AnimTransitionWorkbench244(this);
        rootView.Add(workbench.Root);
    }

    VisualElement BuildLibrary()
    {
        var panel = new VisualElement { name = "AnimTransitionLibrary" };
        panel.style.position = Position.Absolute;
        panel.style.left = 0;
        panel.style.top = ToolbarHeight;
        panel.style.bottom = DiagnosticsHeight;
        panel.style.width = LibraryWidth;
        panel.style.paddingLeft = 8;
        panel.style.paddingRight = 8;
        panel.style.backgroundColor = new Color(0.094f, 0.106f, 0.118f);
        panel.pickingMode = PickingMode.Position;
        panel.Add(new Label("Node Library"));
        var search = new TextField("Search") { name = "AnimTransitionLibrarySearch" };
        search.RegisterValueChangedCallback(evt => FilterLibrary(evt.newValue));
        panel.Add(search);
        libraryHits = new Label { name = "AnimTransitionLibraryHits" };
        libraryHits.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(libraryHits);
        AddNodeButton<AnimGraphEntryNode>(panel, "Entry");
        AddNodeButton<AnimGraphDomainEntryNode244>(panel, "Domain Entry");
        AddNodeButton<AnimGraphPresentationResolveNode244>(panel, "Presentation Resolve");
        AddNodeButton<AnimGraphTransitionFamilyNode244>(panel, "Transition Family");
        AddNodeButton<AnimGraphExceptionRuleNode244>(panel, "Exception Rule");
        AddNodeButton<AnimGraphPolicyProfileNode244>(panel, "Policy Profile");
        AddNodeButton<AnimGraphDefaultFallbackNode244>(panel, "Default Fallback");
        AddNodeButton<AnimGraphPredicateNode>(panel, "Predicate");
        AddNodeButton<AnimGraphSelectorNode>(panel, "Selector");
        AddNodeButton<AnimGraphVariantNode>(panel, "Variant");
        AddNodeButton<AnimGraphTransitionPolicyNode>(panel, "Transition Policy");
        AddNodeButton<AnimGraphSpatialHandoffNode>(panel, "Spatial Handoff");
        AddNodeButton<AnimGraphLayerNode>(panel, "Layer");
        AddNodeButton<AnimGraphSyncNode>(panel, "Sync");
        AddNodeButton<AnimGraphSubGraphNode>(panel, "Sub Graph");
        AddNodeButton<AnimGraphOutputNode>(panel, "Output");
        AddNodeButton<AnimGraphRerouteNode>(panel, "Reroute");
        FilterLibrary(string.Empty);
        return panel;
    }

    void AddNodeButton<T>(VisualElement panel, string title) where T : AnimTransitionGraphNode, new()
    {
        var button = new Button(() => AddNode<T>()) { text = title, name = "Library_" + title };
        libraryButtons.Add(button);
        panel.Add(button);
    }

    void FilterLibrary(string query)
    {
        var needle = query ?? string.Empty;
        for (var i = 0; i < libraryButtons.Count; i++)
        {
            var visible = string.IsNullOrEmpty(needle)
                || libraryButtons[i].text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            libraryButtons[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (libraryHits == null) return;
        if (AuthoringGraph == null)
        {
            libraryHits.text = string.IsNullOrEmpty(needle) ? string.Empty : "No graph loaded.";
            return;
        }

        var hits = new List<string>();
        if (AuthoringGraph.nodes != null)
        {
            for (var i = 0; i < AuthoringGraph.nodes.Count; i++)
            {
                if (!(AuthoringGraph.nodes[i] is AnimTransitionGraphNode node)) continue;
                var haystack = node.Kind + " " + node.BuildDeterministicConfiguration() + " " + node.GUID;
                if (!string.IsNullOrEmpty(needle) && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(needle)) hits.Add(node.Kind + " · " + node.GUID);
            }
        }

        if (string.IsNullOrEmpty(needle))
        {
            libraryHits.text = "Search the current graph and node catalog. It does not scan the whole project.";
        }
        else if (hits.Count == 0)
        {
            libraryHits.text = "No matching node in this graph.";
        }
        else
        {
            libraryHits.text = string.Join("\n", hits);
        }
    }

    void AddNode<T>() where T : AnimTransitionGraphNode, new()
    {
        if (AuthoringGraph == null || EditorApplication.isPlaying) return;
        var view = TransitionView;
        var viewport = view != null
            ? new Rect(0f, 0f, Mathf.Max(1f, view.layout.width), Mathf.Max(1f, view.layout.height))
            : new Rect(0f, 0f, 1280f, 720f);
        var canvasCenter = viewport.center;
        var graphCenter = view != null ? AnimTransitionCanvasCoordinates244.CanvasToGraph(view, canvasCenter) : canvasCenter;
        var selected = new List<Rect>();
        for (var i = 0; i < selection.NodeGuids.Count; i++)
        {
            if (AuthoringGraph.nodesPerGUID.TryGetValue(selection.NodeGuids[i], out var node)) selected.Add(node.position);
        }

        var anchor = default(Rect);
        for (var i = 0; i < selected.Count; i++) anchor = i == 0 ? selected[i] : Union(anchor, selected[i]);
        var intent = new AnimTransitionNodeCreateIntent244(typeof(T), AnimTransitionNodeCreateSource244.Library,
            anchor, new Rect(graphCenter - viewport.size * 0.5f, viewport.size), AnimTransitionNodePlacement244.EstimateNodeSize(typeof(T)));
        var placement = AnimTransitionNodePlacement244.TryPlace(intent, selected);
        if (!placement.Succeeded)
        {
            diagnosticsLabel.text = placement.Reason;
            return;
        }

        var position = placement.Position;
        history.Execute(AuthoringGraph, AnimTransitionCreateNodeCommand.Create<T>(position));
        AfterMutation();
    }

    static Rect Union(Rect first, Rect second)
    {
        return Rect.MinMaxRect(Mathf.Min(first.xMin, second.xMin), Mathf.Min(first.yMin, second.yMin),
            Mathf.Max(first.xMax, second.xMax), Mathf.Max(first.yMax, second.yMax));
    }

    VisualElement BuildInspector()
    {
        var panel = new VisualElement { name = "AnimTransitionInspector" };
        panel.style.position = Position.Absolute;
        panel.style.right = 0;
        panel.style.top = ToolbarHeight;
        panel.style.bottom = DiagnosticsHeight;
        panel.style.width = InspectorWidth;
        panel.style.paddingLeft = 10;
        panel.style.paddingRight = 10;
        panel.style.backgroundColor = new Color(0.094f, 0.106f, 0.118f);
        panel.pickingMode = PickingMode.Position;
        panel.RegisterCallback<PointerDownEvent>(evt =>
        {
            var focused = panel.focusController != null ? panel.focusController.focusedElement : null;
            focusController.Refresh(focused);
            evt.StopPropagation();
        });
        panel.Add(new Label("Inspector"));
        inspectorLabel = new Label { name = "AnimTransitionInspectorSelection" };
        inspectorLabel.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(inspectorLabel);
        inspectorBinder = new AnimTransitionInspectorBinder(panel, this);
        return panel;
    }

    void OnGraphChanged(GraphChanges _)
    {
        if (history.IsApplying) return;
        AuthoringGraph?.MarkCompileRequired();
        RefreshPanels();
    }

    void ValidateGraph()
    {
        var report = AnimTransitionGraphValidator.Validate(AuthoringGraph);
        var crossings = AnimTransitionLayoutService.EstimateCrossings(AuthoringGraph);
        validationOverlay?.Show(report, crossings);
        if (diagnosticsLabel != null && report != null && report.Issues.Count > 0)
        {
            diagnosticsLabel.tooltip = AnimTransitionValidationPresenter244.BuildReport(report);
        }
        RefreshPanels();
    }

    void CompileGraph()
    {
        if (EditorApplication.isPlaying)
        {
            diagnosticsLabel.text = "PlayMode is read-only for authoring graphs.";
            return;
        }

        if (AnimTransitionGraphAssetUtility.TryCompileAndPersist(AuthoringGraph, out var report))
        {
            diagnosticsLabel.text = "Compiled: " + report.Summary;
        }
        else
        {
            diagnosticsLabel.text = "Compile blocked: " + report.Summary;
        }

        RefreshPanels();
    }

    void ValidateAfterHistory(string operation)
    {
        AfterMutation();
        var report = AnimTransitionGraphValidator.Validate(AuthoringGraph);
        if (diagnosticsLabel != null)
        {
            diagnosticsLabel.text = operation + " · " + report.Summary;
        }
    }

    void AfterMutation(bool rebuild = true)
    {
        if (rebuild) TransitionView?.RebuildProjection();
        RefreshPanels();
    }

    void RefreshPanels()
    {
        var graphAsset = AuthoringGraph;
        if (statusLabel == null || diagnosticsLabel == null || inspectorLabel == null) return;
        RefreshBreadcrumb();
        inspectorBinder?.Refresh(graphAsset, selection.Primary);
        if (graphAsset == null)
        {
            statusLabel.text = "No graph selected";
            return;
        }

        statusLabel.text = EditorApplication.isPlaying
            ? "PlayMode Read-Only"
            : (graphAsset.MigrationRequired
                ? "Migration Required (schema " + graphAsset.SchemaVersion + " → " + AnimTransitionAuthoringGraph.CurrentSchemaVersion + ")"
                : (graphAsset.CompileRequired ? "Compile Required" : "Compiled"));
        if (EditorApplication.isPlaying)
        {
            traceOverlay?.ShowReadOnlyState(graphAsset.CompiledGraph);
        }
        workbench?.Refresh();
    }

    public void NotifyInspectorCommitted()
    {
        AfterMutation(rebuild: false);
    }

    public void EnterSubGraph(AnimTransitionAuthoringGraph subGraph)
    {
        if (subGraph == null)
        {
            RebuildCanvas("SubGraph node has no referenced graph.");
            return;
        }

        if (subGraph == AuthoringGraph)
        {
            RebuildCanvas("SubGraph cannot enter its owner graph.");
            return;
        }

        if (AuthoringGraph != null) graphStack.Add(AuthoringGraph);
        InitializeGraph(subGraph);
        RefreshPanels();
    }

    void GoBackSubGraph()
    {
        if (graphStack.Count == 0) return;
        var parent = graphStack[graphStack.Count - 1];
        graphStack.RemoveAt(graphStack.Count - 1);
        if (parent != null) InitializeGraph(parent);
        RefreshPanels();
    }

    void RefreshBreadcrumb()
    {
        if (breadcrumbLabel == null) return;
        if (graphStack.Count == 0)
        {
            breadcrumbLabel.text = AuthoringGraph != null ? AuthoringGraph.name : "Root";
            return;
        }

        breadcrumbLabel.text = graphStack[graphStack.Count - 1].name + " / " + (AuthoringGraph != null ? AuthoringGraph.name : "SubGraph");
    }

    void BringChromeToFront()
    {
        libraryPanel?.BringToFront();
        inspectorPanel?.BringToFront();
        statusLabel?.parent?.BringToFront();
        diagnosticsLabel?.BringToFront();
        workbench?.Root?.BringToFront();
    }
}
#endif
