using UnityEngine;

/// <summary>
/// 玩家动画管理器。
/// 继承 EntityAnimManager（Playable API），监听状态事件自动播放对应动画。
///
/// ═══ 逻辑与表现的解耦 ═══
///
/// 状态机（PlayerStateManager）只做逻辑：判断输入、切换状态、驱动移动。
/// 动画管理器（PlayerAnimManager）只做表现：监听状态切换事件，播放对应动画。
///
/// 它们之间的桥梁是 EventBus：
///   状态机切换状态 → EntityStateManager 发布 EntityStateEnterEvent 到 Entity.EventBus
///                 → PlayerAnimController 监听该事件 → 查 AnimLibrary → 播放 Clip
///
/// 这样做的好处：
/// 1. 状态代码里没有任何 Animator/Playable 引用
/// 2. 换一套动画只需要换 AnimLibrarySO 资产，不改代码
/// 3. 可以给同一个状态机绑不同的 AnimManager（如布偶测试、简化显示等）
/// </summary>
[RequireComponent(typeof(PlayerStateManager))]
public class PlayerAnimController : EntityAnimController
{
    [Header("Animation Library")]
    [SerializeField] private PlayerAnimManagerSO animLibrary;

    private Entity _entity;

    protected override void Awake()
    {
        base.Awake();
        _entity = GetComponent<Entity>();
    }

    private void OnEnable()
    {
        if (_entity != null)
        {
            _entity.EventBus.Subscribe<EntityStateEnterEvent>(OnStateEnter);
            _entity.EventBus.Subscribe<PlayerActionPresentationRequestEvent>(OnActionPresentationRequest);
        }
    }

    private void OnDisable()
    {
        if (_entity != null)
        {
            _entity.EventBus.Unsubscribe<EntityStateEnterEvent>(OnStateEnter);
            _entity.EventBus.Unsubscribe<PlayerActionPresentationRequestEvent>(OnActionPresentationRequest);
        }
    }

    /// <summary>
    /// 状态进入事件回调。
    /// 根据状态名从 AnimLibrary 中查找对应的动画配置并播放。
    /// </summary>
    private void OnStateEnter(EntityStateEnterEvent evt)
    {
        if (animLibrary == null) return;

        // Action 支柱由 PlayerActionPresentationRequestEvent 统一驱动，避免与数据资产双播。
        if (evt.StateName == nameof(PlayerActionState))
        {
            return;
        }

        // 用状态名匹配动画条目
        var entry = animLibrary.GetEntry(evt.StateName);
        if (entry == null || entry.Clip == null) return;

        Play(entry.Clip, entry.TransitionDuration, entry.Speed, entry.IsLooping);
    }

    /// <summary>
    /// Action 支柱显式请求：优先播放 <see cref="ActionDataSO.MainClip"/>，否则回退到状态名映射。
    /// </summary>
    private void OnActionPresentationRequest(PlayerActionPresentationRequestEvent evt)
    {
        if (animLibrary == null)
        {
            return;
        }

        if (evt.Action != null && evt.Action.MainClip != null)
        {
            Play(evt.Action.MainClip, 0.08f, 1f, evt.Action.MainClip.isLooping);
            return;
        }

        var libraryKey = evt.Kind == GameplayIntentKind.Dodge
            ? "PlayerActionDodge"
            : nameof(PlayerActionState);

        var entry = animLibrary.GetEntry(libraryKey);
        if (entry == null || entry.Clip == null)
        {
            return;
        }

        Play(entry.Clip, entry.TransitionDuration, entry.Speed, entry.IsLooping);
    }
}
