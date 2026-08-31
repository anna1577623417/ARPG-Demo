using System;

/// <summary>Bounded, deterministic Shadow selector. It chooses no clip and has no playback side effect.</summary>
public static class MotionCandidateSelector243
{
    public const int MaximumCandidates = 8;

    public static bool TrySelect(
        in PoseSample source,
        AnimationRequestDomain domain,
        string stance,
        string weapon,
        int layer,
        string rootSpace,
        MotionCandidate243[] candidates,
        out MotionCandidate243 selected,
        out float cost)
    {
        selected = default;
        cost = 0f;
        if (candidates == null || candidates.Length == 0 || candidates.Length > MaximumCandidates
            || domain == AnimationRequestDomain.Unknown || string.IsNullOrEmpty(rootSpace)) return false;

        var found = false;
        var best = float.MaxValue;
        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (!candidate.Matches(domain, stance, weapon, layer, rootSpace)) continue;
            var candidateCost = PoseCostCalculator.ComputeCost(in source, in candidate.Pose);
            if (!found || candidateCost < best || (Math.Abs(candidateCost - best) < 0.0001f
                && string.CompareOrdinal(candidate.ClipKey, selected.ClipKey) < 0))
            {
                selected = candidate;
                best = candidateCost;
                found = true;
            }
        }
        cost = found ? best : 0f;
        return found;
    }
}

public readonly struct MotionCandidate243
{
    public readonly string ClipKey;
    public readonly AnimationRequestDomain Domain;
    public readonly string Stance;
    public readonly string Weapon;
    public readonly int Layer;
    public readonly string RootSpace;
    public readonly PoseSample Pose;

    public MotionCandidate243(string clipKey, AnimationRequestDomain domain, string stance, string weapon, int layer, string rootSpace, in PoseSample pose)
    { ClipKey = clipKey ?? string.Empty; Domain = domain; Stance = stance ?? string.Empty; Weapon = weapon ?? string.Empty; Layer = layer; RootSpace = rootSpace ?? string.Empty; Pose = pose; }

    public bool Matches(AnimationRequestDomain domain, string stance, string weapon, int layer, string rootSpace) =>
        Domain == domain && Layer == layer && string.Equals(Stance, stance ?? string.Empty, StringComparison.Ordinal)
        && string.Equals(Weapon, weapon ?? string.Empty, StringComparison.Ordinal) && string.Equals(RootSpace, rootSpace, StringComparison.Ordinal);
}
