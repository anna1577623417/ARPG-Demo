using UnityEngine;

/// <summary>
/// 184.1 W6 — Turn 表现意图构建（纯函数；阈值权威仍在 <see cref="LocomotionTuningSO"/>）。
/// <para><see cref="TurnResolver"/> 只负责时序/锁定；角度→Turn90/180 分类集中在此。</para>
/// </summary>
public static class TurnIntentBuilder
{
    public static bool TryClassify(
        float signedAngleDeg,
        float triggerThreshold,
        float type180Threshold,
        out TurnType type,
        out sbyte direction)
    {
        type = TurnType.None;
        direction = 0;
        var absAngle = Mathf.Abs(signedAngleDeg);
        if (absAngle < triggerThreshold)
        {
            return false;
        }

        direction = (sbyte)(signedAngleDeg > 0f ? 1 : -1);
        type = absAngle >= type180Threshold ? TurnType.Turn180 : TurnType.Turn90;
        return true;
    }

    public static TurnInfo Create(
        bool isTurning,
        TurnType type,
        sbyte direction,
        float absAngle,
        float signedAngle)
    {
        return new TurnInfo
        {
            IsTurning = isTurning,
            Type = type,
            Direction = direction,
            Angle = absAngle,
            SignedAngle = signedAngle,
        };
    }

    public static TurnInfo CreateNonTurning(float absAngle, float signedAngle)
    {
        return Create(false, TurnType.None, 0, absAngle, signedAngle);
    }
}
