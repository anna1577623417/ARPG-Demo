#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public sealed class AnimTransitionImpactIndex244
{
    public sealed class Entry
    {
        public string NodeGuid;
        public string RuleId;
        public string FromKey;
        public string ToKey;
        public int PolicyIndex;
    }

    readonly List<Entry> entries = new List<Entry>();
    public string GraphGuid { get; private set; }
    public string GraphHash { get; private set; }
    public int Count => entries.Count;
    public IReadOnlyList<Entry> Entries => entries;

    public static AnimTransitionImpactIndex244 Build(AnimTransitionAuthoringGraph graph)
    {
        var index = new AnimTransitionImpactIndex244
        {
            GraphGuid = graph != null ? graph.GraphGuid : string.Empty,
            GraphHash = graph != null && graph.CompiledGraph != null ? graph.CompiledGraph.GraphHash : string.Empty,
        };
        if (graph == null || graph.CompiledGraph == null) return index;
        var reader = new CompiledAnimTransitionGraphReader(graph.CompiledGraph);
        for (var i = 0; i < reader.RuleCount; i++)
        {
            if (!reader.TryGetRule(i, out var rule)) continue;
            index.entries.Add(new Entry
            {
                NodeGuid = rule.SourceNodeGuid,
                RuleId = rule.RuleId,
                FromKey = rule.FromKey,
                ToKey = rule.ToKey,
                PolicyIndex = rule.PolicyIndex,
            });
        }
        return index;
    }

    public string Describe()
    {
        return "Impact index · rules=" + Count + " · graph=" + GraphGuid + " · hash=" + (string.IsNullOrEmpty(GraphHash) ? "uncompiled" : GraphHash);
    }
}
#endif
