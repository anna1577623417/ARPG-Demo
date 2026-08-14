using UnityEngine;

/// <summary>
/// 234.6 — InheritPhysics 恒定减速度纯函数积分器。无 Unity 生命周期，不写 Motor。
/// 默认 <c>a = V_ref² / (2 D_ref)</c>，<c>D(v) = v² / (2a)</c>。
/// </summary>
public static class StopIntegrator
{
    public const float DefaultSpeedEpsilon = 0.05f;
    public const float DefaultMicroStopSpeed = 0.35f;

    public readonly struct Step
    {
        public float PreviousSpeed { get; }
        public float NewSpeed { get; }
        public float Distance { get; }
        public bool PhysicsComplete { get; }

        public Step(float previousSpeed, float newSpeed, float distance, bool physicsComplete)
        {
            PreviousSpeed = previousSpeed;
            NewSpeed = newSpeed;
            Distance = distance;
            PhysicsComplete = physicsComplete;
        }
    }

    public static bool TryDeriveDeceleration(float referenceGaitSpeed, float fullSpeedStopDistance, out float deceleration)
    {
        deceleration = 0f;
        if (referenceGaitSpeed <= 0.0001f || fullSpeedStopDistance <= 0.0001f)
        {
            return false;
        }

        deceleration = (referenceGaitSpeed * referenceGaitSpeed) / (2f * fullSpeedStopDistance);
        return deceleration > 0.0001f;
    }

    /// <summary>
    /// 234.6.3 — 点按蠕动：丢掉入场速度，用固定 D 铺满 T。
    /// <c>v0 = 2D/T</c>，<c>a = 2D/T²</c>。
    /// </summary>
    public static bool TryDeriveTapCreep(
        float distance,
        float duration,
        out float startSpeed,
        out float deceleration)
    {
        startSpeed = 0f;
        deceleration = 0f;
        var d = Mathf.Max(0f, distance);
        var t = Mathf.Max(0.001f, duration);
        if (d <= 0.0001f)
        {
            return false;
        }

        startSpeed = 2f * d / t;
        deceleration = startSpeed / t;
        return deceleration > 0.0001f && startSpeed > 0.0001f;
    }

    public static float PredictDistance(float entrySpeed, float deceleration)
    {
        var v = Mathf.Max(0f, entrySpeed);
        if (deceleration <= 0.0001f || v <= 0f)
        {
            return 0f;
        }

        return (v * v) / (2f * deceleration);
    }

    public static float PredictDuration(float entrySpeed, float deceleration)
    {
        var v = Mathf.Max(0f, entrySpeed);
        if (deceleration <= 0.0001f || v <= 0f)
        {
            return 0f;
        }

        return v / deceleration;
    }

    public static Step Advance(float speed, float deceleration, float deltaTime, float epsilon = DefaultSpeedEpsilon)
    {
        var v0 = Mathf.Max(0f, speed);
        var dt = Mathf.Max(0f, deltaTime);
        var eps = Mathf.Max(0.0001f, epsilon);
        if (v0 <= eps)
        {
            return new Step(v0, 0f, 0f, true);
        }

        if (deceleration <= 0.0001f || dt <= 0f)
        {
            return new Step(v0, v0, 0f, false);
        }

        var v1 = v0 - deceleration * dt;
        if (v1 <= eps)
        {
            var remaining = PredictDistance(v0, deceleration);
            return new Step(v0, 0f, remaining, true);
        }

        var ds = 0.5f * (v0 + v1) * dt;
        return new Step(v0, v1, ds, false);
    }

    public static IntegratedStopPlan BuildConstantDecel(
        float entrySpeed,
        Vector3 stopDirection,
        float referenceGaitSpeed,
        float fullSpeedStopDistance,
        StopSessionTier tier,
        bool derivedFromLegacyMaxDistance,
        StopCurveSemantic curveSemantic = StopCurveSemantic.PresentationRhythm)
    {
        if (!TryDeriveDeceleration(referenceGaitSpeed, fullSpeedStopDistance, out var a))
        {
            return IntegratedStopPlan.Disabled;
        }

        var v = Mathf.Max(0f, entrySpeed);
        return new IntegratedStopPlan(
            StopBrakingMode.ConstantDeceleration,
            v,
            stopDirection,
            a,
            referenceGaitSpeed,
            fullSpeedStopDistance,
            PredictDuration(v, a),
            PredictDistance(v, a),
            curveSemantic,
            tier,
            derivedFromLegacyMaxDistance);
    }
}
