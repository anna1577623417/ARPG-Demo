using UnityEngine;

/// <summary>
/// 纯 CapsuleSweep + Collide-and-Slide 约束求解内核（不写 MonoBehaviour）。
/// Why: Phase 1 连续碰撞离散化管线；可被 Player、Monster Motor、测试替身复用。
/// </summary>
public static class KinematicMotorSolver {
    private const float EpsilonSq = 1e-8f;
    private const int DepenetrationBufferSize = 32;
    private static float s_nextLowerHemisphereLogTime;
    private static float s_nextStabilityAbortLogTime;

    static string s_slideSolveBranchTag = "?";
    private static readonly Collider[] s_depenetrationBuffer = new Collider[DepenetrationBufferSize];
    private static GameObject s_penetrationProbeGo;
    private static CapsuleCollider s_penetrationProbeCollider;

    /// <summary>供地面吸附等使用的非分配重叠缓冲（调用方负责层与排除自身碰撞体）。</summary>
    public const int DefaultOverlapBufferSize = 16;

    private static readonly Collider[] s_stepOverlapBuffer = new Collider[DefaultOverlapBufferSize];

    /// <summary>
    /// 以角色 Pivot 为世界原点，求解本帧允许的位移矢量（三维一体，含下落）。
    /// </summary>
    /// <param name="applyGroundedSlideDownwardLock">上一帧接地且策略允许时，禁止滑动迭代产生向下剩余位移（防地陷）。</param>
    /// <summary>最近一次 Slide 约束（含两平面求解）选用的策略标签；供 Debug 读出。</summary>
    public static string DebugSlideSolveBranchTag => s_slideSolveBranchTag;

    public static Vector3 SolveDisplacementFromPivot(
        Vector3 worldPivot,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        Vector3 worldDisplacement,
        LayerMask obstacleLayers,
        bool applyGroundedSlideDownwardLock = false) {
        if (motor == null || worldDisplacement.sqrMagnitude < EpsilonSq) {
            return Vector3.zero;
        }

        s_slideSolveBranchTag = "?";

        var frac = Mathf.Clamp(motor.KinematicSubStepRadiusFraction, 0.12f, 0.95f);
        var maxStep = Mathf.Max(0.01f, motor.Radius * frac);
        var dispSq = worldDisplacement.sqrMagnitude;
        var dispMag = Mathf.Sqrt(dispSq);
        var subSteps = Mathf.CeilToInt(dispMag / maxStep);
        subSteps = Mathf.Clamp(subSteps, 1, Mathf.Max(1, motor.MaxSubSteps));

        var deltaTotal = Vector3.zero;
        var cursorPivot = worldPivot;

        for (var s = 0; s < subSteps; s++) {
            var step = worldDisplacement * (1f / subSteps);
            var partial = SolveSubStepSweep(cursorPivot, pivotToFootOffset, motor, step, obstacleLayers,
                applyGroundedSlideDownwardLock, s, subSteps);
            deltaTotal += partial;
            cursorPivot += partial;
        }

        return deltaTotal;
    }

    static void TagSlideSolveBranch(string tag)
    {
        s_slideSolveBranchTag = tag ?? "?";
    }

    /// <summary>
    /// Lift→Forward→Drop：跨过 StepOffset 以内的立面阻挡（跑楼梯 / 矮槛），不走纯 Slide「立面即墙」模型。
    /// </summary>
    private static bool TryStepUp(
        Vector3 worldPivot,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        Vector3 displacement,
        LayerMask obstacleLayers,
        out Vector3 stepDelta) {
        stepDelta = Vector3.zero;
        if (motor == null || !motor.EnableKinematicStepUp || motor.StepOffset <= 0.01f) {
            return false;
        }

        var dispMag = displacement.magnitude;
        if (dispMag < 1e-5f) {
            return false;
        }

        var dirFull = displacement / dispMag;
        if (dirFull.y > 0.35f) {
            return false;
        }

        var horizFlat = new Vector3(displacement.x, 0f, displacement.z);
        var horizMag = horizFlat.magnitude;
        if (horizMag < 1e-5f) {
            return false;
        }

        var dir = horizFlat * (1f / horizMag);
        var skin = Mathf.Max(0.0005f, motor.SkinWidth);
        var castRadius = Mathf.Max(0.01f, motor.Radius - skin * 0.5f);
        GetCapsuleWorld(worldPivot, pivotToFootOffset, motor, out var p1, out var p2, out _);

        if (!Physics.CapsuleCast(
                p1,
                p2,
                castRadius,
                dir,
                out var hit,
                Mathf.Max(horizMag, skin),
                obstacleLayers,
                QueryTriggerInteraction.Ignore)) {
            return false;
        }

        if (hit.distance >= horizMag - skin * 1.5f) {
            return false;
        }

        var footY = worldPivot.y - Mathf.Max(0f, pivotToFootOffset);
        var hitRelFootY = hit.point.y - footY;
        var maxStep = motor.StepOffset;
        if (hitRelFootY > maxStep + 0.1f) {
            return false;
        }

        var liftCap = Mathf.Clamp(maxStep, 0.05f, maxStep);
        var liftDir = Vector3.up;
        float actualLift;
        if (!Physics.CapsuleCast(
                p1,
                p2,
                castRadius,
                liftDir,
                out var upHit,
                liftCap + skin,
                obstacleLayers,
                QueryTriggerInteraction.Ignore)) {
            actualLift = liftCap;
        }
        else {
            actualLift = Mathf.Max(0f, Mathf.Min(liftCap, upHit.distance - skin));
        }

        if (actualLift < 0.035f) {
            return false;
        }

        var liftedPivot = worldPivot + Vector3.up * actualLift;
        GetCapsuleWorld(liftedPivot, pivotToFootOffset, motor, out var lp1, out var lp2, out _);

        float forwardTravel;
        if (!Physics.CapsuleCast(
                lp1,
                lp2,
                castRadius,
                dir,
                out var fHit,
                horizMag,
                obstacleLayers,
                QueryTriggerInteraction.Ignore)) {
            forwardTravel = horizMag;
        }
        else {
            forwardTravel = Mathf.Max(0f, fHit.distance - skin);
        }

        if (forwardTravel < skin * 1.5f) {
            return false;
        }

        var forwardMoved = dir * forwardTravel;
        var midPivot = liftedPivot + forwardMoved;

        GetCapsuleWorld(midPivot, pivotToFootOffset, motor, out var dp1, out var dp2, out _);
        var dropMax = actualLift + Mathf.Max(0.05f, motor.StepDownProbeExtra);

        if (!Physics.CapsuleCast(
                dp1,
                dp2,
                castRadius,
                Vector3.down,
                out var dHit,
                dropMax,
                obstacleLayers,
                QueryTriggerInteraction.Ignore)) {
            return false;
        }

        if (motor.IsSlopeTooSteep(dHit.normal)) {
            return false;
        }

        var targetPivotY = dHit.point.y + Mathf.Max(0f, pivotToFootOffset);
        var resultPivot = new Vector3(midPivot.x, targetPivotY, midPivot.z);

        if (motor.GroundSnapOverlapGuard) {
            var overlapMask = motor.GroundSnapOverlapLayers.value != 0
                ? motor.GroundSnapOverlapLayers
                : motor.ObstacleLayers;
            if (OverlapCapsuleAtPivotBlocks(
                    resultPivot,
                    pivotToFootOffset,
                    motor,
                    overlapMask,
                    null,
                    s_stepOverlapBuffer)) {
                return false;
            }
        }

        stepDelta = resultPivot - worldPivot;
        return stepDelta.sqrMagnitude > 1e-10f;
    }

    /// <summary>
    /// StepDown：纯 Y 修正——上一帧接地、本帧水平位移结束后，若胶囊脚底离下方地面落差在
    /// <see cref="MotorSettingsSO.StepOffset"/> 内，下沉到地面对齐处。
    ///
    /// ═══ 职责严格隔离（遵循"局部修正"原则）═══
    ///   ✅ 只返回向下位移 delta（仅 Y 分量）
    ///   ❌ 不写 transform、不写 IsGrounded、不写 m_verticalSpeed
    ///   ❌ 不在跳跃中触发（vy &gt; 0 由调用方门控）
    ///
    /// 根治"下楼梯瞬间假滞空"：KCC 主求解只走水平 + 重力下落；下台阶时落差通常 0.15~0.30m，
    /// 单帧重力位移远小于此 → 解算后 Y 仍悬空 → 探针看不到地面 → 假滞空 → 跳变；
    /// 本步在主求解后做一次精准下沉。
    /// </summary>
    /// <param name="wasGroundedLastFrame">上一帧接地标志（本帧是真"下楼梯"还是真"跳跃/起跳"的关键判据）。</param>
    /// <param name="snapDelta">输出：本帧需要追加的位移（仅 Y 分量为负）。</param>
    public static bool TryStepDown(
        Vector3 worldPivot,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        bool wasGroundedLastFrame,
        LayerMask obstacleLayers,
        out Vector3 snapDelta)
    {
        snapDelta = Vector3.zero;
        if (motor == null || !motor.EnableKinematicStepDown || !wasGroundedLastFrame)
        {
            return false;
        }

        if (motor.StepOffset <= 0.01f)
        {
            return false;
        }

        var skin = Mathf.Max(0.0005f, motor.SkinWidth);
        var castRadius = Mathf.Max(0.01f, motor.Radius - skin * 0.5f);
        GetCapsuleWorld(worldPivot, pivotToFootOffset, motor, out var p1, out var p2, out _);

        // 探测距离 = StepOffset + 额外探测延伸（与 StepUp 复用同一字段，语义对称）
        var probeDistance = motor.StepOffset + Mathf.Max(0.05f, motor.StepDownProbeExtra);

        if (!Physics.CapsuleCast(
                p1,
                p2,
                castRadius,
                Vector3.down,
                out var hit,
                probeDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // 法线必须可踩——侧蹭墙的水平命中不参与下沉吸附
        if (motor.IsSlopeTooSteep(hit.normal))
        {
            return false;
        }

        // 已经几乎贴地（小于 SkinWidth）：本帧 KCC 已处理，无需再下沉
        var travel = hit.distance - skin;
        if (travel <= skin * 0.5f)
        {
            return false;
        }

        // 落差超过 StepOffset：这是真下落（跳下高台），不属于 StepDown 范畴，让重力自然处理
        if (travel > motor.StepOffset)
        {
            return false;
        }

        snapDelta = new Vector3(0f, -travel, 0f);
        return true;
    }

    private static Vector3 SolveSubStepSweep(
        Vector3 worldPivot,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        Vector3 displacement,
        LayerMask obstacleLayers,
        bool applyGroundedSlideDownwardLock,
        int sweepSubStepIndex = 0,
        int sweepSubStepCount = 1) {
        if (displacement.sqrMagnitude < EpsilonSq) {
            return Vector3.zero;
        }

        var stepHorizSq = displacement.x * displacement.x + displacement.z * displacement.z;
        if (motor.EnableKinematicStepUp
            && stepHorizSq > 1e-8f
            && displacement.y > -0.28f
            && TryStepUp(worldPivot, pivotToFootOffset, motor, displacement, obstacleLayers, out var stepDelta)) {
            return stepDelta;
        }

        var skin = Mathf.Max(0.0005f, motor.SkinWidth);
        var iterations = Mathf.Max(1, motor.MaxSlideIterations);
        var remaining = displacement;
        var accumulated = Vector3.zero;

        Vector3 lastNormal = default;
        var hadPreviousNormal = false;

        var castRadius = Mathf.Max(0.01f, motor.Radius - skin * 0.5f);

        for (var iter = 0; iter < iterations; iter++) {
            if (remaining.sqrMagnitude < EpsilonSq) {
                break;
            }

            GetCapsuleWorld(worldPivot + accumulated, pivotToFootOffset, motor, out var p1, out var p2, out _);

            var remSq = remaining.sqrMagnitude;
            var dist = Mathf.Sqrt(remSq);
            var dir = dist > 1e-6f ? remaining * (1f / dist) : Vector3.forward;

            if (!Physics.CapsuleCast(
                    p1,
                    p2,
                    castRadius,
                    dir,
                    out var hit,
                    Mathf.Max(dist, skin),
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore)) {
                accumulated += remaining;
                break;
            }

            // ── Falling Exemption（下落豁免）──────────────────────────────────────
            //   原因：WallSanitize + LowerHemisphere 可能把凸角顶点斜上法线压平，下落时再吃光重力分量 → 悬浮。
            //   解药：本帧位移方向向下（dir.y<0）时跳过 Y 剥离，让 PhysX 原始斜法线把重力
            //         折射成沿边缘的滑落速度，自然脱离顶点。
            var isFalling = dir.y < -0.05f;
            var slideNormal = ResolveContactNormalForSlide(motor, p1, in hit, isFalling, out var normalFiltered);

            var approachNormalDot = Vector3.Dot(dir, slideNormal);
            if (approachNormalDot > motor.VelocityAgainstNormalRejectDot) {
                DrawSweepHitDiagnostics(motor, in hit, remaining, rejectedAsNonBlocking: true, in slideNormal,
                    normalFiltered);
                MaybeFlashObstacle(motor, in hit);
                accumulated += remaining;
                break;
            }

            var safeAlong = Mathf.Max(0f, hit.distance - skin);
            accumulated += dir * safeAlong;

            remaining = remaining - dir * safeAlong;

            if (remaining.sqrMagnitude < EpsilonSq) {
                DrawSweepHitDiagnostics(motor, in hit, Vector3.zero, rejectedAsNonBlocking: false, in slideNormal,
                    normalFiltered);
                MaybeFlashObstacle(motor, in hit);
                break;
            }

            var remainingBeforeSlide = remaining;
            remaining = SlideWithCreviceOrTwinPlane(
                remaining,
                slideNormal,
                hadPreviousNormal,
                lastNormal,
                motor.CreviceNormalDotThreshold,
                iter);

            if (applyGroundedSlideDownwardLock
                && motor.ClampGroundedSlideRemainingY
                && remaining.y < 0f) {
                remaining.y = 0f;
            }

            DrawSweepHitDiagnostics(motor, in hit, remaining, rejectedAsNonBlocking: false, in slideNormal,
                normalFiltered);
            DrawSlideVelocityComparison(motor, in hit, remainingBeforeSlide, remaining);
            MaybeFlashObstacle(motor, in hit);

            // ── Stability Snap：剩余位移代表的等效速度过小 → 截断防亚像素抖动放大 ─────────
            //    EpsilonSq 仅过滤数值噪声；StabilitySnapMinSlideSpeed 才是工程级"死角即停"门槛。
            var remainingMagSq = remaining.sqrMagnitude;
            if (remainingMagSq < EpsilonSq) {
                break;
            }

            var minSnapSpeed = motor.StabilitySnapMinSlideSpeed;
            if (minSnapSpeed > 0.0001f) {
                var dt = Time.deltaTime;
                var minDispSq = minSnapSpeed * dt;
                minDispSq *= minDispSq;
                if (remainingMagSq < minDispSq) {
                    if (motor.LogStabilityAborts && Time.unscaledTime >= s_nextStabilityAbortLogTime) {
                        s_nextStabilityAbortLogTime = Time.unscaledTime + 0.1f;
                        //Debug.Log(
                        //    $"[KinematicMotor][StabilitySnap] iter={iter} |v_remain|={Mathf.Sqrt(remainingMagSq) / Mathf.Max(dt, 1e-5f):F4} m/s " +
                        //    $"< minSpeed={minSnapSpeed:F3} m/s → abort iter");
                    }

                    break;
                }
            }

            // ── Progress Watchdog：单次投影损耗过大（绝对死角）→ 截断防迭代放大噪声 ──────
            //    iter==0 排除：玩家正面顶墙时第一次投影损耗 99% 是正常物理现象（V⊥墙），不是死锁。
            //    死锁的判据是「多次折射后还在持续损耗」，因此只在 iter≥1 后启用。
            var deadlockLossCap = motor.DeadlockMaxMagnitudeLossPerProjection;
            if (iter > 0 && deadlockLossCap > 0.001f && deadlockLossCap < 0.999f) {
                var beforeMagSq = remainingBeforeSlide.sqrMagnitude;
                if (beforeMagSq > EpsilonSq) {
                    var ratioSq = remainingMagSq / beforeMagSq;
                    var keepRatioFloor = 1f - deadlockLossCap;
                    if (ratioSq < keepRatioFloor * keepRatioFloor) {
                        if (motor.LogStabilityAborts && Time.unscaledTime >= s_nextStabilityAbortLogTime) {
                            s_nextStabilityAbortLogTime = Time.unscaledTime + 0.1f;
                            Debug.Log(
                                $"[KinematicMotor][ProgressWatchdog] iter={iter} loss={(1f - Mathf.Sqrt(ratioSq)) * 100f:F1}% " +
                                $"> cap={deadlockLossCap * 100f:F0}% → abort iter (deadlock)");
                        }

                        break;
                    }
                }
            }

            hadPreviousNormal = true;
            lastNormal = slideNormal;
        }

        return accumulated;
    }

    static Vector3 SlideWithCreviceOrTwinPlane(
        Vector3 remaining,
        Vector3 nCur,
        bool hadPrev,
        Vector3 lastNormal,
        float creviceDotThresh,
        int iterationIndex) {
        if (!hadPrev || iterationIndex <= 0) {
            TagSlideSolveBranch($"SinglePlane_iter{iterationIndex}");
            return Vector3.ProjectOnPlane(remaining, nCur);
        }

        var nLast = lastNormal.normalized;
        return SolveTwinPlaneCreviceSequential(remaining, nLast, nCur, creviceDotThresh);
    }

    static Vector3 SolveTwinPlaneCreviceSequential(Vector3 remaining, Vector3 nLast, Vector3 nCur,
        float creviceDotThresh) {
        var dot = Vector3.Dot(nCur, nLast);
        if (dot < creviceDotThresh) {
            var seam = Vector3.Cross(nLast, nCur);
            if (seam.sqrMagnitude > 1e-10f) {
                seam.Normalize();
                var projected = Vector3.Project(remaining, seam);
                if (projected.sqrMagnitude > EpsilonSq) {
                    TagSlideSolveBranch($"SharpCreviceSeam(dot={dot:F3})");
                    return projected;
                }
            }
        }

        TagSlideSolveBranch($"SequentialTwinPlane(dot={dot:F3})");
        return Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(remaining, nLast), nCur);
    }

    private static float SweepDebugSeconds(MotorSettingsSO motor) {
        return motor != null && motor.SweepHitDebugLifetime > 0.0001f ? motor.SweepHitDebugLifetime : 0f;
    }

    private static void DrawSweepHitDiagnostics(MotorSettingsSO motor, in RaycastHit hit, Vector3 slideVector,
        bool rejectedAsNonBlocking, in Vector3 resolvedSlideNormal, bool usedLowerHemisphereFilter) {
        if (motor == null || !motor.DrawSweepHitsInSceneView) {
            return;
        }

        var t = SweepDebugSeconds(motor);
        var pointColor = rejectedAsNonBlocking ? motor.SweepIgnoredHitColor : motor.SweepHitPointColor;
        var cross = Mathf.Max(0.02f, motor.SweepCrossHalfExtent);
        DrawCross(hit.point, cross, pointColor, t);
        if (!rejectedAsNonBlocking) {
            Debug.DrawRay(hit.point, hit.normal * motor.SweepHitNormalDrawLength, motor.SweepHitNormalColor, t);
        }

        if (usedLowerHemisphereFilter && motor.DrawLowerHemisphereNormalFilter) {
            Debug.DrawRay(hit.point, hit.normal * motor.SweepLowerHemisphereOriginalDrawLength,
                motor.LowerHemisphereOriginalNormalColor, t);
            Debug.DrawRay(hit.point, resolvedSlideNormal * motor.SweepLowerHemisphereFilteredDrawLength,
                motor.LowerHemisphereFilteredNormalColor, t);
        }

        if (slideVector.sqrMagnitude > EpsilonSq) {
            var sm = slideVector.sqrMagnitude;
            var inv = motor.SweepSlideDrawLength / Mathf.Sqrt(sm);
            Debug.DrawRay(hit.point, slideVector * inv, motor.SweepSlideDirectionColor, t);
        }
    }

    /// <summary>
    /// 滑梯接触法线：可行走坡面豁免 → 墙面 Y 归零 → 下半球陡坡展平。
    /// </summary>
    private static Vector3 ResolveContactNormalForSlide(
        MotorSettingsSO motor,
        Vector3 bottomSphereCenter,
        in RaycastHit hit,
        bool isFalling,
        out bool appliedLowerHemisphereFilter) {
        appliedLowerHemisphereFilter = false;
        var n = hit.normal.normalized;

        // 可行走坡面：勿压平 Y，否则会失去沿阶/上坡分量。
        if (motor != null && !isFalling) {
            if (n.y > 0.0001f && !motor.IsSlopeTooSteep(n)) {
                return n;
            }
        }

        var footY = bottomSphereCenter.y - motor.Radius;
        var hitRelToFoot = hit.point.y - footY;
        var stairBand = motor != null
            && motor.EnableStairHeightNormalExemption
            && !isFalling
            && hitRelToFoot >= -0.08f
            && hitRelToFoot <= motor.StepOffset + 0.08f;
        var exemptYStrip = isFalling || stairBand;

        if (motor != null && motor.SanitizeNearVerticalWallNormals
            && Mathf.Abs(n.y) < motor.WallSanitizeNormalYThreshold) {
            var nxz0 = new Vector3(n.x, 0f, n.z);
            var nxz0Sq = nxz0.sqrMagnitude;
            if (nxz0Sq > 1e-10f) {
                n = nxz0 * (1f / Mathf.Sqrt(nxz0Sq));
            }
        }

        if (motor == null || !motor.EnableLowerHemisphereNormalFilter) {
            return n;
        }

        if (exemptYStrip) {
            return n;
        }

        if (motor.LowerHemisphereOnlyWhenContactBelowBottomCenter
            && hit.point.y > bottomSphereCenter.y + motor.LowerHemisphereBottomCenterYSlop) {
            return n;
        }

        var toContact = hit.point - bottomSphereCenter;
        var horizSq = toContact.x * toContact.x + toContact.z * toContact.z;
        var poleAllow = motor.LowerHemispherePoleRadiusFraction > 0.0001f
            ? Mathf.Max(0.001f, motor.Radius * motor.LowerHemispherePoleRadiusFraction)
            : 0f;
        if (poleAllow > 0.0005f && horizSq <= poleAllow * poleAllow) {
            return n;
        }

        var steep = motor.IsSlopeTooSteep(n);
        var shallowSideScrape = motor.LowerHemisphereFlattenIfGroundNormalYBelow > 0.001f
                                && n.y > 0.0001f
                                && n.y < motor.LowerHemisphereFlattenIfGroundNormalYBelow;
        if (!steep && !shallowSideScrape) {
            return n;
        }

        var nxz = new Vector3(n.x, 0f, n.z);
        var nxzSq = nxz.sqrMagnitude;
        if (nxzSq < 1e-10f) {
            return n;
        }

        nxz *= 1f / Mathf.Sqrt(nxzSq);
        appliedLowerHemisphereFilter = true;

        if (motor.LogLowerHemisphereNormalFilters && Time.unscaledTime >= s_nextLowerHemisphereLogTime) {
            s_nextLowerHemisphereLogTime = Time.unscaledTime + 0.12f;
            Debug.Log(
                $"[KinematicMotor] LowerHemisphereNormalFilter | point=({hit.point.x:F3},{hit.point.y:F3},{hit.point.z:F3}) " +
                $"bottomY={bottomSphereCenter.y:F3} horizOff={Mathf.Sqrt(horizSq):F3} " +
                $"nIn=({n.x:F3},{n.y:F3},{n.z:F3}) nOut=({nxz.x:F3},{nxz.y:F3},{nxz.z:F3}) collider={hit.collider?.name}",
                hit.collider);
        }

        return nxz;
    }

    /// <summary>
    /// 给定 Pivot 处胶囊是否与 layerMask 内任意非 Trigger 碰撞体重叠（不含 ignoreRoot 子层级）。
    /// </summary>
    public static bool OverlapCapsuleAtPivotBlocks(
        Vector3 pivotWorld,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        LayerMask mask,
        Transform ignoreRoot,
        Collider[] overlapBuffer) {
        if (motor == null || overlapBuffer == null || overlapBuffer.Length == 0) {
            return false;
        }

        GetCapsuleWorld(pivotWorld, pivotToFootOffset, motor, out var p1, out var p2, out var r);
        var rs = Mathf.Max(0.01f, r * motor.GroundSnapOverlapRadiusScale);
        var n = Physics.OverlapCapsuleNonAlloc(
            p1,
            p2,
            rs,
            overlapBuffer,
            mask,
            QueryTriggerInteraction.Ignore);

        for (var i = 0; i < n; i++) {
            var c = overlapBuffer[i];
            if (c == null) {
                continue;
            }

            if (ignoreRoot != null && c.transform != null
                                   && (c.transform == ignoreRoot || c.transform.IsChildOf(ignoreRoot))) {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void DrawCross(Vector3 center, float halfExtent, Color color, float duration) {
        Debug.DrawLine(center - Vector3.right * halfExtent, center + Vector3.right * halfExtent, color,
            duration);
        Debug.DrawLine(center - Vector3.up * halfExtent, center + Vector3.up * halfExtent, color,
            duration);
        Debug.DrawLine(center - Vector3.forward * halfExtent, center + Vector3.forward * halfExtent, color,
            duration);
    }

    /// <summary>
    /// 在命中点绘制投影前（白）与投影后（蓝）的速度方向对比射线，辅助肉眼判断乒乓振荡。
    /// 若仍交替，优先查场景 MeshCollider 合并与其它几何。
    /// </summary>
    private static void DrawSlideVelocityComparison(MotorSettingsSO motor, in RaycastHit hit,
        Vector3 velocityBefore, Vector3 velocityAfter) {
        if (motor == null || !motor.DrawSlideVelocityComparison || !motor.DrawSweepHitsInSceneView) {
            return;
        }

        var t = SweepDebugSeconds(motor);
        const float refLen = 0.35f;

        if (velocityBefore.sqrMagnitude > EpsilonSq) {
            Debug.DrawRay(hit.point, velocityBefore.normalized * refLen,
                new Color(0.9f, 0.9f, 0.9f, 0.7f), t); // 白色 = 投影前
        }

        if (velocityAfter.sqrMagnitude > EpsilonSq) {
            Debug.DrawRay(hit.point, velocityAfter.normalized * refLen,
                new Color(0.15f, 0.65f, 1f, 1f), t); // 蓝色 = 投影后
        }
    }

    private static void MaybeFlashObstacle(MotorSettingsSO motor, in RaycastHit hit) {
        if (motor == null || !motor.FlashObstacleColliderOnSweepHit || hit.collider == null) {
            return;
        }

        CollisionDebugFlasher flasher;
        if (motor.AutoAddCollisionFlasherOnObstacleIfMissing) {
            flasher = CollisionDebugFlasher.GetOrCreate(hit.collider);
        } else {
            flasher = hit.collider.GetComponent<CollisionDebugFlasher>();
        }

        var flashRgb = motor.SweepHitPointColor;
        flasher?.Flash(new Color(flashRgb.r, flashRgb.g, flashRgb.b, 1f), motor.SweepFlashHoldSeconds,
            motor.SweepFlashFadeSeconds);
    }

    internal static void GetCapsuleWorld(
        Vector3 pivotWorld,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        out Vector3 bottomSphere,
        out Vector3 topSphere,
        out float radius) {
        var h = motor.EffectiveHeight;
        radius = Mathf.Max(0.01f, motor.Radius);

        var footY = pivotWorld.y - Mathf.Max(0f, pivotToFootOffset);
        var bottomY = footY + radius;
        var topY = footY + h - radius;
        bottomY = Mathf.Min(bottomY, topY - 0.01f);

        var cx = pivotWorld.x;
        var cz = pivotWorld.z;

        bottomSphere = new Vector3(cx, bottomY, cz);
        topSphere = new Vector3(cx, topY, cz);
    }

    /// <summary>
    /// Pivot 下移后的地面探测命中（独立于碰撞层）。
    /// </summary>
    public static bool ProbeGroundBelowPivot(
        Vector3 pivotWorld,
        float pivotToFootOffset,
        float sphereRadius,
        float castExtraDistance,
        LayerMask groundLayers,
        out RaycastHit bestHit) {
        bestHit = default;

        var footPos = pivotWorld - Vector3.up * pivotToFootOffset;
        var probeR = Mathf.Max(0.01f, sphereRadius);
        var startOffset = probeR + 0.08f;
        var origin = footPos + Vector3.up * startOffset;
        var castDistance = Mathf.Max(0.15f, 0.1f + castExtraDistance);

        if (!Physics.SphereCast(
                origin,
                probeR,
                Vector3.down,
                out bestHit,
                castDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore)) {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 将离散位移按直线路径的胶囊 Sweep 截断，避免 Teleport/Blink 终点落入障碍物。
    /// </summary>
    public static Vector3 ClampDisplacementByObstacleSweep(
        Vector3 pivotWorld,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        Vector3 desiredDisplacement,
        LayerMask obstacleLayers) {
        if (motor == null || desiredDisplacement.sqrMagnitude < EpsilonSq) {
            return desiredDisplacement;
        }

        GetCapsuleWorld(pivotWorld, pivotToFootOffset, motor, out var p1, out var p2, out _);
        var skin = Mathf.Max(0.0005f, motor.SkinWidth);
        var castRadius = Mathf.Max(0.01f, motor.Radius - skin * 0.5f);
        var distance = desiredDisplacement.magnitude;
        var dir = desiredDisplacement / Mathf.Max(distance, 1e-6f);

        if (!Physics.CapsuleCast(
                p1,
                p2,
                castRadius,
                dir,
                out var hit,
                distance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore)) {
            return desiredDisplacement;
        }

        var allowed = Mathf.Max(0f, hit.distance - skin);
        return dir * allowed;
    }

    /// <summary>
    /// 位移后自愈：若胶囊已与障碍重叠，使用 ComputePenetration 迭代挤出到安全位置。
    /// </summary>
    public static bool ResolveOverlapsAtPivot(
        Vector3 pivotWorld,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        LayerMask obstacleLayers,
        Transform ignoreRoot,
        out Vector3 resolvedPivot) {
        resolvedPivot = pivotWorld;
        if (motor == null || obstacleLayers.value == 0) {
            return false;
        }

        var probe = GetOrCreatePenetrationProbe();
        if (probe == null) {
            return false;
        }

        var skin = Mathf.Max(0.0005f, motor.SkinWidth);
        var overlapRadius = Mathf.Max(0.01f, (motor.Radius - skin * 0.5f) * 0.995f);
        var extraPush = Mathf.Max(0.0005f, skin * 0.2f);
        var movedAny = false;

        for (var loop = 0; loop < 3; loop++) {
            GetCapsuleWorld(resolvedPivot, pivotToFootOffset, motor, out var p1, out var p2, out _);
            var count = Physics.OverlapCapsuleNonAlloc(
                p1,
                p2,
                overlapRadius,
                s_depenetrationBuffer,
                obstacleLayers,
                QueryTriggerInteraction.Ignore);
            if (count <= 0) {
                break;
            }

            SetupPenetrationProbeTransform(probe, resolvedPivot, pivotToFootOffset, motor, overlapRadius);

            var resolvedThisLoop = false;
            for (var i = 0; i < count; i++) {
                var c = s_depenetrationBuffer[i];
                if (c == null || c == probe) {
                    continue;
                }

                if (ignoreRoot != null && c.transform != null
                                    && (c.transform == ignoreRoot || c.transform.IsChildOf(ignoreRoot))) {
                    continue;
                }

                if (!Physics.ComputePenetration(
                        probe,
                        probe.transform.position,
                        probe.transform.rotation,
                        c,
                        c.transform.position,
                        c.transform.rotation,
                        out var dir,
                        out var dist)) {
                    continue;
                }

                if (dist <= 1e-6f || dir.sqrMagnitude <= 1e-10f) {
                    continue;
                }

                // 向下挤出会把胶囊压穿薄地面（冲刺瞬叠 / 接缝）
                if (dir.y < -1e-5f) {
                    continue;
                }

                // 凸角上 ComputePenetration 常返回竖直向上方向 → 角色被举上墙顶 → 相机插入体内。
                // 例外：穿透深度极小时且分离方向几乎朝上 → 视为薄地面/接缝埋入，允许完整上移脱困。
                // 否则 Depen 只做水平挤出 + Player 禁止大幅向上吸附 → 偶发永久陷地。
                var d = dir;
                if (d.y > 0f) {
                    var dn = d.normalized;
                    if (dist <= 0.065f && dn.y >= 0.88f) {
                        resolvedPivot += d * (dist + extraPush);
                        probe.transform.position = resolvedPivot;
                        resolvedThisLoop = true;
                        movedAny = true;
                        continue;
                    }

                    d.y = 0f;
                    var sq = d.sqrMagnitude;
                    if (sq < 1e-10f) {
                        // 纯竖直顶起 → 跳过该碰撞体，避免被举上墙顶。
                        continue;
                    }
                    d *= 1f / Mathf.Sqrt(sq);
                }

                resolvedPivot += d * (dist + extraPush);
                probe.transform.position = resolvedPivot;
                resolvedThisLoop = true;
                movedAny = true;
            }

            if (!resolvedThisLoop) {
                break;
            }
        }

        return movedAny;
    }

    /// <summary>
    /// 瞬移到目标 Pivot 前先向下找支撑面；可选按 <see cref="MotorSettingsSO.MaxSlopeAngle"/> 拒绝凸角/墙顶。
    /// </summary>
    /// <param name="motorForWalkableSlopeGate">非空且命中法线过陡时返回 false，避免 Teleport 锁在过陡尖角或墙顶。</param>
    public static bool ResolvePivotHeightFromAbove(
        float pivotDesiredX,
        float pivotDesiredZ,
        float referenceHeightBelowRayStart,
        float rayStartYOffset,
        float maxRayDistance,
        float pivotToFootOffset,
        LayerMask groundLayers,
        MotorSettingsSO motorForWalkableSlopeGate,
        out float resolvedPivotY) {
        resolvedPivotY = 0f;
        if (maxRayDistance <= 0f) {
            return false;
        }

        var origin = new Vector3(pivotDesiredX, referenceHeightBelowRayStart + rayStartYOffset, pivotDesiredZ);
        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out var hit,
                maxRayDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore)) {
            return false;
        }

        if (motorForWalkableSlopeGate != null && motorForWalkableSlopeGate.IsSlopeTooSteep(hit.normal)) {
            return false;
        }

        resolvedPivotY = hit.point.y + pivotToFootOffset;
        return true;
    }

    /// <summary>
    /// 将位移投影到斜坡切平面（保持模长在平面上的等价前进感，由位移尺度近似）。
    /// </summary>
    public static Vector3 ProjectDisplacementOntoGroundPlaneIfWalkable(Vector3 displacement, Vector3 groundNormal,
        MotorSettingsSO motor) {
        if (motor == null || !motor.EnablesSlopeProjection || displacement.sqrMagnitude < EpsilonSq) {
            return displacement;
        }

        var n = groundNormal.normalized;
        if (motor.IsSlopeTooSteep(n)) {
            return displacement;
        }

        return Vector3.ProjectOnPlane(displacement, n);
    }

    private static CapsuleCollider GetOrCreatePenetrationProbe() {
        if (s_penetrationProbeCollider != null) {
            return s_penetrationProbeCollider;
        }

        s_penetrationProbeGo = new GameObject("_KinematicMotor_DepenetrationProbe") {
            hideFlags = HideFlags.HideAndDontSave
        };
        s_penetrationProbeGo.layer = 2; // Ignore Raycast
        s_penetrationProbeCollider = s_penetrationProbeGo.AddComponent<CapsuleCollider>();
        s_penetrationProbeCollider.isTrigger = true;
        s_penetrationProbeCollider.enabled = true;
        return s_penetrationProbeCollider;
    }

    private static void SetupPenetrationProbeTransform(
        CapsuleCollider probe,
        Vector3 pivotWorld,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        float radius) {
        var h = motor.EffectiveHeight;
        probe.direction = 1; // Y
        probe.radius = radius;
        probe.height = Mathf.Max(h, radius * 2f + 0.02f);
        probe.center = new Vector3(0f, -pivotToFootOffset + probe.height * 0.5f, 0f);
        probe.transform.SetPositionAndRotation(pivotWorld, Quaternion.identity);
    }
}
