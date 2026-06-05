#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>147.1 Combat Flow GraphProcessor 画布。</summary>
public sealed class CombatFlowGraphWindow : BaseGraphWindow
{
    CombatGraphAsset _ownerAsset;
    BaseGraphView _graphView;

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
        win.graph = asset.ProcessorView;
        if (win.isGraphLoaded && asset.ProcessorView != null)
        {
            win.ReloadGraph(asset.ProcessorView);
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
        base.OnEnable();
        if (graph != null)
        {
            graph.onGraphChanges += OnGraphChanged;
        }
    }

    protected override void OnDisable()
    {
        if (graph != null)
        {
            graph.onGraphChanges -= OnGraphChanged;
        }

        SyncAndCompile(silent: true);
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
            }
        }
    }

    protected override void InitializeWindow(BaseGraph graph)
    {
        var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 28 } };

        var syncBtn = new Button(() =>
        {
            if (!EnsureOwnerBoundForToolbar())
            {
                return;
            }

            SyncAndCompile(silent: false);
        })
        { text = "Sync && Compile" };
        syncBtn.style.marginLeft = 4;
        syncBtn.style.marginTop = 2;
        toolbar.Add(syncBtn);

        var validateBtn = new Button(() =>
        {
            if (!EnsureOwnerBoundForToolbar())
            {
                return;
            }

            SyncAndCompile(silent: true);
            var result = CombatFlowGraphValidator.Validate(_ownerAsset);
            EditorUtility.DisplayDialog("Validate", result.Summary, "OK");
        })
        { text = "Validate" };
        validateBtn.style.marginLeft = 4;
        validateBtn.style.marginTop = 2;
        toolbar.Add(validateBtn);

        rootView.Add(toolbar);

        var edgePanel = new IMGUIContainer(DrawEdgeInspectorPanel)
        {
            style = { height = 320, flexShrink = 0 },
        };
        rootView.Add(edgePanel);

        var view = new BaseGraphView(this);
        view.style.flexGrow = 1;
        rootView.Add(view);
    }

    protected override void InitializeGraphView(BaseGraphView view)
    {
        _graphView = view;
    }

    void DrawEdgeInspectorPanel()
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

        CombatFlowGraphEdgeInspector.Draw(_ownerAsset, pg, _graphView);
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
        if (_ownerAsset == null || graph is not CombatFlowProcessorGraph view)
        {
            return;
        }

        CombatFlowGraphSync.PushToAuthoring(_ownerAsset, view);
        CombatFlowGraphCompiler.TryCompile(_ownerAsset, out var report);
        if (!silent)
        {
            EditorUtility.DisplayDialog("Combat Flow Sync", report, "OK");
        }
    }
}
#endif
