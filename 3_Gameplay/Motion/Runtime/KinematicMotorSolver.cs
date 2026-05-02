using UnityEngine;

/// <summary>
/// 纯 CapsuleSweep + Collide-and-Slide 约束求解内核（不写 MonoBehaviour）。
/// Why: Phase 1 连续碰撞离散化管线；可被 Player、Monster Motor、测试替身复用。
/// </summary>
public static class KinematicMotorSolver
{
    private const float EpsilonSq = 1e-8f;
    private static float s_nextLowerHemisphereLogTime;

    /// <summary>供地面吸附等使用的非分配重叠缓冲（调用方负责层与排除自身碰撞体）。</summary>
    public const int DefaultOverlapBufferSize = 16;

    /// <summary>
    /// 以角色 Pivot 为世界原点，求解本帧允许的位移矢量（三维一体，含下落）。
    /// </summary>
    /// <param name="applyGroundedSlideDownwardLock">上一帧接地且策略允许时，禁止滑动迭代产生向下剩余位移（防地陷）。</param>
    public static Vector3 SolveDisplacementFromPivot(
        Vector3 worldPivot,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        Vector3 worldDisplacement,
        LayerMask obstacleLayers,
        bool applyGroundedSlideDownwardLock = false)
    {
        if (motor == null || worldDisplacement.sqrMagnitude < EpsilonSq)
        {
            return Vector3.zero;
        }

        var maxStep = Mathf.Max(0.01f, motor.Radius * 0.85f);
        var dispSq = worldDisplacement.sqrMagnitude;
        var dispMag = Mathf.Sqrt(dispSq);
        var subSteps = Mathf.CeilToInt(dispMag / maxStep);
        subSteps = Mathf.Clamp(subSteps, 1, Mathf.Max(1, motor.MaxSubSteps));

        var deltaTotal = Vector3.zero;
        var cursorPivot = worldPivot;

        for (var s = 0; s < subSteps; s++)
        {
            var step = worldDisplacement * (1f / subSteps);
            var partial = SolveSubStepSweep(cursorPivot, pivotToFootOffset, motor, step, obstacleLayers,
                applyGroundedSlideDownwardLock);
            deltaTotal += partial;
            cursorPivot += partial;
        }

        return deltaTotal;
    }

    private static Vector3 SolveSubStepSweep(
        Vector3 worldPivot,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        Vector3 displacement,
        LayerMask obstacleLayers,
        bool applyGroundedSlideDownwardLock)
    {
        if (displacement.sqrMagnitude < EpsilonSq)
        {
            return Vector3.zero;
        }

        var skin = Mathf.Max(0.0005f, motor.SkinWidth);
        var iterations = Mathf.Max(1, motor.MaxSlideIterations);
        var remaining = displacement;
        var accumulated = Vector3.zero;

        Vector3 lastNormal = default;
        var hadPreviousNormal = false;

        var castRadius = Mathf.Max(0.01f, motor.Radius - skin * 0.5f);

        for (var iter = 0; iter < iterations; iter++)
        {
            if (remaining.sqrMagnitude < EpsilonSq)
            {
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
                    QueryTriggerInteraction.Ignore))
            {
                accumulated += remaining;
                break;
            }

            var slideNormal = ResolveContactNormalForSlide(motor, p1, in hit, out var normalFiltered);

            var approachNormalDot = Vector3.Dot(dir, slideNormal);
            if (approachNormalDot > motor.VelocityAgainstNormalRejectDot)
            {
                DrawSweepHitDiagnostics(motor, in hit, remaining, rejectedAsNonBlocking: true, in slideNormal,
                    normalFiltered);
                MaybeFlashObstacle(motor, in hit);
                accumulated += remaining;
                break;
            }

            var safeAlong = Mathf.Max(0f, hit.distance - skin);
            accumulated += dir * safeAlong;

            remaining = remaining - dir * safeAlong;

            if (remaining.sqrMagnitude < EpsilonSq)
            {
                DrawSweepHitDiagnostics(motor, in hit, Vector3.zero, rejectedAsNonBlocking: false, in slideNormal,
                    normalFiltered);
                MaybeFlashObstacle(motor, in hit);
                break;
            }

            var slid = SlideOrCrevice(remaining, slideNormal, hadPreviousNormal, lastNormal,
                motor.CreviceNormalDotThreshold, iter);
            remaining = slid;

            if (applyGroundedSlideDownwardLock
                && motor.ClampGroundedSlideRemainingY
                && remaining.y < 0f)
            {
                remaining.y = 0f;
            }

            DrawSweepHitDiagnostics(motor, in hit, remaining, rejectedAsNonBlocking: false, in slideNormal,
                normalFiltered);
            MaybeFlashObstacle(motor, in hit);

            hadPreviousNormal = true;
            lastNormal = slideNormal;
        }

        return accumulated;
    }

    private static float SweepDebugSeconds(MotorSettingsSO motor)
    {
        return motor != null && motor.SweepHitDebugLifetime > 0.0001f ? motor.SweepHitDebugLifetime : 0f;
    }

    private static void DrawSweepHitDiagnostics(MotorSettingsSO motor, in RaycastHit hit, Vector3 slideVector,
        bool rejectedAsNonBlocking, in Vector3 resolvedSlideNormal, bool usedLowerHemisphereFilter)
    {
        if (motor == null || !motor.DrawSweepHitsInSceneView)
        {
            return;
        }

        var t = SweepDebugSeconds(motor);
        var pointColor = rejectedAsNonBlocking ? motor.SweepIgnoredHitColor : motor.SweepHitPointColor;
        var cross = Mathf.Max(0.02f, motor.SweepCrossHalfExtent);
        DrawCross(hit.point, cross, pointColor, t);
        if (!rejectedAsNonBlocking)
        {
            Debug.DrawRay(hit.point, hit.normal * motor.SweepHitNormalDrawLength, motor.SweepHitNormalColor, t);
        }

        if (usedLowerHemisphereFilter && motor.DrawLowerHemisphereNormalFilter)
        {
            Debug.DrawRay(hit.point, hit.normal * motor.SweepLowerHemisphereOriginalDrawLength,
                motor.LowerHemisphereOriginalNormalColor, t);
            Debug.DrawRay(hit.point, resolvedSlideNormal * motor.SweepLowerHemisphereFilteredDrawLength,
                motor.LowerHemisphereFilteredNormalColor, t);
        }

        if (slideVector.sqrMagnitude > EpsilonSq)
        {
            var sm = slideVector.sqrMagnitude;
            var inv = motor.SweepSlideDrawLength / Mathf.Sqrt(sm);
            Debug.DrawRay(hit.point, slideVector * inv, motor.SweepSlideDirectionColor, t);
        }
    }

    /// <summary>
    /// 低台阶：陡坡法线在底球区域剥掉 Y；不再依赖 XZ「南极」半径（WA/WD 易误触盲区），改以命中点相对底球球心高度 + 法线陡度。
    /// </summary>
    private static Vector3 ResolveContactNormalForSlide(
        MotorSettingsSO motor,
        Vector3 bottomSphereCenter,
        in RaycastHit hit,
        out bool appliedLowerHemisphereFilter)
    {
        appliedLowerHemisphereFilter = false;
        var n = hit.normal.normalized;

        // ── Pass 0：墙面消毒（无条件优先于 LowerHemisphere 过滤）─────────────────
        // 网格顶点的浮点漂移会让"理论垂直墙面"返回类似 (1, -0.005, 0.001) 的法线。
        // 在跳跃（V.y>0）时撞这种墙，Vector3.ProjectOnPlane(V, n) 会把上行动能折射成下行动能，
        // 表现为"沿墙跳跃 → 瞬间被压进地里"。强制把这类近垂直法线的 Y 分量归零并归一化即可根治。
        if (motor != null && motor.SanitizeNearVerticalWallNormals
            && Mathf.Abs(n.y) < motor.WallSanitizeNormalYThreshold)
        {
            var nxz0 = new Vector3(n.x, 0f, n.z);
            var nxz0Sq = nxz0.sqrMagnitude;
            if (nxz0Sq > 1e-10f)
            {
                n = nxz0 * (1f / Mathf.Sqrt(nxz0Sq));
            }
        }

        if (motor == null || !motor.EnableLowerHemisphereNormalFilter)
        {
            return n;
        }

        if (motor.LowerHemisphereOnlyWhenContactBelowBottomCenter
            && hit.point.y > bottomSphereCenter.y + motor.LowerHemisphereBottomCenterYSlop)
        {
            return n;
        }

        var toContact = hit.point - bottomSphereCenter;
        var horizSq = toContact.x * toContact.x + toContact.z * toContact.z;
        var poleAllow = motor.LowerHemispherePoleRadiusFraction > 0.0001f
            ? Mathf.Max(0.001f, motor.Radius * motor.LowerHemispherePoleRadiusFraction)
            : 0f;
        if (poleAllow > 0.0005f && horizSq <= poleAllow * poleAllow)
        {
            return n;
        }

        var steep = motor.IsSlopeTooSteep(n);
        var shallowSideScrape = motor.LowerHemisphereFlattenIfGroundNormalYBelow > 0.001f
                                && n.y > 0.0001f
                                && n.y < motor.LowerHemisphereFlattenIfGroundNormalYBelow;
        if (!steep && !shallowSideScrape)
        {
            return n;
        }

        var nxz = new Vector3(n.x, 0f, n.z);
        var nxzSq = nxz.sqrMagnitude;
        if (nxzSq < 1e-10f)
        {
            return n;
        }

        nxz *= 1f / Mathf.Sqrt(nxzSq);
        appliedLowerHemisphereFilter = true;

        if (motor.LogLowerHemisphereNormalFilters && Time.unscaledTime >= s_nextLowerHemisphereLogTime)
        {
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
        Collider[] overlapBuffer)
    {
        if (motor == null || overlapBuffer == null || overlapBuffer.Length == 0)
        {
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

        for (var i = 0; i < n; i++)
        {
            var c = overlapBuffer[i];
            if (c == null)
            {
                continue;
            }

            if (ignoreRoot != null && c.transform != null
                                   && (c.transform == ignoreRoot || c.transform.IsChildOf(ignoreRoot)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void DrawCross(Vector3 center, float halfExtent, Color color, float duration)
    {
        Debug.DrawLine(center - Vector3.right * halfExtent, center + Vector3.right * halfExtent, color,
            duration);
        Debug.DrawLine(center - Vector3.up * halfExtent, center + Vector3.up * halfExtent, color,
            duration);
        Debug.DrawLine(center - Vector3.forward * halfExtent, center + Vector3.forward * halfExtent, color,
            duration);
    }

    private static void MaybeFlashObstacle(MotorSettingsSO motor, in RaycastHit hit)
    {
        if (motor == null || !motor.FlashObstacleColliderOnSweepHit || hit.collider == null)
        {
            return;
        }

        CollisionDebugFlasher flasher;
        if (motor.AutoAddCollisionFlasherOnObstacleIfMissing)
        {
            flasher = CollisionDebugFlasher.GetOrCreate(hit.collider);
        }
        else
        {
            flasher = hit.collider.GetComponent<CollisionDebugFlasher>();
        }

        var flashRgb = motor.SweepHitPointColor;
        flasher?.Flash(new Color(flashRgb.r, flashRgb.g, flashRgb.b, 1f), motor.SweepFlashHoldSeconds,
            motor.SweepFlashFadeSeconds);
    }

    private static Vector3 SlideOrCrevice(
        Vector3 remaining,
        Vector3 currentNormal,
        bool hadPrev,
        Vector3 lastNormal,
        float creviceDotThresh,
        int iterationIndex)
    {
        if (hadPrev
            && iterationIndex > 0
            && Vector3.Dot(currentNormal.normalized, lastNormal.normalized) < creviceDotThresh)
        {
            var seam = Vector3.Cross(lastNormal, currentNormal);
            if (seam.sqrMagnitude > 1e-10f)
            {
                seam.Normalize();
                var alongSeam = Vector3.Dot(remaining, seam);
                var projected = seam * alongSeam;
                if (projected.sqrMagnitude > EpsilonSq)
                {
                    return projected;
                }
            }
        }

        return Vector3.ProjectOnPlane(remaining, currentNormal.normalized);
    }

    internal static void GetCapsuleWorld(
        Vector3 pivotWorld,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        out Vector3 bottomSphere,
        out Vector3 topSphere,
        out float radius)
    {
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
        out RaycastHit bestHit)
    {
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
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 瞬移到目标 Pivot 前先向下找支撑面；可选按 <see cref="MotorSettingsSO.MaxSlopeAngle"/> 拒绝凸角/墙顶。
    /// </summary>
    /// <param name="motorForWalkableSlopeGate">非空且命中法线过陡时返回 false，避免 Teleport 锁在钝角尖上。</param>
    public static bool ResolvePivotHeightFromAbove(
        float pivotDesiredX,
        float pivotDesiredZ,
        float referenceHeightBelowRayStart,
        float rayStartYOffset,
        float maxRayDistance,
        float pivotToFootOffset,
        LayerMask groundLayers,
        MotorSettingsSO motorForWalkableSlopeGate,
        out float resolvedPivotY)
    {
        resolvedPivotY = 0f;
        if (maxRayDistance <= 0f)
        {
            return false;
        }

        var origin = new Vector3(pivotDesiredX, referenceHeightBelowRayStart + rayStartYOffset, pivotDesiredZ);
        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out var hit,
                maxRayDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (motorForWalkableSlopeGate != null && motorForWalkableSlopeGate.IsSlopeTooSteep(hit.normal))
        {
            return false;
        }

        resolvedPivotY = hit.point.y + pivotToFootOffset;
        return true;
    }

    /// <summary>
    /// 将位移投影到斜坡切平面（保持模长在平面上的等价前进感，由位移尺度近似）。
    /// </summary>
    public static Vector3 ProjectDisplacementOntoGroundPlaneIfWalkable(Vector3 displacement, Vector3 groundNormal,
        MotorSettingsSO motor)
    {
        if (motor == null || !motor.EnablesSlopeProjection || displacement.sqrMagnitude < EpsilonSq)
        {
            return displacement;
        }

        var n = groundNormal.normalized;
        if (motor.IsSlopeTooSteep(n))
        {
            return displacement;
        }

        return Vector3.ProjectOnPlane(displacement, n);
    }
}
