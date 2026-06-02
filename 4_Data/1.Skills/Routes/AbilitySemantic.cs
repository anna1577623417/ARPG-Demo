/// <summary>
/// 能力语义（136.1 L4）— 表达「当前能做什么」，不是动作名。
/// </summary>
public enum AbilitySemantic : ushort
{
    None = 0,
    Attack = 1,
    Mobility = 2,
    Defense = 3,
    Counter = 4,
    Special = 5,
    Interaction = 6,
}
