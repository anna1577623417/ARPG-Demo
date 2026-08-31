using System;
using System.Globalization;
using UnityEngine;

/// <summary>244.8 L3 — Matchable presentation semantics. This mask never carries gameplay intent.</summary>
[Flags]
public enum AnimationPresentationSemanticMask244 : uint
{
    None = 0,
    Continuous = 1u << 0,
    Start = 1u << 1,
    Stop = 1u << 2,
    Turn = 1u << 3,
    HardReaction = 1u << 4,
}

/// <summary>Stable authoring identity for one presentation variant.</summary>
[Serializable]
public struct AnimationPresentationIdentity244
{
    [SerializeField] AnimationRequestDomain domain;
    [SerializeField] string key;
    [SerializeField] AnimationPresentationSemanticMask244 semantics;
    [SerializeField] string rootSpaceKey;

    public AnimationRequestDomain Domain => domain;
    public string Key => key ?? string.Empty;
    public AnimationPresentationSemanticMask244 Semantics => semantics;
    public string RootSpaceKey => rootSpaceKey ?? string.Empty;
    public bool IsValid => domain != AnimationRequestDomain.Unknown && !string.IsNullOrEmpty(Key);

    public AnimationPresentationIdentity244(
        AnimationRequestDomain domain,
        string key,
        AnimationPresentationSemanticMask244 semantics,
        string rootSpaceKey)
    {
        this.domain = domain;
        this.key = key ?? string.Empty;
        this.semantics = semantics;
        this.rootSpaceKey = rootSpaceKey ?? string.Empty;
    }

    public bool MatchesExact(AnimationPresentationIdentity244 other)
    {
        return Domain == other.Domain
            && string.Equals(Key, other.Key, StringComparison.Ordinal)
            && Semantics == other.Semantics
            && string.Equals(RootSpaceKey, other.RootSpaceKey, StringComparison.Ordinal);
    }

    public bool MatchesSemantic(AnimationPresentationIdentity244 other, AnimationPresentationSemanticMask244 required)
    {
        if (Domain != other.Domain || !string.Equals(RootSpaceKey, other.RootSpaceKey, StringComparison.Ordinal))
        {
            return false;
        }

        return required != AnimationPresentationSemanticMask244.None
            && (Semantics & required) == required
            && (other.Semantics & required) == required;
    }

    public string BuildDeterministicKey()
    {
        return string.Concat(
            ((int)Domain).ToString(CultureInfo.InvariantCulture), ":",
            Key, ":", ((uint)Semantics).ToString(CultureInfo.InvariantCulture), ":", RootSpaceKey);
    }
}

/// <summary>Resolved clip descriptor consumed by a later typed compiler/runtime bridge.</summary>
[Serializable]
public struct AnimationPresentationDescriptor244
{
    public AnimationPresentationIdentity244 Identity;
    public AnimationClip ResolvedClip;
    public AnimationLoopPolicy LoopPolicy;
    public float Speed;
    public float EntryNormalizedTime;
    public string LegacyTransitionBaselineKey;

    public bool IsValid => Identity.IsValid && ResolvedClip != null;
}

/// <summary>Authoring source for a transition policy. Runtime must consume a compiled snapshot.</summary>
public enum AnimationTransitionPolicySource244 : byte
{
    ImportedLegacyBaseline = 0,
    SharedProfile = 1,
    InlineOverride = 2,
    DomainDefault = 3,
}

[Serializable]
public struct AnimationTransitionPolicyReference244
{
    public AnimationTransitionPolicySource244 Source;
    public string ProfileGuid;
    public string ImportedBaselineKey;

    public bool IsValid => Source != AnimationTransitionPolicySource244.SharedProfile || !string.IsNullOrEmpty(ProfileGuid);
}
