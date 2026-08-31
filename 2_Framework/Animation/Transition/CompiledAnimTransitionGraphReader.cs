/// <summary>Runtime-safe read facade for 243.7 compiler output. It has no authoring graph dependency.</summary>
public sealed class CompiledAnimTransitionGraphReader
{
    readonly CompiledAnimTransitionGraph graph;

    public CompiledAnimTransitionGraphReader(CompiledAnimTransitionGraph compiledGraph)
    {
        graph = compiledGraph;
    }

    public bool IsAvailable => graph != null && graph.NodeCount > 0 && graph.OutputCount > 0;
    public int SchemaVersion => graph != null ? graph.SchemaVersion : 0;
    public string GraphGuid => graph != null ? graph.GraphGuid : string.Empty;
    public string GraphHash => graph != null ? graph.GraphHash : string.Empty;
    public int RuleCount => graph != null ? graph.RuleCount : 0;
    public int TypedPolicyCount => graph != null ? graph.TypedPolicyCount : 0;

    public bool TryGetNode(int index, out CompiledAnimTransitionNode node)
    {
        if (graph != null)
        {
            return graph.TryGetNode(index, out node);
        }

        node = default;
        return false;
    }

    public bool TryGetPrimaryOutput(out CompiledAnimTransitionOutput output)
    {
        if (graph != null)
        {
            return graph.TryGetOutput(0, out output);
        }

        output = default;
        return false;
    }

    public bool TryGetRule(int index, out CompiledAnimationTransitionRule244 rule)
    {
        if (graph != null) return graph.TryGetRule(index, out rule);
        rule = default;
        return false;
    }

    public bool TryGetTypedPolicy(int index, out CompiledAnimationTransitionPolicy244 policy)
    {
        if (graph != null) return graph.TryGetTypedPolicy(index, out policy);
        policy = default;
        return false;
    }
}
