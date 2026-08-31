/// <summary>How presentation changes root-space ownership. Gameplay Transform authority is never affected.</summary>
public enum SpatialHandoffMode : byte
{
    None = 0,
    SameSpace = 1,
    Atomic = 2,
}
