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

/// <summary>
/// 235.2 — Gameplay 已即时改向后，供 Presentation 单次消费的补偿性 Turn Cue。
/// Generation 是消费幂等键；Type=None 表示显著方向边沿只取消旧补偿、不播放新 Turn。
/// </summary>
public readonly struct TurnCompensationCue
{
    public readonly uint Generation;
    public readonly TurnType Type;
    public readonly sbyte Direction;
    public readonly float AbsAngle;
    public readonly float SignedAngle;
    public readonly int SourceFrame;
    public readonly float PresentationLeaseSeconds;

    public TurnCompensationCue(
        uint generation,
        TurnType type,
        sbyte direction,
        float absAngle,
        float signedAngle,
        int sourceFrame)
        : this(generation, type, direction, absAngle, signedAngle, sourceFrame, 0f)
    {
    }

    public TurnCompensationCue(
        uint generation,
        TurnType type,
        sbyte direction,
        float absAngle,
        float signedAngle,
        int sourceFrame,
        float presentationLeaseSeconds)
    {
        Generation = generation;
        Type = type;
        Direction = direction;
        AbsAngle = absAngle;
        SignedAngle = signedAngle;
        SourceFrame = sourceFrame;
        PresentationLeaseSeconds = Mathf.Max(0f, presentationLeaseSeconds);
    }

    public bool IsValid => Generation != 0;
    public bool IsTurning => IsValid && Type != TurnType.None && Direction != 0;

    public TurnInfo ToTurnInfo() => TurnIntentBuilder.Create(
        IsTurning,
        Type,
        Direction,
        AbsAngle,
        SignedAngle);
}

/// <summary>235 — 无状态纯函数；只做 pre-snap 角度边沿与 90/180 分类，不写任何 Transform/Motor。</summary>
public static class TurnCompensationResolver
{
    public const float DefaultSignificantEdgeDeg = 35f;
    public const int DefaultCueMaxAgeFrames = 3;

    public static bool TryResolve(
        Vector3 preLogicForward,
        Vector3 commandDirection,
        bool enabled,
        bool isLockOn,
        bool directionalCommitted,
        float turn90ThresholdDeg,
        float turn180ThresholdDeg,
        uint generation,
        int sourceFrame,
        out TurnCompensationCue cue)
        => TryResolve(
            preLogicForward,
            commandDirection,
            enabled,
            isLockOn,
            directionalCommitted,
            turn90ThresholdDeg,
            turn180ThresholdDeg,
            generation,
            sourceFrame,
            0.16f,
            0.24f,
            out cue);

    public static bool TryResolve(
        Vector3 preLogicForward,
        Vector3 commandDirection,
        bool enabled,
        bool isLockOn,
        bool directionalCommitted,
        float turn90ThresholdDeg,
        float turn180ThresholdDeg,
        uint generation,
        int sourceFrame,
        float turn90PresentationLeaseSeconds,
        float turn180PresentationLeaseSeconds,
        out TurnCompensationCue cue)
    {
        cue = default;
        if (!enabled || isLockOn || directionalCommitted || generation == 0)
        {
            return false;
        }

        preLogicForward.y = 0f;
        commandDirection.y = 0f;
        if (preLogicForward.sqrMagnitude < 0.0001f || commandDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        var signed = Vector3.SignedAngle(
            preLogicForward.normalized,
            commandDirection.normalized,
            Vector3.up);
        var abs = Mathf.Abs(signed);
        if (abs < DefaultSignificantEdgeDeg)
        {
            return false;
        }

        var type = TurnType.None;
        sbyte direction = 0;
        TurnIntentBuilder.TryClassify(
            signed,
            Mathf.Clamp(turn90ThresholdDeg, DefaultSignificantEdgeDeg, 180f),
            Mathf.Clamp(turn180ThresholdDeg, turn90ThresholdDeg, 180f),
            out type,
            out direction);

        cue = new TurnCompensationCue(
            generation,
            type,
            direction,
            abs,
            signed,
            sourceFrame,
            type == TurnType.Turn180
                ? Mathf.Max(0f, turn180PresentationLeaseSeconds)
                : type == TurnType.Turn90
                    ? Mathf.Max(0f, turn90PresentationLeaseSeconds)
                    : 0f);
        return true;
    }

    public static float ResolveLeaseSeconds(float clipLength, float playbackSpeed, float completionRatio)
    {
        if (clipLength <= 0f)
        {
            return 0f;
        }

        var speed = Mathf.Max(0.01f, playbackSpeed);
        return clipLength / speed * Mathf.Clamp(completionRatio, 0.15f, 1f);
    }

    /// <summary>
    /// 235.2：补偿 Cue 只描述输入边沿附近的姿态过渡。若 Recovery/Action 让它延迟多帧，
    /// Presentation 必须丢弃，不能在 Gameplay 已完成转向后补播一次“迟到的第二转”。
    /// </summary>
    public static bool IsCueFresh(
        in TurnCompensationCue cue,
        int currentFrame,
        int maxAgeFrames = DefaultCueMaxAgeFrames)
    {
        if (!cue.IsValid || currentFrame < cue.SourceFrame)
        {
            return false;
        }

        return currentFrame - cue.SourceFrame <= Mathf.Max(0, maxAgeFrames);
    }

    /// <summary>235.2：让指定 normalized pose 区间在有限表现 Lease 内播完。</summary>
    public static float ResolvePlaybackSpeedForLease(
        float clipLength,
        float authoredPlaybackSpeed,
        float completionRatio,
        float presentationLeaseSeconds)
    {
        if (clipLength <= 0f || presentationLeaseSeconds <= 0.0001f)
        {
            return Mathf.Max(0.01f, authoredPlaybackSpeed);
        }

        var desired = clipLength * Mathf.Clamp(completionRatio, 0.15f, 1f)
                      / presentationLeaseSeconds;
        return Mathf.Clamp(desired, 0.05f, 8f);
    }
}
