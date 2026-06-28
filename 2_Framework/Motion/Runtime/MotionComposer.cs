using UnityEngine;

/// <summary>
/// Motion / Gravity 汇合 — 按 MotionYAxisConfig 三权裁决垂直通道。
/// 顺序：Y Motion → Gravity（本类）→ Ground Constraint（<see cref="MotionGroundConstraint"/>）。
/// </summary>
public static class MotionComposer
{
    public static Vector3 ComposeWorldVelocity(
        in MotionContribution motion,
        in GravityContribution gravity,
        Vector3 motionWorldVelocity,
        in MotionYAxisConfig config,
        bool log = false,
        Object logContext = null)
    {
        if (!motion.IsActive)
        {
            if (gravity.IsActive)
            {
                var gOnly = new Vector3(0f, gravity.Vy, 0f);
                if (log)
                {
                    MotionXYZDebug.Log(logContext, MotionXYZDebug.CatComposer,
                        $"no motion → final=(0,{gravity.Vy:F3},0)");
                }

                return gOnly;
            }

            if (log)
            {
                MotionXYZDebug.Log(logContext, MotionXYZDebug.CatComposer, "no contributions → final=zero");
            }

            return Vector3.zero;
        }

        var motionY = config.YMotion switch
        {
            YMotionMode.Curve => motionWorldVelocity.y,
            YMotionMode.GroundTargeted => motionWorldVelocity.y,
            _ => 0f,
        };

        // 174.2 — GravityWeight：Curve 模式下加权 gravity.Vy；V1 默认 1f 不改行为。
        var finalY = ComposeVertical(motionY, in gravity, config, motion.ResolvedGravityWeight);

        var final = new Vector3(motionWorldVelocity.x, finalY, motionWorldVelocity.z);
        if (log)
        {
            MotionXYZDebug.Log(logContext, MotionXYZDebug.CatComposer,
                $"yMotion={config.YMotion} gravity={config.Gravity} ground={config.GroundConstraint} " +
                $"motionY={motionY:F3} finalY={finalY:F3}");
        }

        return final;
    }

    static float ComposeVertical(
        float motionY,
        in GravityContribution gravity,
        in MotionYAxisConfig config,
        float gravityWeight = 1f)
    {
        switch (config.Gravity)
        {
            case GravityMode.SuspendGravity:
                return motionY;

            case GravityMode.AdditiveGravity:
                if (!gravity.IsActive)
                {
                    return motionY;
                }

                return motionY + gravity.Vy * gravityWeight;

            case GravityMode.UseGravity:
            default:
                if (!gravity.IsActive)
                {
                    return motionY;
                }

                if (config.YMotion == YMotionMode.None)
                {
                    return gravity.Vy * gravityWeight;
                }

                return motionY + gravity.Vy * gravityWeight;
        }
    }

    [System.Obsolete("Use ComposeWorldVelocity with MotionYAxisConfig")]
    public static Vector3 ComposeWorldVelocity(
        in MotionContribution motion,
        in GravityContribution gravity,
        Vector3 motionWorldVelocity,
        bool log = false,
        Object logContext = null) =>
        ComposeWorldVelocity(in motion, in gravity, motionWorldVelocity, motion.YAxisConfig, log, logContext);
}
