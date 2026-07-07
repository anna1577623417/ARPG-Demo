/// <summary>
/// HUD 只读句柄 — RouteWidget 与 SkillBarPresenter 唯一可见的运行时面。
///
/// ═══ 设计契约（与 Ver4.3.6 蓝图契约 C4 对齐）═══
///   · HUD 禁止直接持有 SkillRouteRuntime / SkillRouteDefinition；只通过本接口读运行时面。
///   · 本接口仅 getter；HUD 不可反向写运行时状态。
///   · 实现方（SkillRouteRuntime）负责保证所有进度量单调 0→1，HUD 直接写 fillAmount。
/// </summary>
public interface IRouteRuntimeHandle
{
    /// <summary>所属物理入口槽位（HUD 分组用；不暴露 PresentationKind 以保持 Route 多态对 HUD 透明）。</summary>
    SkillEntrySlot EntrySlot { get; }

    /// <summary>HUD 显示图标（取 SkillRouteDefinition.Icon）。</summary>
    UnityEngine.Sprite Icon { get; }

    /// <summary>HUD 显示名（取 SkillRouteDefinition.DisplayName）。</summary>
    string DisplayName { get; }

    /// <summary>HUD 键位标签（取 Loadout.ResolveKeyLabel）。</summary>
    string KeyLabel { get; }

    /// <summary>是否在 HUD 显示。</summary>
    bool ShowOnHud { get; }

    /// <summary>冷却进度 0..1 — 1 = 已就绪、0 = 刚释放。HUD: fillAmount = 1 - CdProgress01。</summary>
    float CdProgress01 { get; }

    /// <summary>当前 CD 剩余秒数；可用于 HUD 文字 ("3" / "0.5")。</summary>
    float CdRemainingSeconds { get; }

    /// <summary>充能数（多充能技能）；不支持时返回 -1。</summary>
    int CurrentCharges { get; }
    int MaxCharges { get; }

    /// <summary>本 Route 是否为 ChargeRoute（HUD 决定是否渲染 ChargeBar）。</summary>
    bool HasChargeBar { get; }

    /// <summary>0..1；非 ChargeRoute 返回 0。</summary>
    float ChargeProgress01 { get; }

    /// <summary>本 Route 是否为 ComboRoute；HUD 决定是否渲染 Combo 计数。</summary>
    bool HasComboOverlay { get; }

    /// <summary>当前 Combo 段位（0 = 第一段，-1 = 未启用）。</summary>
    int ComboStep { get; }

    /// <summary>Combo 窗剩余时间（秒）；HUD 用于绘 Combo 渐隐圈。</summary>
    float ComboWindowRemainingSeconds { get; }

    /// <summary>本 Route 是否为 MultiStageRoute；HUD 决定是否渲染段位指示。</summary>
    bool HasMultiStageOverlay { get; }

    /// <summary>当前 MultiStage 段位（0 = Stage1）；非 MultiStage 返回 -1。</summary>
    int MultiStageIndex { get; }

    /// <summary>当前 Stage 进度 0..1（用于 HUD 段位条）。</summary>
    float CurrentStageProgress01 { get; }

    /// <summary>当前 Stage 已开的 Transition 剩余窗口秒数（玩家"现在就要按下一段"的提示）；无则 0。</summary>
    float ActiveTransitionWindowRemainingSeconds { get; }

    /// <summary>Route 是否可被释放（CD + 资源 + 条件门；HUD 高光边框与最终可点状态）。</summary>
    bool CanCastNow { get; }

    /// <summary>是否处于 Route 冷却中（HUD：优先显示 CD 遮罩，不叠资源不足罩）。</summary>
    bool IsOnCooldown { get; }

    /// <summary>当前资源是否满足 Route.costs（不含 CD；HUD 资源不足遮罩用）。</summary>
    bool HasEnoughResources { get; }

    /// <summary>
    /// 非 CD 原因抑制高光边框（资源不足 / 释放条件未满足等）。
    /// 为 true 时 HUD 隐藏高光；CD 进度仍只驱动 CD 遮罩与（未抑制时的）高光 fill。
    /// </summary>
    bool IsHighlightSuppressed { get; }

    /// <summary>213 — 稳定展示 ID（Validate Warning；Play 不依赖）。</summary>
    string PresentationId { get; }

    /// <summary>HUD 诊断 — RouteId / GroupId（未配时 = 资产名）。</summary>
    string UnitId { get; }

    /// <summary>HUD 诊断 — Route | Group。</summary>
    string HudUnitKind { get; }

    /// <summary>HUD 诊断 — 资产所在目录（Editor Play 为 Assets/... 路径）。</summary>
    string AssetFolder { get; }

    /// <summary>HUD 诊断 — 资产完整路径（Editor Play）。</summary>
    string AssetPath { get; }

    /// <summary>213 — Stage/段切换时递增；Widget 比较后 RefreshPresentation。</summary>
    int PresentationRevision { get; }

    /// <summary>213/214 — Loadout layout 或 slot_subIndex。</summary>
    string HudIdentity { get; }

    /// <summary>215 — CD/Charge/Combo/MultiStage 合成展示态。</summary>
    SkillPresentationState PresentationState { get; }

    /// <summary>215 — 主角标（段号 / Combo 段）。</summary>
    PresentationBadge PrimaryBadge { get; }

    /// <summary>215 — 副角标（预留）。</summary>
    PresentationBadge SecondaryBadge { get; }
}
