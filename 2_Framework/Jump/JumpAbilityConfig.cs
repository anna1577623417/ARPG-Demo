/// <summary>
/// 175.2 W0 — Jump 运行时有效配置（副本，可被 Modifier 链改写）。
/// <para>BaseConfig 来自 <c>JumpAbilityConfigSO</c>；策划设的基线 SO 永远只读。</para>
/// <para>JumpModifierStack.Recompute 把所有叠层 Modifier 应用到本结构。</para>
/// </summary>
public struct JumpAbilityConfig
{
    // 数值字段（顺序须与 JumpField 枚举对齐，以便 Indexer 使用）
    public float JumpForce;
    public float MaxJumpCount;
    public float CoyoteTimeSec;
    public float JumpBufferSec;
    public float ApexGravityScale;
    public float FallGravityScale;
    public float AirMoveMultiplier;
    public float HorizontalBoost;
    public float ExtraHangTimeSec;
    public float VariableHeightMaxHoldSec;
    public float GroundPoundShockwaveRadius;

    // 能力位（Modifier 可累加）
    public bool EnableGlide;
    public bool EnableDashJump;
    public float DashJumpHorizontalBonus;

    /// <summary>175.2 §3.1 字段索引；Recompute 链按 JumpField 枚举一次性遍历改写。</summary>
    public float this[JumpField field]
    {
        get => field switch
        {
            JumpField.JumpForce                    => JumpForce,
            JumpField.MaxJumpCount                 => MaxJumpCount,
            JumpField.CoyoteTimeSec                => CoyoteTimeSec,
            JumpField.JumpBufferSec                => JumpBufferSec,
            JumpField.ApexGravityScale             => ApexGravityScale,
            JumpField.FallGravityScale             => FallGravityScale,
            JumpField.AirMoveMultiplier            => AirMoveMultiplier,
            JumpField.HorizontalBoost              => HorizontalBoost,
            JumpField.ExtraHangTimeSec             => ExtraHangTimeSec,
            JumpField.VariableHeightMaxHoldSec     => VariableHeightMaxHoldSec,
            JumpField.GroundPoundShockwaveRadius   => GroundPoundShockwaveRadius,
            _ => 0f,
        };
        set
        {
            switch (field)
            {
                case JumpField.JumpForce:                  JumpForce = value; break;
                case JumpField.MaxJumpCount:               MaxJumpCount = value; break;
                case JumpField.CoyoteTimeSec:              CoyoteTimeSec = value; break;
                case JumpField.JumpBufferSec:              JumpBufferSec = value; break;
                case JumpField.ApexGravityScale:           ApexGravityScale = value; break;
                case JumpField.FallGravityScale:           FallGravityScale = value; break;
                case JumpField.AirMoveMultiplier:          AirMoveMultiplier = value; break;
                case JumpField.HorizontalBoost:            HorizontalBoost = value; break;
                case JumpField.ExtraHangTimeSec:           ExtraHangTimeSec = value; break;
                case JumpField.VariableHeightMaxHoldSec:   VariableHeightMaxHoldSec = value; break;
                case JumpField.GroundPoundShockwaveRadius: GroundPoundShockwaveRadius = value; break;
            }
        }
    }

    /// <summary>项目默认值：与 LocomotionTuningSO 现有 JumpForce/FallGravityScale 对齐。</summary>
    public static JumpAbilityConfig Default => new()
    {
        JumpForce = 12f,
        MaxJumpCount = 1f,
        CoyoteTimeSec = 0.10f,
        JumpBufferSec = 0.15f,
        ApexGravityScale = 0.45f,
        FallGravityScale = 1.6f,
        AirMoveMultiplier = 0.6f,
        HorizontalBoost = 0f,
        ExtraHangTimeSec = 0f,
        VariableHeightMaxHoldSec = 0.20f,
        GroundPoundShockwaveRadius = 3f,
        EnableGlide = false,
        EnableDashJump = false,
        DashJumpHorizontalBonus = 0f,
    };
}
