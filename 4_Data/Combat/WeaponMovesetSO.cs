using UnityEngine;

/// <summary>
/// 武器招式表（聚合层）：插在 Controller 与 <see cref="ActionDataSO"/> 之间。
/// 换武器 / 换角色 = 换一张表，Controller 不持有零散 defaultXXX 引用。
/// </summary>
[CreateAssetMenu(fileName = "WeaponMoveset", menuName = "ARPG/Combat/Weapon Moveset")]
public class WeaponMovesetSO : ScriptableObject
{
    [Header("Discrete mobility (Action 管线)")]
    [Tooltip("翻滚：对应 GameplayIntentKind.Dodge")]
    public ActionDataSO DodgeAction;

    [Tooltip("剑道冲刺 / 直线爆发位移：对应 GameplayIntentKind.SwordDash")]
    public ActionDataSO SwordDashAction;

    [Header("Combo chains")]
    [Tooltip("轻攻击连段，按下标顺序循环")]
    public ActionDataSO[] LightAttacks;

    [Tooltip("重攻击连段（预留）")]
    public ActionDataSO[] HeavyAttacks;

    [Tooltip("蓄力攻击连段（独立 Clip + Charge；对应 GameplayIntentKind.ChargedAttack，由主攻击键长按从 PrimaryAttackPressTracker 派发）")]
    public ActionDataSO[] ChargedAttacks;

    [Header("Skills (预留)")]
    public ActionDataSO SkillA;
    public ActionDataSO SkillB;
}
