using UnityEngine;

/// <summary>
/// 技能静态模板（不可变运行时修改数值；动态修正走 Modifier / Override）。
/// </summary>
[CreateAssetMenu(menuName = "Skill/Skill Data", fileName = "SkillData")]
public class SkillDataSO : ScriptableObject
{
    [Header("Identity")]
    public string skillName;
    public string skillId;
    public Sprite icon;

    [Header("Cast Model")]
    public CastType castType = CastType.Instant;
    public float castTime;
    public float channelDuration;
    public float chargeThreshold;
    public ChargeLevel[] chargeLevels;

    [Header("Stages")]
    [Tooltip(
        "同一 Skill 资产内的多「演出段」索引（段位），由 SkillRuntime.CurrentStageIndex 与二段窗 SecondStageAvailable 驱动；与 comboChain「切到另一张 Skill 资产」是两条独立管线，勿在同一根技能上混合「根上多段位 + comboChain」再加上二段窗，易导致 CD/段位状态难推理。\nCharge 且无 chargeLevels：长按时可把 stages[1] 当作按住段。\n连招链连招请把每段做成单独 SkillDataSO 填入 comboChain，段位仅用于单张 Sheet。")]
    public SkillStageSO[] stages;

    [Header("Cooldown")]
    public CooldownPolicy cdPolicy = CooldownPolicy.OnLastCast;
    public float baseCooldown;
    public int maxCharges = 1;
    public float rechargeTime;

    [Tooltip("若为真：施放不触发 SkillGcd（如闪避/跳跃类独立技能）；其余技能仍可走技能本体 CD。")]
    public bool ignoreGlobalCooldown;

    [Header("Cost")]
    public SkillCost[] costs;

    [Header("Targeting")]
    public TargetType targetType = TargetType.Direction;
    public float maxRange;

    [Header("Traits")]
    public SkillTrait traits;
    public SkillTraitUnlock[] traitUnlocks;

    [Header("Combo")]
    [Tooltip(
        "连招链：按输入顺序切换到「另一张 SkillDataSO」分段资源；每一段仍是独立资产，可走各自 stages。与「不切 Sheet、只在当前 Skill 上用 stages」不同；二段窗口（任务里打开的 SecondStage）只推进当前 Segment 内的 CurrentStageIndex，不会自动沿 comboChain 前进。")]
    public SkillDataSO[] comboChain;
    public float comboResetTime = 1.2f;

    [Header("Progression")]
    public int maxLevel = 1;
    public AnimationCurve damageByLevel;
    public AnimationCurve cooldownByLevel;
    public AnimationCurve costByLevel;
}
