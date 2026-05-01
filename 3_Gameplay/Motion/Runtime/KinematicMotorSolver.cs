using UnityEngine;

/// <summary>
/// 纯 CapsuleSweep + Collide-and-Slide 约束求解内核（不写 MonoBehaviour）。
/// Why: Phase 1 连续碰撞离散化管线；可被 Player、Monster Motor、测试替身复用。
/// </summary>
public static class KinematicMotorSolver
{
    private const float EpsilonSq = 1e-8f;

    /// <summary>
    /// 以角色 Pivot 为世界原点，求解本帧允许的位移矢量（三维一体，含下落）。
    /// </summary>
    public static Vector3 SolveDisplacementFromPivot(
        Vector3 worldPivot,
        float pivotToFootOffset,
        MotorSettingsSO motor,
        Vector3 worldDisplacement,
        LayerMask obstacleLayers)
    {
        if (motor == null || worldDisplacement.sqrMagnitude < EpsilonSq)
        {
            return Vector3.zero;
        }

        var maxStep = Mathf.Max(0.01f, motor.Radius * 0.85f);
        var dispMag = worldDisplacement.magnitude;
        var subSteps = Mathf.CeilToInt(dispMag / maxStep);
        subSteps = Mathf.Clamp(subSteps, 1, Mathf.Max(1, motor.MaxSubSteps));

        var deltaTotal = Vector3.zero;
        var cursorPivot = worldPivot;

        for (var s = 0; s < subSteps; s++)
        {
            var step = worldDisplacement * (1f / subSteps);
            var partial = SolveSubStepSweep(cursorPivot, pivotToFootOffset, motor, step, obstacleLayers);
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
        LayerMask obstacleLayers)
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

            var dist = remaining.magnitude;
            var dir = dist > 1e-6f ? remaining / dist : Vector3.forward;

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

            var approachNormalDot = Vector3.Dot(dir, hit.normal.normalized);
            if (approachNormalDot > motor.VelocityAgainstNormalRejectDot)
            {
                DrawSweepHitDiagnostics(motor, in hit, remaining, rejectedAsNonBlocking: true);
                MaybeFlashObstacle(motor, in hit);
                accumulated += remaining;
                break;
            }

            var safeAlong = Mathf.Max(0f, hit.distance - skin);
            accumulated += dir * safeAlong;

            remaining = remaining - dir * safeAlong;

            if (remaining.sqrMagnitude < EpsilonSq)
            {
                DrawSweepHitDiagnostics(motor, in hit, Vector3.zero, rejectedAsNonBlocking: false);
                MaybeFlashObstacle(motor, in hit);
                break;
            }

            var slid = SlideOrCrevice(remaining, hit.normal, hadPreviousNormal, lastNormal,
                motor.CreviceNormalDotThreshold, iter);
            DrawSweepHitDiagnostics(motor, in hit, slid, rejectedAsNonBlocking: false);
            MaybeFlashObstacle(motor, in hit);

            remaining = slid;

            hadPreviousNormal = true;
            lastNormal = hit.normal;
        }

        return accumulated;
    }

    private static float SweepDebugSeconds(MotorSettingsSO motor)
    {
        return motor != null && motor.SweepHitDebugLifetime > 0.0001f ? motor.SweepHitDebugLifetime : 0f;
    }

    private static void DrawSweepHitDiagnostics(MotorSettingsSO motor, in RaycastHit hit, Vector3 slideVector,
        bool rejectedAsNonBlocking)
    {
        if (motor == null || !motor.DrawSweepHitsInSceneView)
        {
            return;
        }

        var t = SweepDebugSeconds(motor);
        var pointColor = rejectedAsNonBlocking ? motor.SweepIgnoredHitColor : motor.SweepHitPointColor;
        DrawCross(hit.point, 0.08f, pointColor, t);
        if (!rejectedAsNonBlocking)
        {
            Debug.DrawRay(hit.point, hit.normal * 0.5f, motor.SweepHitNormalColor, t);
        }

        if (slideVector.sqrMagnitude > EpsilonSq)
        {
            Debug.DrawRay(hit.point, slideVector.normalized * 0.42f, motor.SweepSlideDirectionColor, t);
        }
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
        flasher?.Flash(new Color(flashRgb.r, flashRgb.g, flashRgb.b, 1f), 0.04f, 0.22f);
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
