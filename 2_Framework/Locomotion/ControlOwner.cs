/// <summary>
/// 角色当前位移/朝向控制权归属（158.2 §6.2）—— 仅作为可观测枚举。
///
/// ═══ 设计契约 ═══
///   · 单值（非 Flags）—— 控制权互斥，同一时刻只能归属一方。
///   · <strong>不参与裁决</strong> —— 当前已通过 4 支柱切换实现隐式互斥，
///     引入显式 Owner 参与裁决会产生"状态切换说一套、Owner 说一套"的双轨真相。
///     本枚举仅供 Debug HUD / 日志 / Profiler 标记使用。
///   · 由 <see cref="PlayerStateManager.OnPreLogicUpdate"/> 末尾根据当前支柱自动写入；
///     调用方禁止手工赋值。
///
/// ═══ 支柱 → Owner 映射 ═══
///   · PlayerLocomotionState / PlayerAirborneState → <see cref="Locomotion"/>
///   · PlayerActionState  → <see cref="Action"/>
///   · PlayerDeadState    → <see cref="Locomotion"/>（无人主动驱动，按"基线"归属）
///   · 未来 CutsceneState  → <see cref="Cutscene"/>（占位）
/// </summary>
public enum ControlOwner : byte
{
    Locomotion = 0,
    Action     = 1,
    Cutscene   = 2,
}
