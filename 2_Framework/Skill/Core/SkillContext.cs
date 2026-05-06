using UnityEngine;

/// <summary>
/// 单次技能激活的共享上下文（贯穿 Action 段内 Task）。
/// </summary>
public sealed class SkillContext
{
    public Entity Self;
    public Transform SelfTransform;
    public IStatSet Stats;
    public IResourcePool Resources;
    public GameplayTagContainer Tags;

    /// <summary>装配根技能（CD / 充能 / Combo 状态载体）。</summary>
    public SkillRuntime Runtime;

    /// <summary>连招段或根技能：用于数值曲线（伤害等）。</summary>
    public SkillDataSO ResolvedSkillSheet;

    public SkillStageSO CurrentStage;
    public float StageElapsedTime;
    public float StageNormalizedTime;

    public TargetData Target;
    public Entity MarkedTarget;
    public Vector3 MarkedPosition;

    public SkillModifiers FinalModifiers = SkillModifiers.Identity;

    /// <summary>Phase 5 施法模型：随 <see cref="SkillDataSO.castType"/> 装配；在 Action 内每帧 Tick。</summary>
    public ICastHandler CastHandler;
}

/// <summary>Targeting 结果。</summary>
public struct TargetData
{
    public Entity Entity;
    public Vector3 Position;
    public Vector3 Direction;
    public bool IsValid;
}

/// <summary>技能动态修正（装备 / Buff 合并结果）。</summary>
public struct SkillModifiers
{
    public float RadiusScale;
    public float RadiusAdd;
    public int ProjectileCountAdd;
    public float DamageScale;
    public float DurationScale;

    public static SkillModifiers Identity => new SkillModifiers
    {
        RadiusScale = 1f,
        RadiusAdd = 0f,
        ProjectileCountAdd = 0,
        DamageScale = 1f,
        DurationScale = 1f,
    };
}
