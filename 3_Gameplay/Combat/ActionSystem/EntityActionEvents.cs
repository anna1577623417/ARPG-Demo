using UnityEngine;

/// <summary>B2：所有实体共用的时间轴传送事件。</summary>
public readonly struct EntityTeleportedEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;
    public readonly Vector3 WorldPosition;

    public EntityTeleportedEvent(int entityInstanceId, string entityName, Vector3 worldPosition)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
        WorldPosition = worldPosition;
    }
}

/// <summary>B2：所有实体共用的时间轴表现请求。</summary>
public readonly struct EntityActionPresentationEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly ActionTimelineMarkerKind Kind;
    public readonly string Payload;

    public EntityActionPresentationEvent(int entityInstanceId, ActionTimelineMarkerKind kind, string payload)
    {
        EntityInstanceId = entityInstanceId;
        Kind = kind;
        Payload = payload;
    }
}

/// <summary>B5.2：实体 Action 开始时的表现播放请求。</summary>
public readonly struct EntityActionPlaybackRequestEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly GameplayIntentKind Kind;
    public readonly ActionDataSO Action;
    public readonly float NormalizedStart;

    public EntityActionPlaybackRequestEvent(
        int entityInstanceId,
        GameplayIntentKind kind,
        ActionDataSO action,
        float normalizedStart)
    {
        EntityInstanceId = entityInstanceId;
        Kind = kind;
        Action = action;
        NormalizedStart = normalizedStart;
    }
}

/// <summary>220.6.1 C5：命中后的 VFX/SFX 载荷表现请求。</summary>
public readonly struct EntityReactionPresentationEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly ulong SourceEventId;
    public readonly string VfxPayload;
    public readonly string SfxPayload;

    public EntityReactionPresentationEvent(
        int entityInstanceId,
        ulong sourceEventId,
        string vfxPayload,
        string sfxPayload)
    {
        EntityInstanceId = entityInstanceId;
        SourceEventId = sourceEventId;
        VfxPayload = vfxPayload;
        SfxPayload = sfxPayload;
    }
}
