public enum LegacyAnimatorRootMotionDecision : byte
{
    NotRequested = 0,
    Denied = 1,
    AllowedExactLegacyAsset = 2,
}

/// <summary>
/// 232 Stage A quarantine. The allowlist is intentionally empty: no reachable Runtime action may
/// grant Animator direct ownership of the entity root until an explicit legacy asset is approved.
/// </summary>
public static class LegacyAnimatorRootMotionPolicy
{
    public static LegacyAnimatorRootMotionDecision Resolve(bool requested)
    {
        return requested
            ? LegacyAnimatorRootMotionDecision.Denied
            : LegacyAnimatorRootMotionDecision.NotRequested;
    }
}
