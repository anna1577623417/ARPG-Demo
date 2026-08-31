using UnityEngine;

public enum AnimationFootContact243 : byte
{
    Unknown = 0,
    Left = 1,
    Right = 2,
    Double = 3,
    Airborne = 4,
}

/// <summary>Animation-owned phase/contact fact. Unknown is explicit and never inferred from Gameplay grounded state.</summary>
public readonly struct AnimationFootPhaseObservation243
{
    public readonly float Phase;
    public readonly AnimationFootContact243 Contact;
    public readonly bool IsValid;

    public static AnimationFootPhaseObservation243 Unknown => new AnimationFootPhaseObservation243(0f, AnimationFootContact243.Unknown, false);

    public AnimationFootPhaseObservation243(float phase, AnimationFootContact243 contact, bool isValid)
    {
        Phase = Mathf.Repeat(phase, 1f);
        Contact = contact;
        IsValid = isValid && contact != AnimationFootContact243.Unknown;
    }

    public bool IsCompatibleWith(in AnimationFootPhaseObservation243 other) =>
        IsValid && other.IsValid && (Contact == other.Contact || Contact == AnimationFootContact243.Double || other.Contact == AnimationFootContact243.Double);
}
