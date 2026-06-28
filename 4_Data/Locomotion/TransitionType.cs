/// <summary>
/// 184.4 — Motion Grammar：Action 作为 Transition 时的语义原型。
/// </summary>
public enum TransitionType : byte
{
    None = 0,
    Start = 1,
    End = 2,
    Turn = 3,
    Pivot = 4,
}
