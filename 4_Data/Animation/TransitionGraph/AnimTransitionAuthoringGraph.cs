using System;
using GraphProcessor;
using UnityEngine;

/// <summary>
/// 243.7 — The sole editable animation-transition graph asset. BaseGraph owns nodes and edges;
/// this type intentionally has no owner-asset, mirror view, or edge metadata collection.
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Animation/Transition Authoring Graph", fileName = "AnimTransitionGraph_")]
public sealed class AnimTransitionAuthoringGraph : BaseGraph
{
    public const int CurrentSchemaVersion = 2;

    [SerializeField] string graphGuid;
    [SerializeField] int schemaVersion = CurrentSchemaVersion;
    [SerializeField] AnimTransitionGraphDomain domain = AnimTransitionGraphDomain.Any;
    [SerializeField] CompiledAnimTransitionGraph compiledGraph;
    [SerializeField] bool compileRequired = true;
    [SerializeField, TextArea(2, 5)] string lastCompileReport;

    bool graphChangeHooked;

    public string GraphGuid
    {
        get
        {
            EnsureGraphGuid();
            return graphGuid;
        }
    }

    public int SchemaVersion => schemaVersion;
    public bool MigrationRequired => schemaVersion > 0 && schemaVersion < CurrentSchemaVersion;
    public bool SchemaSupported => schemaVersion == CurrentSchemaVersion;
    public AnimTransitionGraphDomain Domain => domain;
    public CompiledAnimTransitionGraph CompiledGraph => compiledGraph;
    public bool CompileRequired => compileRequired;
    public string LastCompileReport => lastCompileReport ?? string.Empty;

    protected override void OnEnable()
    {
        EnsureGraphGuid();
        base.OnEnable();
        if (!graphChangeHooked)
        {
            onGraphChanges += OnGraphChanged;
            graphChangeHooked = true;
        }
    }

    protected override void OnDisable()
    {
        if (graphChangeHooked)
        {
            onGraphChanges -= OnGraphChanged;
            graphChangeHooked = false;
        }

        base.OnDisable();
    }

    void OnValidate()
    {
        EnsureGraphGuid();
        if (schemaVersion <= 0)
        {
            schemaVersion = CurrentSchemaVersion;
        }
    }

    void OnGraphChanged(GraphChanges _)
    {
        MarkCompileRequired();
    }

    public void MarkCompileRequired()
    {
        compileRequired = true;
    }

    public void EditorSetDomain(AnimTransitionGraphDomain value)
    {
        if (domain == value)
        {
            return;
        }

        domain = value;
        MarkCompileRequired();
    }

    public void EditorSetCompiledGraph(CompiledAnimTransitionGraph value, bool valid, string report)
    {
        compiledGraph = value;
        compileRequired = !valid;
        lastCompileReport = report ?? string.Empty;
    }

    void EnsureGraphGuid()
    {
        if (string.IsNullOrEmpty(graphGuid))
        {
            graphGuid = Guid.NewGuid().ToString("N");
        }
    }
}
