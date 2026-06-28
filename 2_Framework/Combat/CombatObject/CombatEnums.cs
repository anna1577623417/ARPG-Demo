/// <summary>188.3 — CombatObject 子系统共用枚举。</summary>

/// <summary>Movement 运动模式（188.3 §4.2）。</summary>
public enum MovementKind : byte
{
    /// <summary>原地不动（近战扇形、火焰地板、原地 AOE）。</summary>
    Static = 0,

    /// <summary>直线匀速（飞弹、滑刀、横扫）。</summary>
    Linear = 1,

    /// <summary>三轴 AnimationCurve 控制偏移（弧线、S 形）—— 留待 P2 W11。</summary>
    Curve = 2,

    /// <summary>半径随时间扩张（冲击波、扩散圈）—— 留待 P2 W12。</summary>
    Expand = 3,

    /// <summary>每帧朝目标转向（跟踪导弹、火球术）—— 留待 P2 W13。</summary>
    Homing = 4,
}

/// <summary>CombatObject 生成位置来源（188.3 §3.2）。</summary>
public enum SpawnSource : byte
{
    /// <summary>施法者根骨（默认）。</summary>
    SelfRootBone = 0,

    /// <summary>施法者右手骨骼。</summary>
    SelfHandR = 1,

    /// <summary>施法者左手骨骼。</summary>
    SelfHandL = 2,

    /// <summary>施法者脚下地面（射线检测取地面 Y）。</summary>
    GroundUnderSelf = 3,

    /// <summary>当前锁定目标脚下地面。</summary>
    GroundUnderTarget = 4,

    /// <summary>相机前方某距离的世界点（用于 RTS 风格 AOE）。</summary>
    WorldFromCamera = 5,

    /// <summary>OnExpire 子 CombatObject 用：上一个 CombatObject 销毁位置。</summary>
    AtSelfPosition = 6,
}

/// <summary>Damage 效果种类（188.3 §4.3）。</summary>
public enum DamageKind : byte
{
    /// <summary>瞬时伤害（走 DamagePipeline 4 Stage）。</summary>
    Instant = 0,

    /// <summary>瞬时伤害 + 命中后挂 EffectDefinitionSO（180.1 Effect 子系统）。</summary>
    InstantPlusEffect = 1,

    /// <summary>治疗（DamagePipeline 反向）。</summary>
    Heal = 2,

    /// <summary>击退（Motor 推力）—— 留待 P2 W14。</summary>
    Knockback = 3,

    /// <summary>升龙（向上推力）—— 留待 P2 W14。</summary>
    Launch = 4,
}
