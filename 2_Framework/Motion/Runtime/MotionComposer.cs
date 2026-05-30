using UnityEngine;

/// <summary>
/// Motion / Gravity 汇合 — 按 YAxisPolicy 裁决垂直通道，输出本帧世界速度意图。
/// </summary>
public static class MotionComposer
{
    public static Vector3 ComposeWorldVelocity(
        in MotionContribution motion,
        in GravityContribution gravity,
        Vector3 motionWorldVelocity,
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

        var policy = motion.YPolicy;
        Vector3 final;
        switch (policy)
        {
            case YAxisPolicy.SuspendGravity:
                final = motionWorldVelocity;
                if (log)
                {
                    MotionXYZDebug.Log(logContext, MotionXYZDebug.CatComposer,
                        $"yPolicy=SuspendGravity → useY=motion({motionWorldVelocity.y:F3})");
                }
                break;

            case YAxisPolicy.MotionControlled:
                final = motionWorldVelocity;
                if (log)
                {
                    MotionXYZDebug.Log(logContext, MotionXYZDebug.CatComposer,
                        $"yPolicy=MotionControlled → useY=motion({motionWorldVelocity.y:F3}) ignoreGravity");
                }
                break;

            case YAxisPolicy.AdditiveGravity:
                final = motionWorldVelocity;
                if (gravity.IsActive)
                {
                    final.y = motionWorldVelocity.y + gravity.Vy;
                }

                if (log)
                {
                    MotionXYZDebug.Log(logContext, MotionXYZDebug.CatComposer,
                        $"yPolicy=AdditiveGravity → useY=motion({motionWorldVelocity.y:F3})+gravity({gravity.Vy:F3})={final.y:F3}");
                }
                break;

            case YAxisPolicy.UseGravity:
            default:
                final = motionWorldVelocity;
                if (gravity.IsActive)
                {
                    final.y = gravity.Vy;
                }

                if (log)
                {
                    MotionXYZDebug.Log(logContext, MotionXYZDebug.CatComposer,
                        $"motion=({motionWorldVelocity.x:F2},{motionWorldVelocity.y:F2},{motionWorldVelocity.z:F2}) " +
                        $"gravity=(0,{gravity.Vy:F2},0) policy=UseGravity → final=({final.x:F2},{final.y:F2},{final.z:F2})");
                }
                break;
        }

        return final;
    }
}
