using System.Collections.Generic;
using UnityEngine;

public sealed class ProjectileController : MonoBehaviour
{
    ProjectileDataSO _data;
    IEntity _source;
    Vector3 _velocity;
    float _spawnTime;
    int _pierceCount;
    readonly HashSet<IEntity> _alreadyHit = new HashSet<IEntity>();
    readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

    public void Init(IEntity source, Vector3 direction, ProjectileDataSO data)
    {
        _source = source;
        _data = data;
        var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        _velocity = dir * (_data != null ? Mathf.Max(0f, _data.speed) : 0f);
        _spawnTime = Time.time;
        _pierceCount = 0;
        _alreadyHit.Clear();
    }

    void Update()
    {
        if (_data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        var dt = Time.deltaTime;
        if (Time.time - _spawnTime > _data.lifetime)
        {
            gameObject.SetActive(false);
            return;
        }

        if (_data.useGravity)
        {
            _velocity.y -= 9.81f * _data.gravityScale * dt;
        }

        var moveDist = _velocity.magnitude * dt;
        if (moveDist > 0.0001f)
        {
            var moveDir = _velocity.normalized;
            var hitCount = Physics.SphereCastNonAlloc(
                transform.position,
                Mathf.Max(0.01f, _data.hitRadius),
                moveDir,
                _hitBuffer,
                moveDist,
                _data.hitLayerMask,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < hitCount; i++)
            {
                var entity = _hitBuffer[i].collider.GetComponentInParent<IEntity>();
                if (entity == null || entity == _source || _alreadyHit.Contains(entity))
                {
                    continue;
                }

                _alreadyHit.Add(entity);
                OnHitEntity(entity, _hitBuffer[i].point);

                if (!_data.piercing || _pierceCount >= _data.maxPierceCount)
                {
                    gameObject.SetActive(false);
                    return;
                }

                _pierceCount++;
            }
        }

        transform.position += _velocity * dt;
        if (_velocity.sqrMagnitude > 0.001f)
        {
            transform.forward = _velocity.normalized;
        }
    }

    void OnHitEntity(IEntity target, Vector3 hitPoint)
    {
        if (target is IEffectReceiver receiver && _data.onHitEffects != null)
        {
            for (var i = 0; i < _data.onHitEffects.Length; i++)
            {
                if (_data.onHitEffects[i] != null)
                {
                    EffectSystem.ApplyEffect(_source != null ? _source.Transform.gameObject : gameObject, receiver, _data.onHitEffects[i]);
                }
            }
        }

        if (target is IDamageable damageable)
        {
            var amount = _source != null ? _source.Stats.Get(StatType.AttackPower) : 0f;
            damageable.TakeDamage(new DamageInfo
            {
                Amount = Mathf.Max(0f, amount),
                Source = _source != null ? _source.Transform.gameObject : gameObject,
                HitPoint = hitPoint,
                Force = Vector3.zero
            });
        }
    }
}
