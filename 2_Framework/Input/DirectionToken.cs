/// <summary>237 L4 — 谁消费了这一次方向 Edge Token。禁止把 Hold 整段当成一次 Claim。</summary>
public enum DirectionTokenOwner : byte
{
    None = 0,
    SkillChord = 1,
    TurnTap = 2,
    Locomotion = 3
}

/// <summary>一次方向 Edge 的身份。id 与 History Token 相同。</summary>
public readonly struct DirectionToken
{
    public readonly int Id;
    public readonly DirectionTokenOwner Owner;

    public DirectionToken(int id, DirectionTokenOwner owner)
    {
        Id = id;
        Owner = owner;
    }

    public bool IsClaimed => Id > 0 && Owner != DirectionTokenOwner.None;
}
