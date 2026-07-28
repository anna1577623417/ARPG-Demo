using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
[AddComponentMenu("GameMain/Presentation/Enemy Animation Controller")]
public sealed class EnemyAnimController : EntityAnimController, IActionPresentationPort
{
    Entity _entity;

    public bool IsReady => IsGraphValid;

    protected override void Awake()
    {
        base.Awake();
        _entity = GetComponent<Enemy>();
    }

    void OnEnable()
    {
        if (_entity != null)
        {
            _entity.EventBus.Subscribe<EntityActionPlaybackRequestEvent>(OnActionPlaybackRequest);
        }
    }

    void OnDisable()
    {
        if (_entity != null)
        {
            _entity.EventBus.Unsubscribe<EntityActionPlaybackRequestEvent>(OnActionPlaybackRequest);
        }
    }

    void OnActionPlaybackRequest(EntityActionPlaybackRequestEvent evt)
    {
        if (_entity == null || evt.EntityInstanceId != _entity.GetInstanceID())
        {
            return;
        }

        var action = evt.Action;
        var clip = action?.MainClip;
        if (clip == null)
        {
            if (GameMainDebugSettings.CombatHit
                || (evt.Kind == GameplayIntentKind.HitReact
                    && GameMainDebugSettings.ReactionDirection2206Log))
            {
                Debug.Log(
                    $"[EnemyAnim] SKIP action={action?.name ?? "-"} kind={evt.Kind} reason=no-main-clip",
                    this);
            }

            return;
        }

        var clipStartNormalized = action.MapActionTimeToClipNormalized(evt.NormalizedStart);
        var speed = Mathf.Max(0.01f, action.ResolveEffectiveAnimSpeed());
        Play(
            clip,
            action.CrossfadeTime,
            speed,
            clip.isLooping,
            clipStartNormalized);

        if (GameMainDebugSettings.CombatHit
            || (evt.Kind == GameplayIntentKind.HitReact
                && GameMainDebugSettings.ReactionDirection2206Log))
        {
            Debug.Log(
                $"[EnemyAnim] PLAY action={action.name} clip={clip.name} " +
                $"kind={evt.Kind} start={evt.NormalizedStart:F2} speed={speed:F2} log=220.6",
                this);
        }
    }
}
