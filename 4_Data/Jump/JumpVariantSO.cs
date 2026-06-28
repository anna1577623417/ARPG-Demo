using System;
using UnityEngine;

/// <summary>
/// 175.2 W1 — 单条跳跃玩法定义（识别条件 + 数值 Override + 资产引用）。
/// 同一份 JumpAbility Runtime 上，N 个 Variant 由 Resolver 优先级匹配。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Jump/Jump Variant", fileName = "JumpVariant_")]
public sealed class JumpVariantSO : ScriptableObject
{
    [Header("Variant 识别")]
    public JumpVariantId Id = JumpVariantId.Normal;

    [Tooltip("Inspector / HUD 显示名。")]
    public string DisplayName = "Normal Jump";

    [Tooltip("优先级（数值越小越先匹配；策划用于覆盖兜底）。")]
    public int Priority = 100;

    [Header("识别条件")]
    public JumpTriggerCondition Condition;

    [Header("基础配置（W1 默认值）")]
    public JumpAbilityConfigSO BaseConfig;

    [Header("Variant 特有 Override")]
    [Tooltip("水平冲量加成（LongJump=8）。")]
    public float HorizontalBoost = 0f;

    [Tooltip("起跳冲量乘子（SideFlip=1.4 / Triple=1.8 / Dive=-0.4）。")]
    public float UpwardMul = 1f;

    [Tooltip("反向冲量（BackFlip）；XYZ 局部，相对角色起手朝向。")]
    public Vector3 BackwardImpulse;

    [Tooltip("起手 Yaw 自旋（度）：SideFlip=180 / BackFlip=180。")]
    public float YawSpin = 0f;

    [Tooltip("是否锁定水平方向（GroundPound 期间禁止移动输入）。")]
    public bool LockHorizontalDir = false;

    [Header("Action 资产")]
    public ActionDataSO TakeOffAction;
    public ActionDataSO RiseAction;
    public ActionDataSO FallAction;

    [Header("Capability Tags")]
    public GameplayTagMask CapabilityTags;
}

/// <summary>175.2 W1 — Variant 识别条件，由 <see cref="JumpVariantResolver"/> 按优先级匹配。</summary>
[Serializable]
public struct JumpTriggerCondition
{
    [Tooltip("是否要求接地。-1=不要求 / 0=必须空中 / 1=必须接地。")]
    public sbyte RequireGroundedTri;

    [Tooltip("是否要求 Sprint 键持续按下。")]
    public bool RequireSprintHeld;

    [Tooltip("是否要求 Crouch 键持续按下。")]
    public bool RequireCrouchHeld;

    [Tooltip("是否要求最近 N 秒内有 180° 反向输入（Backflip）。0=不要求。")]
    public float RequirePivotWithinSec;

    [Tooltip("是否要求触墙（WallJump）。")]
    public bool RequireWallContact;

    [Tooltip("是否要求按下方向键（GroundPound）。")]
    public bool RequireDownInput;

    [Tooltip("是否要求按攻击键（Dive）。")]
    public bool RequireAttackPressed;

    [Tooltip("是否要求按 Spin 键（Spin Jump）。")]
    public bool RequireSpinPressed;

    [Tooltip("最小水平速度阈值（Run/LongJump 用）。&lt;0 = 不检查。")]
    public float MinHorizontalSpeed;

    [Tooltip("最大水平速度阈值；&lt;0 = 不检查。")]
    public float MaxHorizontalSpeed;

    [Tooltip("要求落地连按段数（Triple Jump = 2）；-1 = 不要求。")]
    public int RequireComboIndex;

    [Tooltip("落地后 N 秒内仍允许 Combo（落地越界 Combo 重置）。0 = 不要求。")]
    public float RequireLandWithinSec;

    [Tooltip("要求剩余空中跳次数 ≥ N（DoubleAir/TripleAir）；-1 = 不要求。")]
    public int RequireRemainingJumpsAtLeast;

    /// <summary>"什么都不要求"的兜底条件 —— 用作 Normal Variant。</summary>
    public static JumpTriggerCondition Any => new()
    {
        RequireGroundedTri = -1,
        RequirePivotWithinSec = 0f,
        MinHorizontalSpeed = -1f,
        MaxHorizontalSpeed = -1f,
        RequireComboIndex = -1,
        RequireLandWithinSec = 0f,
        RequireRemainingJumpsAtLeast = -1,
    };
}
