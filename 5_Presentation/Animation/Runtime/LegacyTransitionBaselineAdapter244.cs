using System;

/// <summary>Explicit legacy input supplied by the migration preview; runtime never discovers assets.</summary>
public readonly struct LegacyTransitionBaseline244
{
    public readonly string Key;
    public readonly float BlendDuration;
    public readonly string SourcePath;

    public LegacyTransitionBaseline244(string key, float blendDuration, string sourcePath)
    {
        Key = key ?? string.Empty;
        BlendDuration = IsFinite(blendDuration) ? Math.Max(0f, blendDuration) : 0f;
        SourcePath = sourcePath ?? string.Empty;
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>Converts an explicitly imported legacy duration into the same pure TransitionContext contract.</summary>
public static class LegacyTransitionBaselineAdapter244
{
    public static TransitionContext BuildContext(
        in AnimationPresentationSubmission244 submission,
        in LegacyTransitionBaseline244 baseline)
    {
        var duration = baseline.BlendDuration;
        return new TransitionContext(
            in submission.Request,
            in submission.SourcePresentation,
            submission.TargetRootSpaceKey,
            submission.TargetFootPhase,
            submission.TargetHasValidFootPhase,
            TransitionMode.CrossFade,
            submission.RequestedRootTranslationMode,
            duration,
            0f,
            AnimationPhaseMatchMode.Off,
            submission.IsHardReaction,
            submission.HasRootMotionAdapter,
            string.Empty,
            string.Empty);
    }
}
