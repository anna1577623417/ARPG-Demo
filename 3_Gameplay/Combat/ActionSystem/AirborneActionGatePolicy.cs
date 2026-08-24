/// <summary>
/// 242.2：物理空中时的 Player ActionState 技能门禁路由策略。
///
/// L1 仍由 AirInterruptResolver 裁决，L2 仍由 AbilityGateService 裁决。
/// 本类只定义当前 Action 是否是“空中 Locomotion 表现载体”，因此不应再以 ActionWindow
/// 充当第三道技能门禁。
/// </summary>
public static class AirborneActionGatePolicy
{
    /// <summary>物理空中 Skill Intent 不因当前 FSM 已进入 ActionState 而丢失角色空中画像 L1。</summary>
    public static bool RequiresAirInterruptGate(bool isGrounded) => !isGrounded;

    /// <summary>
    /// 无 ActiveRoute 的 Locomotion Action（例如 Profile JumpStart）在空中只是表现/运动载体。
    /// 它不拥有技能中断窗口；来袭技能由 L1 + L2 决定能否重入新 Action。
    /// </summary>
    public static bool IsAirborneLocomotionPresentationCarrier(
        bool isGrounded,
        bool isLocomotionOnlyAction,
        ActionIntentCategory currentIntentCategory)
    {
        return !isGrounded
               && isLocomotionOnlyAction
               && currentIntentCategory == ActionIntentCategory.Locomotion;
    }
}
