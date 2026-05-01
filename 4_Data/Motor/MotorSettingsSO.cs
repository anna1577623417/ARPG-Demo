using UnityEngine;

/// <summary>
/// Kinematic Motor 可调参数的资产化承载。
/// Why: ARPG 中不同体量与机动实体需数据驱动；避免将皮肤厚度、终端速度等写死在 Player 上。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Motor/Motor Settings", fileName = "MotorSettings")]
public sealed class MotorSettingsSO : ScriptableObject
{
    [Header("Geometry")]
    public float Radius = 0.5f;
    public float Height = 2f;

    [Tooltip("Sweep 预留安全距离，过小易穿模，过大易隔空。")]
    [Range(0.001f, 0.05f)]
    public float SkinWidth = 0.015f;

    [Header("Layers")]
    [Tooltip("视作固体障碍物（墙体、静态碰撞体等）参与 CapsuleCast 解算。")]
    public LayerMask ObstacleLayers = ~0;

    [Tooltip("斜坡动能投影：将水平意图投影到此法线定义的切平面上时的最大仰角约束（度）。\n0 禁用斜率投影。\n")] [Range(0f, 90f)]
    public float MaxSlopeAngle = 45f;

    [Tooltip("PhysicsPassThrough 策略下允许的「脚掌—探测命中」空隙上限；过大易空中误判贴地。\n")] [Range(0.05f, 0.55f)]
    public float AirborneGroundContactSlop = 0.14f;

    [Header("Terrain coupling (Grounded pillar)")]
    [Tooltip("满阶梯辅助高度（下一阶段 StepSolver 接线）。当前版本预留，不参与求解。")] [Range(0f, 1f)]
    public float StepOffset = 0.3f;

    [Tooltip("下坡/高速运动时允许向下吸附以保持贴地的最大落空距离（米）。\n仅在 FullTerrainCoupling 生效。")] [Range(0f, 2f)]
    public float GroundSnapDistance = 0.4f;

    [Tooltip("斜坡动能保持：若为真且在地面且检测到坡面法线，将水平位移矢量投影至坡切平面。")]
    public bool ProjectHorizontalOntoSlope = true;

    [Header("Teleport")]
    public float TeleportRayStartAbove = 2f;
    [Tooltip("<= 0：瞬移不进行向下地形射线修正。")] [Range(0f, 48f)]
    public float TeleportRayMaxDistance = 12f;

    [Header("Kinematic caps")]
    [Tooltip("下落终端速度上限，防止 Δt·v 单帧击穿薄碰撞。")] [Range(10f, 120f)]
    public float TerminalVelocity = 50f;

    [Range(1, 8)] public int MaxSubSteps = 3;

    [Tooltip("单次子步内 Collide-and-Slide 的最大折射次数（含接缝滑出迭代）。")] [Range(1, 6)]
    public int MaxSlideIterations = 4;

    [Tooltip("锐角接缝：两法线点积小于该阈值时改用 cross(n0,n1) 滑出轴线。")] [Range(-1f, 0f)]
    public float CreviceNormalDotThreshold = -0.2f;

    [Header("Sweep — convex / internal edge")]
    [Tooltip(
        "位移方向·命中法线 点积大于该值视为「非阻挡」（凸角接缝、背向噪声、擦肩而过），\n本段剩余位移一次性放行并结束本子步折射，减轻钝角抖动与过矫正死锁。")] [Range(-0.05f, 0.25f)]
    public float VelocityAgainstNormalRejectDot = 0.01f;

    [Header("Debug (Scene / Game)")]
    [Tooltip("在 Scene 视图绘制 Sweep 命中点、法线与滑动方向。")]
    public bool DrawSweepHitsInSceneView;

    [Tooltip("Debug 线存活秒数；0 = 仅当前帧。")] [Range(0f, 4f)]
    public float SweepHitDebugLifetime = 0.35f;

    [Tooltip("命中障碍物时触发 CollisionDebugFlasher（需物件上已有组件，或开启下方 AutoAdd）。")]
    public bool FlashObstacleColliderOnSweepHit;

    [Tooltip("测试用：首次命中时自动挂上 CollisionDebugFlasher；正式关卡请关。")]
    public bool AutoAddCollisionFlasherOnObstacleIfMissing;

    public Color SweepHitPointColor = new Color(1f, 0.15f, 0.1f, 1f);
    public Color SweepIgnoredHitColor = new Color(1f, 0.92f, 0.2f, 1f);
    public Color SweepHitNormalColor = new Color(0.2f, 0.95f, 0.25f, 1f);
    public Color SweepSlideDirectionColor = new Color(0.2f, 0.75f, 1f, 1f);

    [Header("Soft rigidity (future)")]
    public float MotorMassWeight = 1f;
    public float RepulsionStrength = 5f;

    /// <summary>胶囊总高不得小于 2×半径的工程约束提示（运行时仍做 Clamp）。</summary>
    public float EffectiveHeight => Mathf.Max(Height, Radius * 2f + 0.02f);

    /// <summary>沿坡投影是否启用。</summary>
    public bool EnablesSlopeProjection => ProjectHorizontalOntoSlope && MaxSlopeAngle > 0.5f;

    /// <summary>斜坡是否过陡（视作墙）：用于地面切平面投影裁剪。</summary>
    public bool IsSlopeTooSteep(in Vector3 groundNormal)
    {
        var n = groundNormal.normalized;
        return n.y <= 0.001f || Vector3.Angle(Vector3.up, n) > MaxSlopeAngle + 0.01f;
    }
}
