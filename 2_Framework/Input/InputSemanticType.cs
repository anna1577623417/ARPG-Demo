/// <summary>
/// 输入语义类型（Ver4.3.7+） — 由 <see cref="InputSemanticResolver"/> 单一产生、写入 <see cref="GameplayIntent.Semantic"/>。
///
/// ═══ 设计约束（来自《112.1 重构》中 Input Semantic Resolver 章）═══
///   1. 技能层（SkillRuntime / StageRuntime / Stage）禁止解析 hold 时间或 raw 输入；
///   2. Tap / Hold / DoubleTap / Charge 等分流必须在 InputSemanticResolver 完成；
///   3. Intent 一旦发出不可在技能运行中被重新解释；
///   4. Intent 与具体技能解耦——只描述"玩家想做哪类抽象事"。
///
/// ═══ 最小可用集（Phase A） ═══
///   Tap / Charge / Combo 三态覆盖当前 LM 三路由切分；
///   Directional 给方向闪避（Phase E）；其它为预留位，按需在 Resolver 内启用。
/// </summary>
public enum InputSemanticType : byte
{
    /// <summary>未指定 / 兼容旧调用路径。</summary>
    None = 0,

    /// <summary>短按松手（hold &lt; TapThreshold） — 通常走 Combo 首段或 Normal。</summary>
    Tap = 1,

    /// <summary>长按越过阈值（按下即派发，不等松手） — 进入 ChargeRoute 起手，动画到 HoldAnchor 自然冻结。</summary>
    Charge = 2,

    /// <summary>Combo 窗口内的连续 Tap — 携带 <see cref="GameplayIntent.ComboIndex"/>。</summary>
    Combo = 3,

    /// <summary>仍处于按住期间的持续帧信号（预留：Channel / 持续蓄力增量）。当前不入队。</summary>
    Hold = 4,

    /// <summary>显式松手 raw 事件 — Resolver 用于通知 Runtime 解冻；不携技能分流语义。</summary>
    Release = 5,

    /// <summary>双击 — 预留，由 Resolver 维护双击窗。</summary>
    DoubleTap = 6,

    /// <summary>方向输入（WSAD + Trigger 组合，如 Space 翻滚） — <see cref="GameplayIntent.DirectionAxis"/> 携方向。</summary>
    Directional = 7,

    /// <summary>组合键（Shift + LMB 等） — 预留。</summary>
    Chord = 8,
}
