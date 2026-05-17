using System;
using UnityEngine;

/// <summary>
/// 路由优先级 — RouteResolver 在 SkillEntry 中选谁出来。
/// 数值越小优先级越高（资源消耗 / 数组扫描顺序友好）。
/// </summary>
public enum RoutePriority : byte
{
    Charge      = 0,   // 蓄力释放优先 — 玩家正在按住时一律走 Charge
    Combo       = 1,   // Combo 窗口开启时走 Combo
    Directional = 2,   // 方向化（WASD + Trigger）
    Derivative  = 3,   // 派生（从父 Route 派生出来的衍生招）
    MultiStage  = 4,   // 多段（同 Sheet 内部 Q1→Q2→Q3）
    Normal      = 5,   // 默认普攻
}

/// <summary>
/// 蓄力 Clip 编排模式（仅入口语义层用；动作语义层不知此枚举）。
/// </summary>
public enum ChargePresentationMode : byte
{
    /// <summary>
    /// 单 Clip：前摇/Hold/释放打包同一 Action，靠 MotionPlaybackContext.RuntimeSpeedMultiplier 实现"蓄满压速"。
    /// </summary>
    SingleClip = 0,

    /// <summary>
    /// 多 Clip：Startup / HoldLoop / Release / Cancel 四个独立 Stage，靠 MotionPlaybackContext.LoopWindow 循环。
    /// </summary>
    MultiClip = 1,
}

/// <summary>
/// Stage 之间的衔接触发类型（Transition Trigger Kind）。
/// </summary>
public enum StageTransitionTrigger : byte
{
    /// <summary>归一化时间到达 OpenTime 自动衔接（无条件后摇 / 自动收尾）。</summary>
    Auto       = 0,

    /// <summary>玩家再次输入指定槽位（连段）。</summary>
    OnInput    = 1,

    /// <summary>本 Stage 期间产生至少一次命中（盲僧 Q1→Q2）。</summary>
    OnHit      = 2,

    /// <summary>独立计时器到时（如蓄力满 chargeFullTime 自动切 HoldLoop）。</summary>
    OnTimer    = 3,

    /// <summary>输入释放（Charge 松手切 Release）。</summary>
    OnRelease  = 4,
}

/// <summary>
/// Transition 命中目标过滤（与 OnHit 触发联动）。
/// </summary>
public enum TransitionTargetRule : byte
{
    Any        = 0,
    HeroOnly   = 1,
    NonMinion  = 2,
    Boss       = 3,
    Minion     = 4,
}

/// <summary>
/// 方向化路由 8 方向枚举（仅在 InputChordResolver 中使用）。
/// </summary>
public enum DirectionalRouteType : byte
{
    Forward       = 0,
    Backward      = 1,
    Left          = 2,
    Right         = 3,
    ForwardLeft   = 4,
    ForwardRight  = 5,
    BackwardLeft  = 6,
    BackwardRight = 7,
}

/// <summary>
/// 资源消耗时机（Route-level）。与 <see cref="RouteCooldownPolicy"/> 解耦，可独立配置。
/// </summary>
public enum RouteResourceConsumePolicy : byte
{
    /// <summary>进入 Route 时扣费（默认；与旧行为一致）。</summary>
    OnRouteStart = 0,

    /// <summary>首段 Stage 开始时扣费（OnEnter 进入 Stage[0] 后）。</summary>
    OnFirstStageStart = 1,

    /// <summary>末段 Stage 自然完成时扣费（最后一段 Stage.Completed，Route 尚未 Exit）。</summary>
    OnLastStageEnd = 2,

    /// <summary>整条 Route 退出时扣费（OnExit 内，与 CD OnRouteEnd 同帧序）。</summary>
    OnRouteEnd = 3,

    /// <summary>Combo 容器：首条 SubRoute 进入 Session 时扣费（替代 OnRouteStart）。</summary>
    [InspectorName("On First SubRoute Start")]
    OnFirstSubRouteStart = 4,

    /// <summary>Combo 容器：末条 SubRoute 完成后扣费（替代 OnRouteEnd / OnLastStageEnd）。</summary>
    [InspectorName("On Last SubRoute End")]
    OnLastSubRouteEnd = 5,
}

/// <summary>
/// 冷却启动策略（Route-level）。与旧 SkillData.cdPolicy 三档对齐。
/// </summary>
public enum RouteCooldownPolicy : byte
{
    /// <summary>进入 Route 即扣 CD（旧：OnCast / 起手进 CD）。</summary>
    OnRouteStart = 0,

    /// <summary>整条 Route 完全结束后扣 CD（旧：整条技能结束）。</summary>
    OnRouteEnd = 1,

    /// <summary>最后一段 Stage 播完后扣 CD（旧：最后一段释放后）。</summary>
    OnLastStageEnd = 2,

    /// <summary>第一段 Stage 结束后扣 CD（旧：第一段释放后）；第二段仍可进窗（拉克丝 E 引爆段）。</summary>
    OnFirstStageEnd = 3,

    /// <summary>Combo 容器：首条 SubRoute 起手进 CD（少用；多数连招在末段结算）。</summary>
    [InspectorName("On First SubRoute Start")]
    OnFirstSubRouteStart = 4,

    /// <summary>Combo 容器：末条 SubRoute 完成后进 CD（推荐；整套连招一次 CD）。</summary>
    [InspectorName("On Last SubRoute End")]
    OnLastSubRouteEnd = 5,
}

/// <summary>
/// MultiStage 段间衔接模式（同一条 Route 内多 Stage）。
/// </summary>
public enum MultiStageChainMode : byte
{
    /// <summary>凯隐 Q：单次按键，Stage0 播完 Auto 衔接 Stage1（同一次 Route 会话）。</summary>
    AutoInRoute = 0,

    /// <summary>拉克丝 E：两段各按一次；Stage0 结束后武装下一段，超时未按则进 CD。</summary>
    PressToAdvance = 1,

    /// <summary>盲僧 Q：Stage0 命中英雄后，跨次按同槽进 Stage1。</summary>
    HitToAdvance = 2,
}

/// <summary>PressToAdvance 时，何时允许显示/进入第二段（HUD 换 Icon + Pending）。</summary>
public enum MultiStageArmTrigger : byte
{
    /// <summary>第一段动作播完即武装（拉克丝：扔出后即可引爆）。</summary>
    OnFirstStageComplete = 0,

    /// <summary>锚点落地后武装（拉克丝：投掷物到达区域后才可引爆）。</summary>
    OnSkillAnchorReady = 1,
}

/// <summary>
/// 蓄力被取消（超时 / 外部打断 / 死亡）时 CD 返还策略。
/// </summary>
public enum ChargeCancelRefundMode : byte
{
    None          = 0,   // 不返还（按完整 CD 结算）
    ReturnPercent = 1,   // finalCd = baseCd * (1 - returnValue)
    ReturnFixed   = 2,   // finalCd = max(0, baseCd - returnValue)
    SkipCooldown  = 3,   // 取消即视为技能未发生
}

/// <summary>
/// 蓄力到达 maxHoldTime 后的处理策略。
/// </summary>
public enum ChargeExpiryPolicy : byte
{
    Cancel       = 0,   // 取消（按 ChargeCancelRefundMode 结算）
    ForceRelease = 1,   // 自动 Release（等同松手）
}

/// <summary>
/// 派生 Route 是否独占父 Route 的 CD（共享 / 独立 CD Group 的简化版）。
/// </summary>
[Flags]
public enum DerivativeFlags : byte
{
    None              = 0,
    ShareParentCd     = 1 << 0,  // 派生触发时不另起 CD（父 CD 已扣）
    InheritResource   = 1 << 1,  // 派生不另扣资源
    ConsumeOnlyOnHit  = 1 << 2,  // 仅命中时扣 CD/资源
}
