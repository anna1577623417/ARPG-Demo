/// <summary>
/// MotionProfile 动画速率的合成模式（v4.5）。
///
/// ═══ 三层组合公式 ═══
///
///   finalClipSpeed = ActionData.AnimSpeed × profileFactor
///
///   ActionData.AnimSpeed       基础倍率（恒定，全程不变；用于"重击 0.85x / 轻击 1.1x" 等手感调校）
///   profileFactor              由本枚举决定，逐帧采样
///
/// ═══ 三种模式职责 ═══
///
///   Constant     → profileFactor = 1.0
///                  仅用 ActionData.AnimSpeed，MotionProfile 不干预动画速率。
///                  适用：站桩攻击、不需要节奏曲线/步幅匹配的场景（默认）。
///
///   Curve        → profileFactor = SpeedOverTime.Evaluate(t)
///                  编排化时间曲线（慢起手 → 快出招 → 收招减速）。
///                  适用：戏剧化大招、蓄力释放、有明显节奏对比的招式。
///
///   StrideMatch  → profileFactor = clamp(actualSpeed / ReferenceSpeed, 0.7, 1.3)
///                  步幅匹配：用马达实际平面速度 / 该 Clip 1× 的参考速度作为播放倍率。
///                  适用：跑步、翻滚、突进等"位移驱动"动作；防滑步。
///
/// ═══ 与旧 MatchAnimationSpeed 的对应 ═══
///
///   旧 MatchAnimationSpeed = true  → 等价于 StrideMatch
///   旧 MatchAnimationSpeed = false → 等价于 Curve
///   新增 Constant 模式：让 ActionData.AnimSpeed 单独生效（旧架构无法表达）。
/// </summary>
public enum AnimSpeedMode : byte
{
    Constant = 0,
    Curve = 1,
    StrideMatch = 2,
}
