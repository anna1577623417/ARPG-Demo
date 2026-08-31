/// <summary>Presentation root-yaw policy only; this is not a LogicFacing command.</summary>
public enum RootYawChannelMode : byte
{
    Preserve = 0,
    SnapToTarget = 1,
}

public enum RootTranslationChannelMode : byte
{
    Preserve = 0,
    Atomic = 1,
    Blend = 2,
}
