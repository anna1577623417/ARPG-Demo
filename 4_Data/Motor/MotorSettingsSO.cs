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

    [Header("Terrain coupling — Step-Up & stairs")]
    [Tooltip("启用 KinematicMotorSolver 内 Lift→Forward→Drop 阶梯跨越（跑楼梯 / 矮槛）。")]
    public bool EnableKinematicStepUp = true;

    [Tooltip("启用 StepDown：上一帧接地、本帧主位移结束后若 Y 离地面在 StepOffset 内，纯 Y 修正下沉至地面。\n职责严格隔离：只改 transform.y，不改 velocity，不写 IsGrounded。\n根治下楼梯瞬间假滞空——KCC 主求解只解决水平位移，下台阶的微小垂直回收由本步专责。")]
    public bool EnableKinematicStepDown = true;

    [Tooltip("接地状态下，Grounding 阶梯带（Stair Band）判定的纵向半宽（米）。\n命中点高度在 [footY, footY + StepOffset + 此值] 内且法线可踩时，强制接地放行，绕过'动作脱地'与'上行速度死区'闸门。\n防上下楼梯单帧悬空闪烁。")]
    [Range(0f, 0.1f)]
    public float StairBandSlop = 0.02f;

    [Tooltip(
        "Stair Band 放行时要求法线朝上分量 ≥ 该值，用于排除近竖直踢面/侧墙；\n" +
        "不再把「可行走坡度」(MaxSlopeAngle) 作为 Stair Band 前置条件——楼梯三角面/边缘常返回略陡于 MaxSlopeAngle 的法线，原逻辑会导致 stairBand 永不触发、随后被 steepSlope 整段拒绝。")]
    [Range(0.05f, 0.5f)]
    public float StairBandMinNormalY = 0.12f;

    [Tooltip(
        "单级可跨越的最大垂直高度（米）。与 CharacterController.stepOffset 同类语义；过大易「飞台阶」，过小仍蹭立面。")]
    [Range(0.05f, 1f)]
    public float StepOffset = 0.3f;

    [Tooltip("阶梯接地探测：从抬起并前移后的位置向下 CapsuleCast 的额外长度（米）。")]
    [Range(0.05f, 0.6f)]
    public float StepDownProbeExtra = 0.22f;

    [Tooltip(
        "命中点相对脚底高度在 [0, StepOffset] 带宽内时，法线消毒不剥离 Y，便于 Slide 沿立面「滑上」台阶。\n与下落豁免互补：上楼 ≠ 自由落体凸角。")]
    public bool EnableStairHeightNormalExemption = true;

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

    [Tooltip(
        "子步位移上限 = Radius × 该系数（越小单帧切段越多，冲刺越不易穿薄墙 / 楼梯转角）。\n旧版固定 0.85·R；默认 0.5 偏保守。")]
    [Range(0.15f, 0.95f)]
    public float KinematicSubStepRadiusFraction = 0.5f;

    [Range(1, 12)] public int MaxSubSteps = 6;

    [Tooltip("单次子步内 Collide-and-Slide 的最大折射次数（含接缝滑出迭代）。")] [Range(1, 6)]
    public int MaxSlideIterations = 4;

    [Tooltip("锐角接缝：两法线点积小于该阈值时改用 cross(n0,n1) 滑出轴线。")] [Range(-1f, 0f)]
    public float CreviceNormalDotThreshold = -0.2f;

    [Header("Sweep — convex / internal edge")]
    [Tooltip(
        "位移方向·命中法线 点积大于该值视为「非阻挡」（凸角接缝、背向噪声、擦肩而过），\n本段剩余位移一次性放行并结束本子步折射，减轻接缝处抖动与过矫正死锁。")] [Range(-0.05f, 0.25f)]
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

    [Tooltip(
        "硬吸附允许的最大向上修正（米）。>0：探针给出的合法地面略高于当前 Pivot 时，仅在该幅度内允许上移，用于抗薄地/去穿插未挤出导致的轻微陷地；过大仍会被视为「蹭顶举升」而拒绝（见 Player 单向吸附逻辑）。\n0 = 完全禁止向上吸附（旧行为）。")]
    [Range(0f, 0.35f)]
    public float MaxGroundSnapUpwardPull = 0.12f;

    [Header("Slide — grounded sink lock")]
    [Tooltip("接地且本帧无显著向上初速时，禁止滑动剩余位移带向下分量，防止复合投影把角色往地里推。")]
    public bool ClampGroundedSlideRemainingY = true;

    [Header("Wall sanitization (anti-deadlock)")]
    [Tooltip("把近垂直墙面的微小 ±Y 法线分量强制清零，避免浮点漂移让 ProjectOnPlane 把跳跃动能折射成下钻动能（'跳跃地陷'根因）。\n例：墙面真实法线本应为 (1,0,0)，因网格顶点精度变成 (1,-0.005,0.001)，跳跃时 V.y>0 投影后变成 V.y<0，玩家被压进地里。")]
    public bool SanitizeNearVerticalWallNormals = true;

    [Tooltip("|normal.y| 小于此阈值视作墙面并清零 Y。0.1 ≈ 法线偏离垂直 ≤5.7°；过大会误伤陡坡（45° 坡的 normal.y=0.707 远大于此），过小则放过更多漂移。")]
    [Range(0.01f, 0.3f)]
    public float WallSanitizeNormalYThreshold = 0.1f;

    [Header("Half-wall edge sanitization (anti-jitter)")]
    [Tooltip("根治'半身墙顶边'抖动：胶囊侧面（位于两球心之间）命中带显著 Y 分量的法线时，强制展平为水平墙。\n背景：底球与墙顶边接触时，PhysX 因 Triangle Selection Non-determinism 在墙侧/墙顶/45°边缘三种法线间帧间跳变 → 投影方向高频抖 → 相机震动。本规则把三种法线全部归一到 (1,0,0)，根除帧间不一致。\n仅命中'上行边缘'时触发（n.y > 0），不影响走下凸边场景。")]
    public bool SanitizeHalfWallEdgeContact = true;

    [Tooltip("命中点 Y ≥ 底球球心 Y − 此容差时，视为'位于胶囊侧面'。默认 0.05。\n增大：覆盖更多接近底球底部的边缘命中（更激进），可能误吞低坡蹭斜上手感；减小：仅严格胶囊侧面才触发。")]
    [Range(0f, 0.2f)]
    public float EdgeContactBottomYSlop = 0.05f;

    [Tooltip("命中点 Y ≤ 顶球球心 Y + 此容差时，视为'位于胶囊侧面'。默认 0.05。\n增大：覆盖刚刚高于胶囊顶部的边缘；减小：严格仅圆柱体范围内。")]
    [Range(0f, 0.2f)]
    public float EdgeContactTopYSlop = 0.05f;

    [Tooltip("法线 Y 分量 > 此值才可能进入边缘消毒（排除纯水平墙——已由 Pass 0 处理）。默认 0.1。")]
    [Range(0f, 0.5f)]
    public float EdgeContactMinNormalY = 0.1f;

    [Tooltip("法线 Y 分量 < 此值才进入边缘消毒（排除纯垂直地面/顶面——由 LowerHemisphere 与 Phase 0 处理）。默认 0.95。")]
    [Range(0.5f, 0.99f)]
    public float EdgeContactMaxNormalY = 0.95f;

    [Tooltip("启用后向 Console 输出边缘消毒事件（约 0.12s 节流）。")]
    public bool LogEdgeContactSanitizations;

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

    [Header("Inter-frame normal lock / 跨帧法线缓存（半身墙抖动根治）")]
    [Tooltip(
        "【启用跨帧法线锁定 / Enable Inter-frame Normal Lock】\n" +
        "针对「半身墙顶边」「斜坡棱角」等位置 — PhysX 会在每一帧根据胶囊浮点漂移返回不同三角形的法线\n" +
        "（top-face / side-face / 中间斜面交替），导致 ProjectOnPlane 输出每帧抖动 → 相机震荡。\n" +
        "本机制：缓存上一帧同一 collider 的法线，本帧返回值与缓存「相似但不完全相同」时复用缓存，\n" +
        "扼杀 PhysX 三角形选择的非确定性。这是「半身墙抖、超高墙不抖」的根因修复。\n" +
        "EN: PhysX returns slightly different triangle normals each frame on edge contacts (half-wall tops, ramp corners). " +
        "Cache last frame's normal per collider; if this frame's normal is 'similar-but-not-identical', reuse the cache. " +
        "This kills triangle-selection non-determinism — the root cause of half-wall jitter.")]
    public bool EnableInterframeNormalLock = true;

    [Tooltip(
        "【缓存有效期（秒） / Normal Cache TTL】缓存超过此时间自动失效。\n" +
        "增大（→ 0.2）：长时间贴边时锁定更牢，但角色离开后再回来仍会用旧法线。\n" +
        "减小（→ 0.02）：缓存只在连续帧内生效，安全但短暂离开就重置。推荐 0.05 ~ 0.1。\n" +
        "EN: Cache entries expire after this. Recommended 0.05–0.10s.")]
    [Range(0.02f, 0.5f)]
    public float NormalCacheTTLSeconds = 0.08f;

    [Tooltip(
        "【缓存命中下界 / Cache Hit Min Dot】本帧法线与缓存的点积下限。\n" +
        "增大（→ 0.95）：仅极相似时复用，更保守；过大失去抗抖动能力。\n" +
        "减小（→ 0.7）：覆盖更多变化，但可能在真正的几何转角处错误冻结。推荐 0.85 ~ 0.95。\n" +
        "EN: Min dot for cache hit. Recommended 0.85–0.95.")]
    [Range(0.5f, 0.99f)]
    public float NormalCacheMinDot = 0.9f;

    [Tooltip(
        "【边缘命中识别 / Edge Contact Sanitization】\n" +
        "底半球撞到「半身墙顶边」时，法线呈 (0.7, 0.7, 0) 这类斜对角形态 —\n" +
        "既不被 WallSanitization 捕获（|n.y|>0.1）也不被 LowerHemisphere 陡坡判据拦截（不算陡坡）。\n" +
        "本机制：当 hit.point 低于胶囊中心 AND |n.y| 介于此区间，强制按「垂直墙」处理（清零 Y）。\n" +
        "EN: When the lower hemisphere hits a HALF-WALL's top edge, PhysX returns a diagonal normal that " +
        "escapes both wall-sanitize and steep-slope filters. Snap such edge normals to vertical-wall behavior.")]
    public bool EnableEdgeContactSanitization = true;

    [Tooltip(
        "【边缘命中 |n.y| 上界 / Edge Contact Normal Y Upper Bound】\n" +
        "底半球命中且 |n.y| ∈ (WallSanitizeNormalYThreshold, 此值) 时触发边缘消毒。\n" +
        "增大（→ 0.95）：把更多「斜面命中」误归类为「半身墙顶边」，会让斜坡变难走。\n" +
        "减小（→ 0.6）：仅最接近 45° 的边触发，对真正的半身墙顶（n.y≈0.7）覆盖弱。推荐 0.80 ~ 0.92。\n" +
        "EN: Edge sanitization triggers when |n.y| is between WallSanitizeNormalYThreshold and this. Recommended 0.80–0.92.")]
    [Range(0.3f, 0.99f)]
    public float EdgeContactNormalYUpperBound = 0.85f;

    [Tooltip("【调试：日志跨帧法线锁 / Log Normal Cache Hits】节流约 0.1s。\nEN: Throttled cache-hit log.")]
    public bool LogNormalCacheHits;

    [Tooltip(
        "【调试：绘制跨帧法线锁 / Draw Normal Cache Rays】\n" +
        "命中缓存复用时，在命中点绘制原始法线（橙）与复用法线（青），用于确认是否真的在“压平法线噪声”。\n" +
        "EN: Draw raw (orange) and cached (cyan) normals when cache lock is applied.")]
    public bool DrawNormalCacheRays;

    [Tooltip("跨帧法线锁：原始法线颜色 / Normal cache: raw normal color.")]
    public Color NormalCacheRawColor = new Color(1f, 0.55f, 0.15f, 1f);

    [Tooltip("跨帧法线锁：复用法线颜色 / Normal cache: cached normal color.")]
    public Color NormalCacheLockedColor = new Color(0.1f, 0.95f, 1f, 1f);

    [Tooltip(
        "【调试：绘制半身墙边缘消毒 / Draw Half-wall Edge Sanitization Rays】\n" +
        "Pass 0.5 触发时绘制消毒前法线（红）与消毒后法线（绿），用于确认“中段斜法线是否被压成纯墙”。\n" +
        "EN: Draw before/after normal rays when half-wall edge sanitization (Pass 0.5) triggers.")]
    public bool DrawHalfWallEdgeSanitizeRays;

    [Tooltip("半身墙边缘消毒：消毒前法线颜色 / Half-wall sanitize: raw normal color.")]
    public Color HalfWallEdgeRawNormalColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Tooltip("半身墙边缘消毒：消毒后法线颜色 / Half-wall sanitize: sanitized normal color.")]
    public Color HalfWallEdgeSanitizedNormalColor = new Color(0.2f, 1f, 0.35f, 1f);

    [Header("N-Plane solver (MVP) / 多平面约束求解（最小可用版）")]
    [Tooltip(
        "【启用 N-Plane（MVP） / Enable N-Plane Solver】\n" +
        "同一子步内缓存碰撞平面并联合求解，而不是单平面串行 ProjectOnPlane。\n" +
        "2 面：锐缝则用交棱轴向投影；否则顺序双 ProjectOnPlane；3 面：直接归零（死角阻挡）。\n" +
        "EN: 2-plane uses crevice seam axis or sequential twin-plane projection; 3-plane clamps to zero.")]
    public bool EnableNPlaneMvpSolver = true;

    [Tooltip(
        "【平面去重阈值 / Plane Dedup Dot Threshold】\n" +
        "同一子步内新命中法线与已缓存平面点积高于此值时视为同一平面，不重复加入。\n" +
        "增大（→0.999）：更严格去重，易保留微小噪声面；减小（→0.9）：更宽松去重，可能把真实不同面误合并。\n" +
        "EN: If dot(new, existing) > threshold, treat as same plane and skip adding.")]
    [Range(0.85f, 0.9999f)]
    public float PlaneDedupDotThreshold = 0.98f;

    [Tooltip(
        "【过弹修正系数 / Overbounce Factor】\n" +
        "联合投影时用于轻微“推出平面”以避免粘墙反复回撞。1.0=关闭，推荐 1.001~1.005。\n" +
        "增大：更不容易粘墙，但过大会显得发飘；减小：更物理真实但易产生边缘回撞。\n" +
        "EN: Slightly over-correct projection to avoid sticky re-penetration. 1.0 disables.")]
    [Range(1f, 1.02f)]
    public float NPlaneOverbounceFactor = 1.002f;

    [Tooltip(
        "【调试：绘制 N-Plane 求解方向 / Draw N-Plane Solve Ray】\n" +
        "联合约束求解后，在命中点绘制紫色结果速度射线，便于观察是否仍在 ping-pong。\n" +
        "EN: Draw the N-Plane solved velocity direction ray at hit point.")]
    public bool DrawNPlaneSolveRay;

    [Tooltip("N-Plane 求解结果射线颜色 / N-Plane solve result ray color.")]
    public Color NPlaneSolveRayColor = new Color(0.8f, 0.35f, 1f, 1f);

    [Header("Stability snapping / 速度稳定阈值")]
    [Tooltip(
        "【最小有效滑动速度（米/秒） / Min Effective Slide Speed】\n" +
        "Collide-and-Slide 投影后剩余位移所代表的等效速度模长低于此值时，强制截断剩余迭代（视为「死角挤压」）。\n" +
        "增大（→ 0.10）：更早静止、更稳，但可能在缓坡侧蹭时损失少量贴墙滑动距离。\n" +
        "减小（→ 0.005）：保留亚像素级移动，连续紧贴墙跑更顺，但相机微抖风险变高。推荐 0.01 ~ 0.05。\n" +
        "EN: After projection, if the remaining slide-equivalent speed falls below this, abort further iterations to prevent " +
        "sub-pixel jitter feeding the camera. Recommended 0.01–0.05 m/s.")]
    [Range(0.001f, 0.2f)]
    public float StabilitySnapMinSlideSpeed = 0.02f;

    [Tooltip(
        "【单次投影进度损耗上限（0~1） / Max Magnitude Loss Per Projection】\n" +
        "本帧某次 Slide 投影后，速度模长缩减比例超过此值视为「绝对死角」，截断剩余迭代避免反复折射放大噪声。\n" +
        "增大（→ 0.95）：更宽容，复杂折射场景有效；过大可能放过真实死锁。\n" +
        "减小（→ 0.7）：更早截断，反应灵敏；过小会在常规墙角误截影响沿墙滑动。推荐 0.85 ~ 0.92。\n" +
        "EN: If a single Slide projection shrinks |v| by MORE than this fraction, treat as deadlock and abort the iteration. " +
        "Recommended 0.85–0.92.")]
    [Range(0.5f, 0.99f)]
    public float DeadlockMaxMagnitudeLossPerProjection = 0.9f;

    [Tooltip(
        "【调试：记录稳定阅限触发 / Log Stability Aborts】\n" +
        "速度阅限 / 进度看门狗触发时输出节流日志（约 0.1s）。\n" +
        "EN: Log when stability snap or progress watchdog aborts the iteration (throttled).")]
    public bool LogStabilityAborts;

    [Tooltip(
        "【调试：绘制滑动速度射线 / Draw Slide Velocity Rays】\n" +
        "每次 Slide 投影后，在命中点额外绘制一条代表投影前后速度变化的对比射线（蓝=投影后，白=投影前）。\n" +
        "EN: Draw before/after velocity comparison rays at hit point for each slide projection (blue=after, white=before). " +
        "Helps spot ping-pong by seeing alternating slide directions across frames.")]
    public bool DrawSlideVelocityComparison;

    [Header("Debug · Corner / seam jitter")]
    [Tooltip(
        "仅在「单次子步 Sweep」内出现异常时输出 [CornerContact]：\n" +
        "· 相邻 slide 迭代消解后法线点积过低（接缝 / 振荡）\n" +
        "· PhysX raw 法线与 ResolveContactNormalForSlide 后法线不一致幅度大\n" +
        "· 连续迭代命中不同 Collider（两 Box 接缝 / 双面）\n" +
        "用于定位墙接缝、内棱处的微震颤与射线扇形乱跳。勿长期开启。")]
    public bool DebugCornerContactObservability;

    [Tooltip(
        "求解器在每次 SolveDisplacementFromPivot 时把「每一子步、每一 Slide 迭代」压成单行写入环形缓冲。\n" +
        "勾选后请在 PlayerKCCMotor 上同时打开「平面突变 Burst」，否则缓冲只堆积不打字。\n" +
        "EN: Ring-buffer per-substep/per-iter solver lines during each displacement solve.")]
    public bool DebugMotorSolveLineRingBuffer;

    [Tooltip("环形缓冲的最大行数（超过则丢弃最旧）。\nEN: Max solver lines retained per frame solve.")]
    [Range(8, 256)]
    public int DebugMotorSolveLineRingCapacity = 96;

    [Tooltip("同类日志最小间隔（秒），防刷屏。\nEN: Minimum seconds between corner-contact anomaly logs.")]
    [Range(0.02f, 0.5f)]
    public float CornerContactLogThrottleSeconds = 0.08f;

    [Tooltip(
        "上一迭代 slideNormal 与本迭代 slideNormal 点积低于此值则记异常（越小越少见证）。接缝 / 双面 Collider 常见 0.7~0.95 间抖动。")]
    [Range(0.5f, 0.995f)]
    public float CornerContactLogNormalDisagreementDot = 0.92f;

    [Tooltip("PhysX hit.normal 与消解后 slideNormal 点积低于此值则记异常（消毒/LowerHemisphere 改过法线）。")] [Range(0.3f, 0.995f)]
    public float CornerContactLogRawVsResolvedDot = 0.88f;

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
