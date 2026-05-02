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

    [Header("Low step — lower-hemisphere normal filter")]
    [Tooltip(
        "底半球「侧向」刮到低台阶直角时，PhysX 法线常带向上的寄生分量导致 Capsule Riding。\n在命中点相对底球球心存在明显水平偏移且法线被判为不可行走时，剥离法线 Y 并重归一化，当作竖直挡板滑动。")]
    public bool EnableLowerHemisphereNormalFilter = true;

    [Tooltip(
        "仅当命中点 Y 不高于底球球心+Slop 时做法线展平（WA/WD 斜向易误触「南极盲区」；平底法线向上则不会判为陡坡）。\n关闭则任意高度命中在陡坡时均展平（易误伤圆柱中段与坡面）。")]
    public bool LowerHemisphereOnlyWhenContactBelowBottomCenter = true;

    [Tooltip("与底球球心 Y 比较的容差（米）。")] [Range(0f, 0.08f)]
    public float LowerHemisphereBottomCenterYSlop = 0.02f;

    [Tooltip(
        "遗留：XZ「南极」半径盲区。0=完全关闭（推荐，改以法线陡度+底心高度判定）。\n>0 时在该半径内且未触发展平时保留原法线。")] [Range(0f, 0.35f)]
    public float LowerHemispherePoleRadiusFraction = 0f;

    [Tooltip(
        "0=禁用。>0 时：侧向刮蹭且法线 Y 在 (0,该值) 内也强制摊平，用于「斜向骑台阶」仍被判为可行走坡面的极端情况。\n可能误伤极缓坡侧缘，默认关闭；若仍抖动可试 0.88~0.94。")] [Range(0f, 0.99f)]
    public float LowerHemisphereFlattenIfGroundNormalYBelow = 0f;

    [Header("Ground stabilization (locomotion)")]
    [Tooltip("主 SphereCast 未命中但上一帧接地且本帧无向上初速时，追加一次更长下探，减轻低台阶蹭起后的单帧悬空与 IsGrounded 抖动。")]
    public bool EnableExtraGroundStabilizationProbe = true;

    [Tooltip("追加下探在基础 castDistance 上额外延伸的米数。")] [Range(0f, 1.5f)]
    public float ExtraGroundProbeExtension = 0.35f;

    [Tooltip("允许执行追加下探时垂直速度上限（向上速度大于此值视为跳跃/受击，不强行贴地）。")] [Range(0f, 5f)]
    public float GroundSnapMaxUpwardVelocity = 0.08f;

    [Header("Ground snap — overlap guard")]
    [Tooltip("硬吸附 Y 前用 OverlapCapsuleNonAlloc 检测目标 Pivot 处是否与静态几何重叠；重叠则放弃本帧吸附，避免斜向卡边时被拽入网格。")]
    public bool GroundSnapOverlapGuard = true;

    [Tooltip("重叠查询层；0 = 使用 ObstacleLayers 与 Default 的常见组合仍建议显式勾选。")]
    public LayerMask GroundSnapOverlapLayers;

    [Tooltip("Overlap 半径相对马达半径的缩放（略小于 1 减少边缘误报）。")] [Range(0.85f, 0.999f)]
    public float GroundSnapOverlapRadiusScale = 0.99f;

    [Header("Slide — grounded sink lock")]
    [Tooltip("接地且本帧无显著向上初速时，禁止滑动剩余位移带向下分量，防止复合投影把角色往地里推。")]
    public bool ClampGroundedSlideRemainingY = true;

    [Header("Wall sanitization (anti-deadlock)")]
    [Tooltip("把近垂直墙面的微小 ±Y 法线分量强制清零，避免浮点漂移让 ProjectOnPlane 把跳跃动能折射成下钻动能（'跳跃地陷'根因）。\n例：墙面真实法线本应为 (1,0,0)，因网格顶点精度变成 (1,-0.005,0.001)，跳跃时 V.y>0 投影后变成 V.y<0，玩家被压进地里。")]
    public bool SanitizeNearVerticalWallNormals = true;

    [Tooltip("|normal.y| 小于此阈值视作墙面并清零 Y。0.1 ≈ 法线偏离垂直 ≤5.7°；过大会误伤陡坡（45° 坡的 normal.y=0.707 远大于此），过小则放过更多漂移。")]
    [Range(0.01f, 0.3f)]
    public float WallSanitizeNormalYThreshold = 0.1f;

    [Header("Ground probe — center raycast fallback")]
    [Tooltip("主 SphereCast 命中墙侧（W+A 沿墙摩擦时常见）时，再用一条胶囊轴心的细射线垂直下探作为兜底；细射线不会被胶囊侧面接触的墙面抢走，能精确穿过墙根缝隙找到地面。")]
    public bool EnableCenterRayGroundFallback = true;

    [Header("Debug (Scene / Game)")]
    [Tooltip("在 Scene 视图绘制 Sweep 命中点、法线与滑动方向。")]
    public bool DrawSweepHitsInSceneView;

    [Tooltip("命中点十字线半长（米）。")] [Range(0.02f, 0.3f)]
    public float SweepCrossHalfExtent = 0.08f;

    [Tooltip("Physics 命中法线 Debug 射线长度系数（相对 1m 基长）。")] [Range(0.1f, 2f)]
    public float SweepHitNormalDrawLength = 0.5f;

    [Tooltip("滑动方向 Debug 射线长度系数。")] [Range(0.1f, 2f)]
    public float SweepSlideDrawLength = 0.42f;

    [Tooltip("下半球过滤：原始法线射线长度系数。")] [Range(0.1f, 2f)]
    public float SweepLowerHemisphereOriginalDrawLength = 0.45f;

    [Tooltip("下半球过滤：展平后法线射线长度系数。")] [Range(0.1f, 2f)]
    public float SweepLowerHemisphereFilteredDrawLength = 0.52f;

    [Tooltip("在 Scene 视图绘制地面 SphereCast 主射线（红）与命中线段。")]
    public bool DrawGroundProbeInSceneView = true;

    [Tooltip("地面探针：主向下射线显示长度 = castDistance × 该系数。")] [Range(0.5f, 2f)]
    public float GroundProbeRayLengthScale = 1f;

    public Color GroundProbeMainRayColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color GroundProbeHitLineColor = new Color(0.2f, 0.95f, 0.25f, 1f);
    public Color GroundProbeExtraHitLineColor = new Color(1f, 0.45f, 0.1f, 1f);

    [Tooltip("Debug 线存活秒数；0 = 仅当前帧。")] [Range(0f, 4f)]
    public float SweepHitDebugLifetime = 0.35f;

    [Tooltip("命中障碍物时触发 CollisionDebugFlasher（需物件上已有组件，或开启下方 AutoAdd）。")]
    public bool FlashObstacleColliderOnSweepHit;

    [Tooltip("Sweep 命中时 CollisionDebugFlasher 的变色保持时间（秒）。")] [Range(0.02f, 0.5f)]
    public float SweepFlashHoldSeconds = 0.04f;

    [Tooltip("Sweep 命中时 CollisionDebugFlasher 的淡出时间（秒）。")] [Range(0.05f, 0.6f)]
    public float SweepFlashFadeSeconds = 0.22f;

    [Tooltip("测试用：首次命中时自动挂上 CollisionDebugFlasher；正式关卡请关。")]
    public bool AutoAddCollisionFlasherOnObstacleIfMissing;

    public Color SweepHitPointColor = new Color(1f, 0.15f, 0.1f, 1f);
    public Color SweepIgnoredHitColor = new Color(1f, 0.92f, 0.2f, 1f);
    public Color SweepHitNormalColor = new Color(0.2f, 0.95f, 0.25f, 1f);
    public Color SweepSlideDirectionColor = new Color(0.2f, 0.75f, 1f, 1f);

    [Tooltip("下半球法线被过滤时，在命中点绘制原始法线与过滤后法线（需 DrawSweepHitsInSceneView）。")]
    public bool DrawLowerHemisphereNormalFilter;

    public Color LowerHemisphereOriginalNormalColor = new Color(1f, 0.92f, 0.2f, 1f);
    public Color LowerHemisphereFilteredNormalColor = new Color(1f, 0.35f, 0.85f, 1f);

    [Tooltip("启用时向 Console 输出下半球法线过滤事件（约 0.12s 节流，避免刷屏）。")]
    public bool LogLowerHemisphereNormalFilters;

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
