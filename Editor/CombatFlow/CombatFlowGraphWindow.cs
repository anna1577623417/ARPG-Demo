#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 147.1 Combat Flow GraphProcessor 画布 — 152.1 布局/滚动/GraphDirty/刷新。
/// GraphProcessor 要求 <see cref="BaseGraphView"/> 为 <c>rootView</c> 的直接子元素，否则 Initialize 不会执行。
/// </summary>
public sealed class CombatFlowGraphWindow : BaseGraphWindow
{
    const string InspectorWidthPrefsKey = "CombatFlowGraph.InspectorWidth";
    const string SearchPrefsKey = "CombatFlowGraph.NodeSearch";
    const float ToolbarHeight = 30f;
    const float OuterGutter = 10f;
    const float SplitterHitWidth = 8f;
    const float InspectorWidthDefault = 320f;
    const float InspectorWidthMin = 260f;
    const float InspectorWidthMax = 700f;

    static readonly Color SplitterNormalColor = new(0.2f, 0.2f, 0.2f, 1f);
    static readonly Color SplitterHoverColor = new(0.4f, 0.4f, 0.4f, 1f);

    CombatGraphAsset _ownerAsset;
    VisualElement _toolbar;
    IMGUIContainer _toolbarImgui;
    VisualElement _splitterHandle;
    IMGUIContainer _splitterCursor;
    IMGUIContainer _inspectorPanel;
    string _searchText = string.Empty;
    bool _chromeBuilt;
    bool _graphDirty;
    bool _inputDebugRegistered;
    readonly CombatFlowGraphSelectionController _selection = new();

    float _inspectorWidth;
    bool _splitterDragging;
    Vector2 _inspectorScroll;

    CombatFlowGraphView CombatView => graphView as CombatFlowGraphView;

    [MenuItem("Window/Combat Flow Graph")]
    public static void OpenEmpty()
    {
        var win = GetWindow<CombatFlowGraphWindow>();
        win.titleContent = new GUIContent("Combat Flow Graph");
        win.Show();
    }

    public static void Open(CombatGraphAsset asset)
    {
        if (asset == null)
        {
            return;
        }

        if (!CombatFlowGraphSync.TryEnsureProcessorView(asset, out var error))
        {
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Combat Flow Graph", error, "OK");
            }

            return;
        }

        var win = GetWindow<CombatFlowGraphWindow>();
        win.titleContent = new GUIContent($"Combat Flow — {asset.name}");
        win._ownerAsset = asset;
        win._graphDirty = false;
        win.graph = asset.ProcessorView;
        if (win.graph != null)
        {
            win.InitializeGraph(win.graph);
        }

        win.Show();
    }

    public void ReloadGraph(BaseGraph loadedGraph)
    {
        InitializeGraph(loadedGraph);
        if (graph != null)
        {
            graph.onGraphChanges -= OnGraphChanged;
            graph.onGraphChanges += OnGraphChanged;
        }
    }

    protected override void OnEnable()
    {
        _inspectorWidth = EditorPrefs.GetFloat(InspectorWidthPrefsKey, InspectorWidthDefault);
        base.OnEnable();
        if (graph != null)
        {
            graph.onGraphChanges -= OnGraphChanged;
            graph.onGraphChanges += OnGraphChanged;
        }
    }

    protected override void OnDisable()
    {
        if (graph != null)
        {
            graph.onGraphChanges -= OnGraphChanged;
        }

        EditorPrefs.SetFloat(InspectorWidthPrefsKey, _inspectorWidth);
        if (_graphDirty)
        {
            SyncAndCompile(silent: true);
        }

        base.OnDisable();
    }

    void OnGraphChanged(GraphChanges changes)
    {
        if (graph is CombatFlowProcessorGraph pg)
        {
            if (changes.addedEdge != null)
            {
                pg.GetOrCreateEdgeMeta(changes.addedEdge.GUID);
            }

            if (changes.removedEdge != null)
            {
                pg.RemoveEdgeMeta(changes.removedEdge.GUID);
                if (_selection.Committed.Edge != null
                    && _selection.Committed.Edge.GUID == changes.removedEdge.GUID)
                {
                    _selection.Clear();
                    _inspectorPanel?.MarkDirtyRepaint();
                }
            }
        }

        MarkGraphDirty();
        CombatView?.SetSearchFilter(EditorPrefs.GetString(SearchPrefsKey, string.Empty));
        EnsureChromeOnTop();
    }

    void MarkGraphDirty()
    {
        _graphDirty = true;
        _inspectorPanel?.MarkDirtyRepaint();
    }

    void MarkGraphClean()
    {
        _graphDirty = false;
    }

    protected override void InitializeWindow(BaseGraph loadedGraph)
    {
        EnsureChromeBuilt();

        var view = new CombatFlowGraphView(this);
        view.style.flexGrow = 1f;
        view.style.flexShrink = 1f;
        view.style.marginTop = ToolbarHeight;
        ApplyGraphViewMargins(view);
        rootView.Add(view);

        EnsureChromeOnTop();
        RegisterInputDebugHooks();
    }

    void EnsureChromeBuilt()
    {
        if (_chromeBuilt)
        {
            return;
        }

        _chromeBuilt = true;
        rootView.style.flexDirection = FlexDirection.Column;
        _searchText = EditorPrefs.GetString(SearchPrefsKey, string.Empty);

        _toolbar = new VisualElement
        {
            name = "CombatFlowToolbar",
            pickingMode = PickingMode.Position,
            style =
            {
                height = ToolbarHeight,
                flexShrink = 0,
                flexGrow = 0,
            },
        };

        _toolbarImgui = new IMGUIContainer(DrawToolbarImgui)
        {
            name = "ToolbarImgui",
            style = { flexGrow = 1f },
        };
        _toolbar.Add(_toolbarImgui);

        _splitterHandle = CreateSplitterHandle();
        _splitterCursor = new IMGUIContainer(DrawSplitterCursorOnly)
        {
            name = "SplitterCursor",
            pickingMode = PickingMode.Ignore,
        };
        _inspectorPanel = new IMGUIContainer(DrawInspectorPanel)
        {
            name = "InspectorPanel",
            focusable = true,
        };
        RegisterInspectorDragPassthrough(_inspectorPanel);
        ApplyInspectorLayout();

        rootView.Add(_toolbar);
        rootView.Add(_splitterHandle);
        rootView.Add(_splitterCursor);
        rootView.Add(_inspectorPanel);
    }

    /// <summary>与 CombatGraphAsset Inspector 同款 IMGUI 按钮（有按下反馈）。</summary>
    void DrawToolbarImgui()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(_ownerAsset == null))
            {
                if (GUILayout.Button("Validate", GUILayout.Height(24f), GUILayout.Width(88f)))
                {
                    CombatFlowGraphInputDebug.Log("Toolbar Validate clicked");
                    RunValidateOnly();
                }

                if (GUILayout.Button("Validate && Compile", GUILayout.Height(24f), GUILayout.Width(148f)))
                {
                    CombatFlowGraphInputDebug.Log("Toolbar Validate&&Compile clicked");
                    RunValidateAndCompile(closeOnSuccess: true);
                }

                if (GUILayout.Button("Refresh", GUILayout.Height(24f), GUILayout.Width(72f)))
                {
                    RefreshInspectorAndGraph();
                }
            }

            GUILayout.Space(8f);

            EditorGUI.BeginChangeCheck();
            _searchText = EditorGUILayout.TextField(
                _searchText,
                ResolveToolbarSearchFieldStyle(),
                GUILayout.Width(180f),
                GUILayout.Height(20f));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(SearchPrefsKey, _searchText ?? string.Empty);
                CombatView?.SetSearchFilter(_searchText);
            }

            if (GUILayout.Button(
                    CombatFlowGraphInputDebug.Enabled ? "Input Dbg*" : "Input Dbg",
                    GUILayout.Height(24f),
                    GUILayout.Width(72f)))
            {
                ToggleInputDebug();
            }

            if (GUILayout.Button(
                    CombatFlowGraphInspectorDiagnostics.Enabled ? "Insp Dbg*" : "Insp Dbg",
                    GUILayout.Height(24f),
                    GUILayout.Width(72f)))
            {
                ToggleInspectorDebug();
            }
        }
    }

    void ToggleInspectorDebug()
    {
        CombatFlowGraphInspectorDiagnostics.Enabled = !CombatFlowGraphInspectorDiagnostics.Enabled;
        if (CombatFlowGraphInspectorDiagnostics.Enabled)
        {
            CombatFlowGraphInspectorDiagnostics.LogEvent(
                $"DEBUG ON graph={(_ownerAsset != null ? _ownerAsset.name : "null")}");
        }
        _toolbarImgui?.MarkDirtyRepaint();
        _inspectorPanel?.MarkDirtyRepaint();
    }

    static GUIStyle ResolveToolbarSearchFieldStyle()
    {
        var style = GUI.skin.FindStyle("ToolbarSeachTextField")
            ?? GUI.skin.FindStyle("ToolbarSearchTextField");
        if (style != null)
        {
            return style;
        }

        return EditorStyles.toolbarTextField ?? EditorStyles.textField;
    }

    void RunValidateOnly()
    {
        if (!EnsureOwnerBoundForToolbar())
        {
            return;
        }

        // EdgeKind 等仅存于 ProcessorView 边 meta；不 Push 则 asset.flowEdges 仍为默认 Flow。
        if (!TryPushAuthoringFromGraphView())
        {
            return;
        }

        var result = CombatFlowGraphValidator.Validate(_ownerAsset);
        EditorUtility.DisplayDialog("Combat Flow Validate", result.Summary, "OK");
    }

    void RunValidateAndCompile(bool closeOnSuccess)
    {
        if (!EnsureOwnerBoundForToolbar())
        {
            return;
        }

        if (!TryPushAuthoringFromGraphView())
        {
            return;
        }

        var ok = CombatFlowGraphCompiler.TryCompile(_ownerAsset, out var report);
        MarkGraphClean();
        if (!ok)
        {
            EditorUtility.DisplayDialog("Combat Flow Compile", report, "OK");
            _toolbarImgui?.MarkDirtyRepaint();
            return;
        }

        if (closeOnSuccess)
        {
            Close();
            return;
        }

        EditorUtility.DisplayDialog("Combat Flow Compile", report, "OK");
        _toolbarImgui?.MarkDirtyRepaint();
    }

    bool TryPushAuthoringFromGraphView()
    {
        if (_ownerAsset == null)
        {
            return false;
        }

        if (graph is CombatFlowProcessorGraph view)
        {
            CombatFlowGraphSync.PushToAuthoring(_ownerAsset, view);
            return true;
        }

        if (CombatFlowGraphSync.TryEnsureProcessorView(_ownerAsset, out var err))
        {
            CombatFlowGraphSync.PushToAuthoring(_ownerAsset, _ownerAsset.ProcessorView);
            return true;
        }

        if (!string.IsNullOrEmpty(err))
        {
            EditorUtility.DisplayDialog("Combat Flow Graph", err, "OK");
        }

        return false;
    }

    void ToggleInputDebug()
    {
        CombatFlowGraphInputDebug.Enabled = !CombatFlowGraphInputDebug.Enabled;
        _toolbarImgui?.MarkDirtyRepaint();
        CombatFlowGraphInputDebug.Log(
            CombatFlowGraphInputDebug.Enabled
                ? "Input debug ON — 点击 Toolbar / Splitter / Graph，看 Console 命中 target"
                : "Input debug OFF");
    }

    void RegisterInputDebugHooks()
    {
        if (_inputDebugRegistered || rootView == null)
        {
            return;
        }

        _inputDebugRegistered = true;
        rootView.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);

        if (_toolbar != null)
        {
            _toolbar.RegisterCallback<PointerDownEvent>(evt =>
            {
                CombatFlowGraphInputDebug.Log(
                    $"Toolbar PointerDown target={CombatFlowGraphInputDebug.Describe(evt.target as VisualElement)}");
            }, TrickleDown.TrickleDown);
        }
    }

    void OnRootPointerDown(PointerDownEvent evt)
    {
        CombatFlowGraphInputDebug.Log(
            $"Root PointerDown target={CombatFlowGraphInputDebug.Describe(evt.target as VisualElement)} " +
            $"pos={evt.position} chromeOnTop={IsChromeAboveGraph(evt.target as VisualElement)}");
    }

    static bool IsChromeAboveGraph(VisualElement target)
    {
        if (target == null)
        {
            return false;
        }

        while (target != null)
        {
            var name = target.name;
            if (name is "CombatFlowToolbar" or "ToolbarImgui" or "SplitterHandle" or "InspectorPanel" or "SplitterCursor")
            {
                return true;
            }

            if (target is BaseGraphView)
            {
                return false;
            }

            target = target.parent;
        }

        return false;
    }

    void RegisterInspectorDragPassthrough(IMGUIContainer panel)
    {
        if (panel == null)
        {
            return;
        }

        panel.RegisterCallback<DragEnterEvent>(_ => { }, TrickleDown.TrickleDown);
        panel.RegisterCallback<DragUpdatedEvent>(evt =>
        {
            if (DragAndDrop.objectReferences is { Length: > 0 })
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                evt.StopPropagation();
            }
        }, TrickleDown.TrickleDown);
        panel.RegisterCallback<DragPerformEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
    }

    /// <summary>GraphView 后加入会盖住 Toolbar/Splitter；压到最底并抬升 Chrome。</summary>
    void EnsureChromeOnTop()
    {
        if (graphView != null)
        {
            graphView.SendToBack();
        }

        _toolbar?.BringToFront();
        _splitterHandle?.BringToFront();
        _splitterCursor?.BringToFront();
        _inspectorPanel?.BringToFront();

        CombatFlowGraphInputDebug.Log(
            $"EnsureChromeOnTop graphIdx={GetChildIndex(graphView)} toolbarIdx={GetChildIndex(_toolbar)} " +
            $"splitterIdx={GetChildIndex(_splitterHandle)} inspectorIdx={GetChildIndex(_inspectorPanel)}");
    }

    static int GetChildIndex(VisualElement element)
    {
        if (element?.parent == null)
        {
            return -1;
        }

        return element.parent.IndexOf(element);
    }

    void RefreshInspectorAndGraph()
    {
        if (_ownerAsset == null || graph is not CombatFlowProcessorGraph)
        {
            return;
        }

        CombatView?.BindEdgeSelectionHooks();
        _inspectorPanel?.MarkDirtyRepaint();
        Repaint();
    }

    void ApplyInspectorLayout()
    {
        if (_inspectorPanel == null)
        {
            return;
        }

        _inspectorPanel.style.position = Position.Absolute;
        _inspectorPanel.style.right = 0;
        _inspectorPanel.style.top = ToolbarHeight;
        _inspectorPanel.style.bottom = 0;
        _inspectorPanel.style.width = _inspectorWidth;
        _inspectorPanel.style.paddingLeft = 0;
        _inspectorPanel.style.paddingRight = 0;
        _inspectorPanel.style.paddingTop = 0;
        _inspectorPanel.style.paddingBottom = 0;

        ApplySplitterLayout(_splitterHandle);
        ApplySplitterLayout(_splitterCursor);

        if (graphView != null)
        {
            ApplyGraphViewMargins(graphView);
        }
    }

    void ApplySplitterLayout(VisualElement element)
    {
        if (element == null)
        {
            return;
        }

        element.style.position = Position.Absolute;
        element.style.top = ToolbarHeight;
        element.style.bottom = 0;
        element.style.width = SplitterHitWidth;
        UpdateSplitterPosition(element);
    }

    void ApplyGraphViewMargins(VisualElement view)
    {
        if (view == null)
        {
            return;
        }

        view.style.marginLeft = OuterGutter;
        view.style.marginRight = _inspectorWidth + OuterGutter;
        view.style.marginBottom = OuterGutter;
    }

    void UpdateSplitterPosition(VisualElement element)
    {
        if (element == null)
        {
            return;
        }

        element.style.right = _inspectorWidth - SplitterHitWidth * 0.5f;
    }

    VisualElement CreateSplitterHandle()
    {
        var splitter = new VisualElement
        {
            name = "SplitterHandle",
            pickingMode = PickingMode.Position,
            style =
            {
                backgroundColor = SplitterNormalColor,
            },
        };

        splitter.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (!_splitterDragging)
            {
                splitter.style.backgroundColor = SplitterHoverColor;
            }
        });

        splitter.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            if (!_splitterDragging)
            {
                splitter.style.backgroundColor = SplitterNormalColor;
            }
        });

        splitter.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0)
            {
                return;
            }

            _splitterDragging = true;
            splitter.style.backgroundColor = SplitterHoverColor;
            splitter.CaptureMouse();
            CombatFlowGraphInputDebug.Log("Splitter UIToolkit MouseDown captured");
            evt.StopPropagation();
        });

        splitter.RegisterCallback<MouseMoveEvent>(evt =>
        {
            if (!_splitterDragging)
            {
                return;
            }

            _inspectorWidth = Mathf.Clamp(
                _inspectorWidth - evt.mouseDelta.x,
                InspectorWidthMin,
                InspectorWidthMax);
            ApplyInspectorLayout();
            CombatFlowGraphInputDebug.Log($"Splitter Drag width={_inspectorWidth:F0}");
            evt.StopPropagation();
        });

        splitter.RegisterCallback<MouseUpEvent>(evt =>
        {
            if (!_splitterDragging)
            {
                return;
            }

            _splitterDragging = false;
            splitter.ReleaseMouse();
            splitter.style.backgroundColor = SplitterHoverColor;
            EditorPrefs.SetFloat(InspectorWidthPrefsKey, _inspectorWidth);
            CombatFlowGraphInputDebug.Log("Splitter UIToolkit MouseUp");
            evt.StopPropagation();
        });

        return splitter;
    }

    /// <summary>仅绘制 ←→ 光标；pickingMode=Ignore，不抢 MouseDown。</summary>
    void DrawSplitterCursorOnly()
    {
        var height = _splitterCursor != null && _splitterCursor.contentRect.height > 1f
            ? _splitterCursor.contentRect.height
            : Mathf.Max(1f, position.height - ToolbarHeight);
        var rect = GUILayoutUtility.GetRect(SplitterHitWidth, height, GUILayout.ExpandWidth(false));
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
    }

    protected override void InitializeGraphView(BaseGraphView view)
    {
        ApplyInspectorLayout();

        if (view is CombatFlowGraphView cfv)
        {
            cfv.BindEdgeSelectionHooks();
        }

        EnsureChromeOnTop();
        RegisterInputDebugHooks();

        view?.schedule.Execute(() =>
        {
            CombatView?.SetSearchFilter(EditorPrefs.GetString(SearchPrefsKey, string.Empty));
            CombatView?.BindEdgeSelectionHooks();
            NotifyGraphSelectionChanged(allowClearWhenEmpty: false);
            EnsureChromeOnTop();
        }).ExecuteLater(50);
    }

    public void CommitInspectorEdge(SerializableEdge edge)
    {
        var prev = _selection.Committed;
        if (graph is CombatFlowProcessorGraph pg)
        {
            if (prev.Edge != null && edge != null && prev.Edge.GUID != edge.GUID)
            {
                CombatFlowGraphEdgeInspector.CommitLateWindowIfDirty(pg, prev.Edge.GUID);
                CombatFlowGraphEdgeInspector.ClearImguiEditFocusForSelectionChange();
            }

            var metaLate = edge != null ? pg.GetOrCreateEdgeMeta(edge.GUID).Authoring.LateWindowSeconds : 0f;
            _selection.CommitEdge(edge);
            CombatFlowGraphInspectorDiagnostics.LogEdgeCommit(prev.Edge, edge, metaLate);
        }
        else
        {
            _selection.CommitEdge(edge);
        }

        _inspectorPanel?.MarkDirtyRepaint();
    }

    public void CommitInspectorNode(BaseNode node)
    {
        var prev = _selection.Committed;
        if (prev.Edge != null && graph is CombatFlowProcessorGraph pg)
        {
            CombatFlowGraphEdgeInspector.CommitLateWindowIfDirty(pg, prev.Edge.GUID);
            CombatFlowGraphEdgeInspector.ClearImguiEditFocusForSelectionChange();
        }

        _selection.CommitNode(node);
        _inspectorPanel?.MarkDirtyRepaint();
    }

    public void NotifyGraphSelectionChanged(bool allowClearWhenEmpty)
    {
        if (graphView == null)
        {
            return;
        }

        var before = _selection.Committed;
        _selection.OnGraphSelectionChanged(graphView, allowClearWhenEmpty);
        var after = _selection.Committed;

        if (graph is CombatFlowProcessorGraph pg
            && before.Edge != null
            && (after.Edge == null || after.Edge.GUID != before.Edge.GUID))
        {
            CombatFlowGraphEdgeInspector.CommitLateWindowIfDirty(pg, before.Edge.GUID);
            CombatFlowGraphEdgeInspector.ClearImguiEditFocusForSelectionChange();
        }

        CombatFlowGraphInspectorDiagnostics.LogSelectionSync(in before, in after, graphView);
        _inspectorPanel?.MarkDirtyRepaint();
    }

    void DrawInspectorPanel()
    {
        CombatFlowGraphInspectorLayout.EnsureStyles();

        _inspectorScroll = EditorGUILayout.BeginScrollView(
            _inspectorScroll,
            false,
            false,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginVertical(CombatFlowGraphInspectorLayout.ContentPaddingStyle);
        DrawInspectorPanelContent();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    void DrawInspectorPanelContent()
    {
        if (_ownerAsset == null)
        {
            EditorGUILayout.HelpBox(
                "未绑定 CombatGraphAsset。\n" +
                "请从 Project 选中 CombatGraphAsset → Inspector →「Open Graph Editor」打开；" +
                "勿用 Window/Combat Flow Graph 空窗口。",
                MessageType.Warning);
            return;
        }

        if (graph is not CombatFlowProcessorGraph pg)
        {
            EditorGUILayout.LabelField("Graph 视图未加载");
            return;
        }

        var snap = _selection.Committed;

        switch (snap.Kind)
        {
            case CombatFlowInspectorTargetKind.Multi:
                CombatFlowGraphEdgeInspector.DrawMultiSelectionSummary(in snap);
                return;
            case CombatFlowInspectorTargetKind.FlowEdge:
                CombatFlowGraphEdgeInspectorContext.CommittedEdge = snap.Edge;
                if (CombatFlowGraphEdgeInspector.Draw(_ownerAsset, pg, snap.Edge))
                {
                    MarkGraphDirty();
                }

                return;
            case CombatFlowInspectorTargetKind.UtilityEdge:
                CombatFlowGraphInspectorFeedback.DrawUtilityEdge(snap.Edge);
                return;
            case CombatFlowInspectorTargetKind.RelayNode:
                CombatFlowGraphInspectorFeedback.DrawRelayNode(snap.Node as RelayNode);
                return;
            case CombatFlowInspectorTargetKind.UtilityNode:
                CombatFlowGraphInspectorFeedback.DrawUtilityNode(snap.Node);
                return;
            case CombatFlowInspectorTargetKind.FlowNode:
                EditorGUI.BeginChangeCheck();
                CombatFlowGraphNodeInspector.Draw(snap.Node);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(pg);
                    MarkGraphDirty();
                }

                return;
        }

        EditorGUILayout.HelpBox(
            "选中画布上的一条边或一个节点以编辑属性。\n\n" +
            "Flow 边：Action→Action，Route 入口须与 To 动作一致。\n" +
            "Interrupt 边：Action→End，Route 藏在边内。\n\n" +
            "框选多个对象时只显示汇总。",
            MessageType.Info);

        EditorGUILayout.Space(6f);
        CombatFlowChainPreviewDrawer.Draw(_ownerAsset);
    }

    bool EnsureOwnerBoundForToolbar()
    {
        if (_ownerAsset != null)
        {
            return true;
        }

        EditorUtility.DisplayDialog(
            "Combat Flow Graph",
            "当前窗口未绑定 CombatGraphAsset。\n\n" +
            "正确入口：Project 选中 CombatGraphAsset → Inspector →「Open Graph Editor」。\n" +
            "Window/Combat Flow Graph 仅打开空壳，Sync/Validate 不可用。",
            "OK");
        return false;
    }

    void SyncAndCompile(bool silent)
    {
        if (_ownerAsset == null)
        {
            return;
        }

        if (!TryPushAuthoringFromGraphView())
        {
            return;
        }

        CombatFlowGraphCompiler.TryCompile(_ownerAsset, out var report);
        MarkGraphClean();
        if (!silent)
        {
            EditorUtility.DisplayDialog("Combat Flow Compile", report, "OK");
        }
    }
}
#endif
