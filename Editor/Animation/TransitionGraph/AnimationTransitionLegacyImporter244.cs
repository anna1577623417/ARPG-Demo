#if UNITY_EDITOR
using System.Collections.Generic;

public sealed class AnimationTransitionLegacyImportCommand244
{
    public string Key;
    public AnimationRequestDomain Domain;
    public float BlendDuration;
    public string SourcePath;
}

/// <summary>Explicit-input legacy preview. It never scans AssetDatabase and never writes assets.</summary>
public static class AnimationTransitionLegacyImporter244
{
    public static AnimationTransitionLegacyBaselineReport244 ScanSelectedGraph(
        AnimTransitionAuthoringGraph selectedGraph,
        ActionDataSO[] actionDataSources,
        LocomotionStateBinding[] locomotionBindings,
        PlayerAnimManagerSO legacyLibrary)
    {
        var report = AnimationTransitionLegacyBaselineAnalyzer244.AnalyzeGraph(selectedGraph);
        if (selectedGraph == null) return report;

        var domain = ToRequestDomain(selectedGraph.Domain);
        if (actionDataSources != null)
        {
            for (var i = 0; i < actionDataSources.Length; i++)
            {
                var action = actionDataSources[i];
                if (action == null) continue;
                var key = action.MainClip != null ? action.MainClip.name : action.name;
                report.Add(key, domain, action.CrossfadeTime, "ActionDataSO[" + i + "].CrossfadeTime");
            }
        }

        if (locomotionBindings != null)
        {
            for (var i = 0; i < locomotionBindings.Length; i++)
            {
                var binding = locomotionBindings[i];
                var action = binding.ResolveLocomotionAction();
                if (action != null)
                {
                    var key = action.MainClip != null ? action.MainClip.name : action.name;
                    report.Add(key, domain, action.CrossfadeTime, "LocomotionStateBinding[" + i + "].LocomotionAction.CrossfadeTime");
                    continue;
                }

#pragma warning disable CS0618
                if (binding.ContinuousClip != null)
                {
                    report.Add(binding.ContinuousClip.name, domain,
                        binding.TransitionDuration > 0.0001f ? binding.TransitionDuration : 0.08f,
                        "LocomotionStateBinding[" + i + "].ContinuousClip/TransitionDuration");
                }
#pragma warning restore CS0618
            }
        }

        if (legacyLibrary != null)
        {
            var entries = legacyLibrary.GetAllEntries();
            if (entries != null)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (entry == null || entry.Clip == null) continue;
                    report.Add(entry.Clip.name, domain, entry.TransitionDuration,
                        "PlayerAnimManagerSO.entries[" + i + "].TransitionDuration");
                }
            }
        }

        if (report.Entries.Count == 0)
        {
            report.Findings.Add("No explicit legacy source was supplied for the selected graph.");
        }
        else if (report.HasConflicts)
        {
            report.Findings.Add("Import preview is blocked until each conflicting key has one explicit winner.");
        }
        else
        {
            report.Findings.Add("Import preview is reproducible; apply remains an explicit 244.9 transaction.");
        }
        return report;
    }

    public static List<AnimationTransitionLegacyImportCommand244> BuildPreview(
        AnimationTransitionLegacyBaselineReport244 report)
    {
        var commands = new List<AnimationTransitionLegacyImportCommand244>();
        if (report == null || report.HasConflicts) return commands;
        for (var i = 0; i < report.Entries.Count; i++)
        {
            var entry = report.Entries[i];
            commands.Add(new AnimationTransitionLegacyImportCommand244
            {
                Key = entry.Key,
                Domain = entry.Domain,
                BlendDuration = entry.BlendDuration,
                SourcePath = entry.SourcePath,
            });
        }
        return commands;
    }

    static AnimationRequestDomain ToRequestDomain(AnimTransitionGraphDomain domain)
    {
        switch (domain)
        {
            case AnimTransitionGraphDomain.Locomotion: return AnimationRequestDomain.Locomotion;
            case AnimTransitionGraphDomain.Airborne: return AnimationRequestDomain.Airborne;
            case AnimTransitionGraphDomain.Action: return AnimationRequestDomain.Action;
            case AnimTransitionGraphDomain.Turn: return AnimationRequestDomain.Turn;
            case AnimTransitionGraphDomain.Hit: return AnimationRequestDomain.Reaction;
            default: return AnimationRequestDomain.Unknown;
        }
    }
}
#endif
