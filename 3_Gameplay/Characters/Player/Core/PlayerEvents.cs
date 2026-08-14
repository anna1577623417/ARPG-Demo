using UnityEngine;

/// <summary>
/// 玩家展示/反馈事件（readonly struct + IGameEvent）。
/// 仅用于动画、音效、UI 等旁路通知；不参与输入—状态机—移动的确定性控制流。
/// </summary>

// ─── 攻击 ───

public readonly struct PlayerAttackStartedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerAttackStartedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

public readonly struct PlayerAttackEndedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerAttackEndedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

// ─── 跳跃 ───

public readonly struct PlayerJumpEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;
    public readonly AirCycleSnapshot AirCycle;
    public readonly RuntimeStepStamp Step;

    public PlayerJumpEvent(int playerInstanceId, string playerName)
        : this(playerInstanceId, playerName, default, default)
    {
    }

    public PlayerJumpEvent(
        int playerInstanceId,
        string playerName,
        in AirCycleSnapshot airCycle,
        in RuntimeStepStamp step)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
        AirCycle = airCycle;
        Step = step;
    }
}

// ─── 跳跃空中阶段（到达最高点后进入下落）───

public readonly struct PlayerJumpAirPhaseEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;
    public readonly AirCycleSnapshot AirCycle;
    public readonly RuntimeStepStamp Step;

    public PlayerJumpAirPhaseEvent(int playerInstanceId, string playerName)
        : this(playerInstanceId, playerName, default, default)
    {
    }

    public PlayerJumpAirPhaseEvent(
        int playerInstanceId,
        string playerName,
        in AirCycleSnapshot airCycle,
        in RuntimeStepStamp step)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
        AirCycle = airCycle;
        Step = step;
    }
}

// ─── 落地 ───

public readonly struct PlayerLandedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;
    public readonly AirCycleSnapshot AirCycle;
    public readonly RuntimeStepStamp Step;

    public PlayerLandedEvent(int playerInstanceId, string playerName)
        : this(playerInstanceId, playerName, default, default)
    {
    }

    public PlayerLandedEvent(
        int playerInstanceId,
        string playerName,
        in AirCycleSnapshot airCycle,
        in RuntimeStepStamp step)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
        AirCycle = airCycle;
        Step = step;
    }
}

// ─── 闪避 ───

public readonly struct PlayerDodgeStartedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerDodgeStartedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

public readonly struct PlayerDodgeEndedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerDodgeEndedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

public readonly struct PlayerTeleportedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;
    public readonly Vector3 WorldPosition;

    public PlayerTeleportedEvent(int playerInstanceId, string playerName, Vector3 worldPosition)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
        WorldPosition = worldPosition;
    }
}

public enum TurnInterruptReason : byte
{
    External = 0,
    MovementModeChanged = 1,
    Jump = 2,
    Action = 3,
    LocomotionDiscrete = 4,
    StateExit = 5,
}

/// <summary>Gameplay turn cancellation fact. Presentation may observe it but never acknowledges it.</summary>
public readonly struct TurnPresentationInterruptedEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly RuntimeStepStamp Step;
    public readonly uint TurnGeneration;
    public readonly TurnType PreviousTurnType;
    public readonly TurnInterruptReason Reason;

    public TurnPresentationInterruptedEvent(
        int entityInstanceId,
        in RuntimeStepStamp step,
        uint turnGeneration,
        TurnType previousTurnType,
        TurnInterruptReason reason)
    {
        EntityInstanceId = entityInstanceId;
        Step = step;
        TurnGeneration = turnGeneration;
        PreviousTurnType = previousTurnType;
        Reason = reason;
    }
}

// ─── 表现层（Playables）请求：逻辑状态不直接引用 Animator ───

/// <summary>
/// Action 支柱请求播放某条动作资产；PlayerAnimController 监听后走 Playables。
/// </summary>
/// <summary>Action 时间轴触发的表现事件（SFX/VFX/HitFrame）；Presentation 层订阅。</summary>
public readonly struct ActionTimelinePresentationEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly ActionTimelineMarkerKind Kind;
    public readonly string Payload;

    public ActionTimelinePresentationEvent(int playerInstanceId, ActionTimelineMarkerKind kind, string payload)
    {
        PlayerInstanceId = playerInstanceId;
        Kind = kind;
        Payload = payload;
    }
}

/// <summary>164.1 L3 / 227.5.1：Locomotion 连续表现换片（不切 ActionState）。</summary>
public readonly struct PlayerContinuousLocomotionRequestEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly ActionDataSO Action;
    public readonly LocomotionStateId ResolvedState;
    public readonly LocomotionExecutionPolicy ExecutionPolicy;

    public PlayerContinuousLocomotionRequestEvent(
        int playerInstanceId,
        ActionDataSO action,
        LocomotionStateId resolvedState,
        LocomotionExecutionPolicy executionPolicy)
    {
        PlayerInstanceId = playerInstanceId;
        Action = action;
        ResolvedState = resolvedState;
        ExecutionPolicy = executionPolicy;
    }
}

public readonly struct PlayerActionPresentationRequestEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly GameplayIntentKind Kind;
    public readonly ActionDataSO Action;
    public readonly uint ActionLeaseVersion;
    /// <summary>非空时覆盖 Action.MainClip（164.1 L10 相位急停变体等）。</summary>
    public readonly AnimationClip PresentationClip;
    /// <summary>167.1 Segment 预留：Clip 归一化起播点。</summary>
    public readonly float NormalizedStart;
    /// <summary>≥0 时覆盖 Action.ResolveEffectiveAnimSpeed（InheritPhysics 动态倍率等）。</summary>
    public readonly float PlaybackAnimSpeedOverride;

    public PlayerActionPresentationRequestEvent(
        int playerInstanceId,
        GameplayIntentKind kind,
        ActionDataSO action,
        AnimationClip presentationClip = null,
        float normalizedStart = 0f,
        float playbackAnimSpeedOverride = -1f,
        uint actionLeaseVersion = 0)
    {
        PlayerInstanceId = playerInstanceId;
        Kind = kind;
        Action = action;
        ActionLeaseVersion = actionLeaseVersion;
        PresentationClip = presentationClip;
        NormalizedStart = normalizedStart;
        PlaybackAnimSpeedOverride = playbackAnimSpeedOverride;
    }
}

/// <summary>
/// 请求修改当前 Action 主 Clip 的 Playable 播放速度（蓄力等）；由 PlayerAnimController 应用到主 AnimationClipPlayable。
/// </summary>
public readonly struct PlayablePlaybackSpeedRequestEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly float TargetSpeed;

    public PlayablePlaybackSpeedRequestEvent(int entityInstanceId, float targetSpeed)
    {
        EntityInstanceId = entityInstanceId;
        TargetSpeed = targetSpeed;
    }
}
