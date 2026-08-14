public enum AirCyclePhase : byte
{
    None = 0,
    Rising = 1,
    Falling = 2,
    LandingRouted = 3,
    Closed = 4,
    Cancelled = 5,
}

public enum AirCycleCancelReason : byte
{
    None = 0,
    Dead = 1,
    Teleport = 2,
    EntityDisabled = 3,
    Replaced = 4,
}
