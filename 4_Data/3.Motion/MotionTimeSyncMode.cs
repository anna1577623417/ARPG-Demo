/// <summary>
/// Motion 与 Clip 墙钟对齐策略（翻滚/冲刺/终结技通用）。
/// </summary>
public enum MotionTimeSyncMode : byte
{
    /// <summary>不对齐：Logic Duration 与 Action.AnimSpeed 各走各的。</summary>
    None = 0,

    /// <summary>匹配动画：保持 Clip 墙钟；拉伸 Logic/Motion 时长至 clip.length/AnimSpeed。</summary>
    MatchAnimation = 1,

    /// <summary>匹配运动：保持 Logic Duration；动画倍率 = clipWall/logicDur。</summary>
    MatchMotion = 2,
}
