using UnityEngine;

/// <summary>
/// 订阅 <see cref="EntityActionPresentationEvent"/>，将时间轴 FX/SFX/HitFrame 接到表现层（139.2 P2/P3）。
/// 挂 Player 根节点；后续可替换为 Addressables / VFX Graph / Wwise。
/// </summary>
[AddComponentMenu("GameMain/Presentation/Action Timeline Presentation Player")]
[RequireComponent(typeof(Player))]
public sealed class ActionTimelinePresentationPlayer : MonoBehaviour
{
    [SerializeField] bool logEvents = true;

    Player _player;

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void OnEnable()
    {
        if (_player == null)
        {
            return;
        }

        _player.EventBus.Subscribe<EntityActionPresentationEvent>(OnTimelineEvent);
    }

    void OnDisable()
    {
        if (_player == null)
        {
            return;
        }

        _player.EventBus.Unsubscribe<EntityActionPresentationEvent>(OnTimelineEvent);
    }

    void OnTimelineEvent(EntityActionPresentationEvent evt)
    {
        if (_player == null || evt.EntityInstanceId != _player.GetInstanceID())
        {
            return;
        }

        if (!logEvents)
        {
            return;
        }

        Debug.Log($"[Timeline] kind={evt.Kind} payload={evt.Payload ?? ""}", this);
    }
}
