/// <summary>
/// 237 L5 — Action 期间 CommittedFacing 策略。SkillGroup 只选槽，不决定角色是否转身。
/// TrackTarget 本版枚举存在，不接 LockOn 矩阵。
/// </summary>
public enum ActionFacingPolicy : byte
{
    /// <summary>位移可侧向，Committed 保持进入朝向。八向 Dodge / Slide 默认。</summary>
    PreserveEntryFacing = 0,
    /// <summary>进入时提交到本次位移方向。冲锋类后续。</summary>
    FaceMotionAtEntry = 1,
    /// <summary>面向锁定目标。本版按 PreserveEntry 处理并打 OPEN。</summary>
    TrackTarget = 2
}
