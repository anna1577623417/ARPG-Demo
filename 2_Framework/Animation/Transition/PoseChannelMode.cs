public enum PoseChannelMode : byte
{
    Suppress = 0,
    Snap = 1,
    CrossFade = 2,
    PhaseMatch = 3,
    Inertialization = 4,
}

public enum AnimationPhaseMatchMode : byte
{
    Off = 0,
    IfValid = 1,
    Required = 2,
}
