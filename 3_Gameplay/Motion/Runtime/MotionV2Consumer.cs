using UnityEngine;

/// <summary>
/// 174.2 — Player / Motor 层消费 V2 通道的统一 helper。
/// <para>暴露的接入点（Player 层调用）：</para>
/// <para>  · <see cref="ComposeFinalYaw"/> — 起手 Yaw + 玩家输入按 FacingInputWeight 加权</para>
/// <para>  · <see cref="ComposeFinalMoveDelta"/> — XYZ 曲线位移 + 玩家输入按 MoveInputWeight 叠加</para>
/// <para>  · <see cref="ComposeFinalPositionWithRm"/> — 曲线位移与 Animator Root Motion 按 RmBlend 混合</para>
/// <para>  · <see cref="ApplyHitstopToDeltaTime"/> — 按 HitstopMultiplier 调整 motionT 推进速率</para>
/// <para>本类纯静态、无状态；调用方按需读取 MotionExecutor.LastContribution。</para>
/// </summary>
public static class MotionV2Consumer
{
    /// <summary>
    /// 合成 Yaw：起手 Yaw + 玩家输入按 FacingInputWeight 加权（210.5 后位移 Yaw 曲线已移除）。
    /// </summary>
    /// <param name="startYawDegrees">动作起手时角色 Yaw（度，世界空间）。</param>
    /// <param name="contribution">MotionExecutor.LastContribution。</param>
    /// <param name="playerInputYawDegrees">玩家当前输入对应的目标 Yaw（度）。</param>
    /// <returns>本帧合成的最终 Yaw（度）。</returns>
    public static float ComposeFinalYaw(
        float startYawDegrees,
        in MotionContribution contribution,
        float playerInputYawDegrees)
    {
        var curveYaw = startYawDegrees;

        var facingW = contribution.ResolvedFacingInputWeight;
        if (facingW <= 0.0001f)
        {
            return curveYaw;
        }

        if (facingW >= 0.9999f)
        {
            return playerInputYawDegrees;
        }

        return Mathf.LerpAngle(curveYaw, playerInputYawDegrees, facingW);
    }

    /// <summary>
    /// 合成本帧最终位移：曲线 LocalDelta + 玩家输入按 MoveInputWeight 叠加（不替换）。
    /// </summary>
    public static Vector3 ComposeFinalMoveDelta(
        Vector3 curveWorldDelta,
        in MotionContribution contribution,
        Vector3 playerInputWorldDelta)
    {
        var moveW = contribution.ResolvedMoveInputWeight;
        if (moveW <= 0.0001f)
        {
            return curveWorldDelta;
        }
        return curveWorldDelta + playerInputWorldDelta * moveW;
    }

    /// <summary>
    /// 合成本帧最终位移：曲线 + Animator Root Motion 按 RootMotionBlend 混合（Lerp）。
    /// rmBlend=0 取曲线；rmBlend=1 取 RM；中间值线性插值。
    /// </summary>
    public static Vector3 ComposeFinalPositionWithRm(
        Vector3 curveWorldDelta,
        in MotionContribution contribution,
        Vector3 animatorRootDelta)
    {
        var blend = Mathf.Clamp01(contribution.RootMotionBlend);
        if (blend <= 0.0001f)
        {
            return curveWorldDelta;
        }
        if (blend >= 0.9999f)
        {
            return animatorRootDelta;
        }
        return Vector3.Lerp(curveWorldDelta, animatorRootDelta, blend);
    }

    /// <summary>
    /// 按 HitstopMultiplier 调整 deltaTime（响应外部 Hitstop 事件）。
    /// hitstopFactor: 来自 Combat 层的当前 hitstop 强度 [0,1]（0=无 hitstop / 1=完全冻结）。
    /// 返回值用于推进 motionT 与 Animator 速度。
    /// </summary>
    public static float ApplyHitstopToDeltaTime(
        float deltaTime,
        in MotionContribution contribution,
        float hitstopFactor)
    {
        if (hitstopFactor <= 0.0001f)
        {
            return deltaTime;
        }
        var multiplier = contribution.ResolvedHitstopMultiplier;
        var blockFactor = Mathf.Clamp01(hitstopFactor * multiplier);
        return deltaTime * (1f - blockFactor);
    }

    /// <summary>
    /// 174.2 W8 — 锁敌追踪：在原 basis 与"朝向目标的 basis"之间按 TargetTrackingWeight Slerp。
    /// 仅 MotionSpace=LockTarget 时由调用方使用。
    /// </summary>
    public static Vector3 BlendTargetTracking(
        Vector3 startForward,
        Vector3 toTargetForward,
        in MotionContribution contribution)
    {
        var w = Mathf.Clamp01(contribution.TargetTrackingWeight);
        if (w <= 0.0001f)
        {
            return startForward;
        }
        if (w >= 0.9999f)
        {
            return toTargetForward.sqrMagnitude > 0.0001f ? toTargetForward.normalized : startForward;
        }
        return Vector3.Slerp(startForward, toTargetForward, w);
    }
}
