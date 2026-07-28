using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
[AddComponentMenu("GameMain/AI/Script Intent Emitter")]
public sealed class ScriptIntentEmitter : MonoBehaviour, IIntentProducer
{
    [Header("Intent Pulse")]
    [SerializeField] SkillEntrySlot entrySlot = SkillEntrySlot.LM;
    [SerializeField, Min(0.05f)] float emitIntervalSeconds = 2f;
    [SerializeField, Min(0f)] float initialDelaySeconds = 0.5f;
    [SerializeField, Min(0.01f)] float intentBufferSeconds = 0.25f;
    [SerializeField] bool emitOnlyInLocomotion = true;
    [SerializeField, Tooltip("220.7 D6：关闭后仅保留 ProduceIntent 手动调试入口，不作为主 Producer。")]
    bool allowAsPrimaryProducer;

    Enemy _enemy;
    AIController _aiController;
    IIntentHost _host;
    float _nextEmitTime;
    bool _runtimeBlockedLogged;

    void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _aiController = GetComponent<AIController>();
        _host = _enemy;
    }

    void OnEnable()
    {
        _nextEmitTime = Time.time + initialDelaySeconds;
        _runtimeBlockedLogged = false;
    }

    void Update()
    {
        if (_enemy == null || _host == null || _enemy.IsDead)
        {
            return;
        }

        if (!allowAsPrimaryProducer)
        {
            return;
        }

        if (_aiController != null)
        {
            return;
        }

        if (!_enemy.IsRuntimeReady)
        {
            if (!_runtimeBlockedLogged)
            {
                Debug.LogWarning(
                    $"[Intent] producer=ScriptIntentEmitter result=Blocked reason=runtime-not-ready " +
                    $"detail={_enemy.RuntimeReadyFailure ?? "state-manager-not-started"}",
                    this);
                _runtimeBlockedLogged = true;
            }

            return;
        }

        if (emitOnlyInLocomotion
            && _enemy.StateManager?.Current is not EnemyLocomotionState)
        {
            return;
        }

        if (Time.time < _nextEmitTime)
        {
            return;
        }

        ProduceIntent(Time.time);
        _nextEmitTime = Time.time + Mathf.Max(0.05f, emitIntervalSeconds);
    }

    public void ProduceIntent(float now)
    {
        var intent = GameplayIntent.ForEntry(
            entrySlot,
            now,
            Mathf.Max(0.01f, intentBufferSeconds),
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: 0UL);

        _host?.TryEnqueue(in intent);
    }
}
