/// <summary>
/// Motion 缩放来源。
/// Why: 让位移幅度与角色属性解耦，便于 Buff 系统在不改动作资产的前提下影响手感。
/// </summary>
public enum MotionScaleType
{
    None = 0,
    MoveSpeed = 1,
    AttackSpeed = 2,
    Custom = 3,
}
