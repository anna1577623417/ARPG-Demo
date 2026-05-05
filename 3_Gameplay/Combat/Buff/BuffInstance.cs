using System;

public struct BuffInstance : IEquatable<BuffInstance>
{
    public int RuntimeId;
    public BuffDefinitionSO Definition;
    public object Source;
    public float RemainingSeconds;
    public float PeriodTimer;

    public bool Equals(BuffInstance other) => RuntimeId == other.RuntimeId;
    public override bool Equals(object obj) => obj is BuffInstance other && Equals(other);
    public override int GetHashCode() => RuntimeId;
}
