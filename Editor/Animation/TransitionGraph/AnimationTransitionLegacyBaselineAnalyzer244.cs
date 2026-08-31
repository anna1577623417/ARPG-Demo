#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public sealed class AnimationTransitionLegacyBaselineEntry244
{
    public string Key;
    public AnimationRequestDomain Domain;
    public float BlendDuration;
    public string SourcePath;
    public bool IsConflict;
}

public sealed class AnimationTransitionLegacyBaselineReport244
{
    public string Scope;
    public int PolicyCount;
    public readonly List<string> Findings = new List<string>();
    public readonly List<AnimationTransitionLegacyBaselineEntry244> Entries = new List<AnimationTransitionLegacyBaselineEntry244>();
    public bool CanApply => false;
    public bool HasConflicts
    {
        get
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].IsConflict) return true;
            }
            return false;
        }
    }
    public string Summary => "Legacy baseline · scope=" + Scope + " · policies=" + PolicyCount + " · apply=explicit-only";

    internal void Add(string key, AnimationRequestDomain domain, float duration, string sourcePath)
    {
        if (string.IsNullOrEmpty(key))
        {
            Findings.Add("Skipped legacy source with empty presentation key: " + sourcePath);
            return;
        }

        var normalizedDuration = Math.Max(0f, duration);
        for (var i = 0; i < Entries.Count; i++)
        {
            var existing = Entries[i];
            if (!string.Equals(existing.Key, key, StringComparison.Ordinal)) continue;
            if (Math.Abs(existing.BlendDuration - normalizedDuration) <= 0.0001f) return;
            existing.IsConflict = true;
            Entries[i] = existing;
            Entries.Add(new AnimationTransitionLegacyBaselineEntry244
            {
                Key = key,
                Domain = domain,
                BlendDuration = normalizedDuration,
                SourcePath = sourcePath,
                IsConflict = true,
            });
            Findings.Add("Conflict blocked: " + key + " has multiple legacy durations.");
            return;
        }

        Entries.Add(new AnimationTransitionLegacyBaselineEntry244
        {
            Key = key,
            Domain = domain,
            BlendDuration = normalizedDuration,
            SourcePath = sourcePath,
        });
    }
}

/// <summary>Bounded, read-only legacy analyzer. It never scans the project and never writes assets.</summary>
public static class AnimationTransitionLegacyBaselineAnalyzer244
{
    public static AnimationTransitionLegacyBaselineReport244 AnalyzeGraph(AnimTransitionAuthoringGraph graph)
    {
        var report = new AnimationTransitionLegacyBaselineReport244 { Scope = "selected graph only" };
        if (graph == null || graph.CompiledGraph == null)
        {
            report.Findings.Add("No compiled graph in selected asset.");
            return report;
        }

        report.PolicyCount = graph.CompiledGraph.TypedPolicyCount;
        if (report.PolicyCount == 0) report.Findings.Add("Graph has no typed policy snapshot; compile v2 first.");
        else report.Findings.Add("Typed policies are available for comparison; legacy import remains disabled.");
        return report;
    }
}
#endif
