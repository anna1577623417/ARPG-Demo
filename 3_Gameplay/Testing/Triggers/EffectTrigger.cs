using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class EffectTrigger : MonoBehaviour
{
    [SerializeField] EffectTriggerConfigSO config;

    readonly Dictionary<IEntity, float> _lastTriggerTimes = new Dictionary<IEntity, float>();
    readonly HashSet<IEntity> _entitiesInZone = new HashSet<IEntity>();

    bool _hasTriggered;
    bool _isActive;
    float _spawnTime;
    float _activationTimer;

    public event Action<IEntity> OnEffectApplied;
    public event Action OnTriggerActivated;
    public event Action OnTriggerDeactivated;

    void OnEnable()
    {
        _spawnTime = Time.time;
        _hasTriggered = false;
        _activationTimer = config != null ? Mathf.Max(0f, config.activationDelay) : 0f;
        _isActive = _activationTimer <= 0f;
    }

    void Update()
    {
        if (config == null)
        {
            return;
        }

        if (!_isActive)
        {
            _activationTimer -= Time.deltaTime;
            if (_activationTimer <= 0f)
            {
                _isActive = true;
                OnTriggerActivated?.Invoke();
            }
            return;
        }

        if (config.lifetime > 0f && Time.time - _spawnTime >= config.lifetime)
        {
            Deactivate();
            return;
        }

        if (config.triggerMode == TriggerMode.Interval)
        {
            TickInterval();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!_isActive || config == null)
        {
            return;
        }

        var entity = ResolveEntity(other);
        if (entity == null || !PassesFactionFilter(entity))
        {
            return;
        }

        _entitiesInZone.Add(entity);
        if (config.triggerMode == TriggerMode.OnEnter)
        {
            ApplyEffects(entity);
            HandlePostTrigger();
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!_isActive || config == null || config.triggerMode != TriggerMode.OnStay)
        {
            return;
        }

        var entity = ResolveEntity(other);
        if (entity == null || !PassesFactionFilter(entity))
        {
            return;
        }

        ApplyEffects(entity);
    }

    void OnTriggerExit(Collider other)
    {
        var entity = ResolveEntity(other);
        if (entity == null)
        {
            return;
        }

        _entitiesInZone.Remove(entity);
        _lastTriggerTimes.Remove(entity);
    }

    void TickInterval()
    {
        var now = Time.time;
        var interval = Mathf.Max(0.01f, config.interval);
        foreach (var entity in _entitiesInZone)
        {
            if (!entity.IsAlive)
            {
                continue;
            }

            var lastTime = _lastTriggerTimes.TryGetValue(entity, out var t) ? t : -999f;
            if (now - lastTime < interval)
            {
                continue;
            }

            ApplyEffects(entity);
            _lastTriggerTimes[entity] = now;
        }
    }

    void ApplyEffects(IEntity target)
    {
        if (config.triggerOnce && _hasTriggered)
        {
            return;
        }

        var receiver = target as IEffectReceiver;
        if (receiver == null || config.effects == null)
        {
            return;
        }

        for (var i = 0; i < config.effects.Length; i++)
        {
            var effect = config.effects[i];
            if (effect == null)
            {
                continue;
            }

            EffectSystem.ApplyEffect(gameObject, receiver, effect);
        }

        OnEffectApplied?.Invoke(target);
    }

    bool PassesFactionFilter(IEntity entity)
    {
        if (config == null)
        {
            return false;
        }

        if (config.factionFilter == FactionFilterMode.AffectAll)
        {
            return true;
        }

        var hasAnyListed = false;
        if (config.affectedFactions != null)
        {
            for (var i = 0; i < config.affectedFactions.Length; i++)
            {
                if (entity.Tags.Faction.HasAny((ulong)config.affectedFactions[i]))
                {
                    hasAnyListed = true;
                    break;
                }
            }
        }

        if (config.factionFilter == FactionFilterMode.AffectEnemies)
        {
            return hasAnyListed;
        }

        return !hasAnyListed;
    }

    IEntity ResolveEntity(Collider col)
    {
        var entity = col.GetComponent<IEntity>();
        return entity ?? col.GetComponentInParent<IEntity>();
    }

    void HandlePostTrigger()
    {
        _hasTriggered = true;
        if (config.destroyAfterTrigger)
        {
            Deactivate();
        }
    }

    void Deactivate()
    {
        _isActive = false;
        _entitiesInZone.Clear();
        _lastTriggerTimes.Clear();
        OnTriggerDeactivated?.Invoke();
        gameObject.SetActive(false);
    }
}
