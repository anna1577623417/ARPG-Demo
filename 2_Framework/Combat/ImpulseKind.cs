/// <summary>
/// 实体反馈中的冲量语义。属于 2_Framework/Combat，不绑定具体 Motor 实现。
/// </summary>
public enum ImpulseKind : byte
{
    Small = 0,
    Large = 1,
    Launch = 2,
    Custom = 3,
}
