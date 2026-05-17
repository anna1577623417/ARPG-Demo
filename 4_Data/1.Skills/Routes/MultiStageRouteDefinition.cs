using UnityEngine;

/// <summary>
/// 多段路由 — 单条 Route 内部用 stages[] 多段（盲僧 Q1→Q2 范式）。
///
/// ═══ 与 ComboRoute 的区别 ═══
///   · MultiStageRoute: 全段共享 CD/Cost；段间靠 SkillTransition.Conditions（OnHit / OnInput / OnTime）显式衔接。
///   · ComboRoute     : 每段是独立 SubRoute；切的是整条 Route 资产。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Route/MultiStage Route", fileName = "Route_MultiStage_")]
public sealed class MultiStageRouteDefinition : SkillRouteDefinition
{
    [Header("Multi-Stage Behaviour")]
    [SerializeField,
     Tooltip("超过 lastStage 的等待时长（秒）未输入则强制退出 Route。0 = 沿用 ActionData 自然时长。")]
    private float overallTimeoutSeconds;

    [SerializeField,
     Tooltip("被外部打断时是否仍按 cooldownPolicy 结算 CD（false = 中段被打断不进 CD，由策划权衡）。")]
    private bool chargeCdOnExternalInterrupt = true;

    [SerializeField,
     Tooltip("段间模式：AutoInRoute=凯隐Q；PressToAdvance=拉克丝E；HitToAdvance=盲僧Q。")]
    private MultiStageChainMode chainMode = MultiStageChainMode.AutoInRoute;

    [SerializeField,
     Tooltip("PressToAdvance / HitToAdvance：下一段可释放窗口（秒）。超时未按 → 进 CD 并恢复首段 Icon。")]
    private float stageChainArmSeconds = 5f;

    [SerializeField,
     Tooltip("PressToAdvance：何时武装下一段（首段播完 / 锚点落地）。")]
    private MultiStageArmTrigger armNextStageTrigger = MultiStageArmTrigger.OnFirstStageComplete;

    [SerializeField,
     Tooltip("PressToAdvance 且超时未按第二段时，是否按 cooldownPolicy 进 CD（通常 OnRouteEnd 或 OnLastStageEnd）。")]
    private bool startCooldownOnArmTimeout = true;

    public float OverallTimeoutSeconds => overallTimeoutSeconds;
    public bool ChargeCdOnExternalInterrupt => chargeCdOnExternalInterrupt;
    public MultiStageChainMode ChainMode => chainMode;
    public float StageChainArmSeconds => stageChainArmSeconds;
    public MultiStageArmTrigger ArmNextStageTrigger => armNextStageTrigger;
    public bool StartCooldownOnArmTimeout => startCooldownOnArmTimeout;

    public override RouteKind Kind => RouteKind.MultiStage;
}
