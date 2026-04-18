/// <summary>
/// Where to place the character becoming active when switching party members.
/// </summary>
public enum SwitchSpawnPolicy
{
    /// <summary>New active pawn inherits world pose from the previously active pawn (Genshin-like).</summary>
    InheritPreviousActivePose = 0,

    /// <summary>Use per-slot anchors assigned on <c>SystemRoot</c> (fallback: primary spawn).</summary>
    SlotAnchors = 1,

    /// <summary>Always warp to the primary <see cref="SpawnPoint"/>.</summary>
    PrimarySpawnOnly = 2,
}
