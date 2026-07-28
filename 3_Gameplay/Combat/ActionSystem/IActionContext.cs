using UnityEngine;

/// <summary>
/// B2：Action 时间轴所需的能力上下文。
/// <para>Timeline 只依赖实体能力，不依赖 Player/Enemy 具体类型。</para>
/// <para>每帧由所属状态传入，不由 Timeline 缓存。</para>
/// </summary>
public interface IActionContext : IIntentHost, ISkillHost
{
    Transform Transform { get; }
    Animator Animator { get; }
    IEntityMotor Motor { get; }
    LocalEventBus EventBus { get; }
    CombatObjectSpawner CombatObjectSpawner { get; }

    void PublishActionPresentation(ActionTimelineMarkerKind kind, string payload);
    void PublishTeleported(Vector3 worldPosition);
}

/// <summary>
/// 220.5 B5.3：实体动作表现端口的最小就绪契约。
/// <para>Gameplay 只检查能力是否可用，不依赖具体 Animator 或 Playables 实现。</para>
/// </summary>
public interface IActionPresentationPort
{
    bool IsReady { get; }
}
