using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 188.3 W8 — 单个 CombatObject 运行时实例（POCO，无 MonoBehaviour）。
/// <para>由 <see cref="CombatObjectSpawner"/> 创建与 Tick；命中走既有 <see cref="DamagePipeline"/>。</para>
/// </summary>
public sealed class CombatObjectRuntime
{
    public CombatObjectDefinitionSO Definition;
    public Entity Source;                  // 施法者
    public Vector3 SpawnWorldPos;
    public Quaternion SpawnWorldRot;
    public float ElapsedSec;
    public float NextTickAt;
    public int HitCountTotal;
    public bool IsExpired;

    /// <summary>已命中目标 → 次数；MaxHitsPerTarget 去重。</summary>
    public readonly Dictionary<int, int> HitsPerTarget = new(8);

    public Vector3 CurrentWorldPos { get; private set; }

    /// <summary>188.3 W12 — Expand 模式下当前判定半径（Shape 自身半径忽略）。</summary>
    public float CurrentExpandedRadius { get; private set; }

    /// <summary>188.3 W13 — Homing 模式下当前飞行方向（每帧 RotateTowards 目标更新）。</summary>
    Vector3 _homingDir;
    Vector3 _homingPos;

    /// <summary>188.3 W13 — Homing 锁定目标；由调用方在 Spawn 后调 SetHomingTarget 设置。</summary>
    public Entity HomingTarget;

    public void Initialize(
        CombatObjectDefinitionSO def,
        Entity source,
        Vector3 spawnPos,
        Quaternion spawnRot)
    {
        Definition = def;
        Source = source;
        SpawnWorldPos = spawnPos;
        SpawnWorldRot = spawnRot;
        CurrentWorldPos = spawnPos;
        ElapsedSec = 0f;
        NextTickAt = 0f;
        HitCountTotal = 0;
        IsExpired = false;
        HitsPerTarget.Clear();
        CurrentExpandedRadius = def?.Movement.StartRadius ?? 0f;
        _homingDir = spawnRot * Vector3.forward;
        _homingPos = spawnPos;
        HomingTarget = null;
    }

    public void Tick(float dt, Collider[] scratch)
    {
        if (Definition == null || Definition.Shape == null)
        {
            IsExpired = true;
            return;
        }

        ElapsedSec += dt;

        // ① Movement → 当前世界位置（Homing 需要 dt 累计转向；其它按 t 一次性算）
        if (Definition.Movement.Kind == MovementKind.Homing)
        {
            UpdateHoming(dt);
            CurrentWorldPos = _homingPos;
        }
        else
        {
            CurrentWorldPos = ComputeWorldPosition(ElapsedSec);
        }

        // W12: Expand 更新当前半径
        if (Definition.Movement.Kind == MovementKind.Expand)
        {
            UpdateExpandedRadius(ElapsedSec);
        }

        // ② 周期触发判定（Tick=0 → 单次：仅在第一次触发）
        var shouldFire = false;
        if (Definition.Lifecycle.TickInterval <= 0f)
        {
            // 单次：仅 ElapsedSec 首次达到 0（首帧）触发
            shouldFire = NextTickAt < 0.0001f && ElapsedSec >= 0f;
            if (shouldFire) NextTickAt = float.MaxValue; // 标记已触发
        }
        else if (ElapsedSec >= NextTickAt)
        {
            shouldFire = true;
            NextTickAt = ElapsedSec + Definition.Lifecycle.TickInterval;
        }

        if (shouldFire)
        {
            DoOverlapAndApply(scratch);
        }

        // ③ Lifecycle 判存活
        var duration = Definition.Lifecycle.Duration;
        if (duration > 0f && ElapsedSec >= duration)
        {
            IsExpired = true;
        }
    }

    Vector3 ComputeWorldPosition(float t)
    {
        var mov = Definition.Movement;
        switch (mov.Kind)
        {
            case MovementKind.Static:
                return SpawnWorldPos;

            case MovementKind.Linear:
            {
                var dir = SpawnWorldRot * Vector3.forward;
                var dist = mov.Speed * t;
                if (mov.MaxDistance > 0f) dist = Mathf.Min(dist, mov.MaxDistance);
                return SpawnWorldPos + dir * dist;
            }

            // 188.3 W11: Curve（局部 XYZ 偏移）
            case MovementKind.Curve:
            {
                var x = mov.LocalOffsetXOverTime != null ? mov.LocalOffsetXOverTime.Evaluate(t) : 0f;
                var y = mov.LocalOffsetYOverTime != null ? mov.LocalOffsetYOverTime.Evaluate(t) : 0f;
                var z = mov.LocalOffsetZOverTime != null ? mov.LocalOffsetZOverTime.Evaluate(t) : 0f;
                return SpawnWorldPos + SpawnWorldRot * new Vector3(x, y, z);
            }

            // 188.3 W12: Expand 形状原地不动，半径在 UpdateExpandedRadius 算
            case MovementKind.Expand:
                return SpawnWorldPos;

            default:
                return SpawnWorldPos;
        }
    }

    /// <summary>188.3 W12 — 按 Lifecycle.Duration 归一化 t 算当前半径。</summary>
    void UpdateExpandedRadius(float elapsed)
    {
        var mov = Definition.Movement;
        var duration = Mathf.Max(0.0001f, Definition.Lifecycle.Duration);
        var t = Mathf.Clamp01(elapsed / duration);
        if (mov.ExpandCurve != null && mov.ExpandCurve.length > 0)
        {
            t = Mathf.Clamp01(mov.ExpandCurve.Evaluate(t));
        }
        CurrentExpandedRadius = Mathf.Lerp(mov.StartRadius, mov.EndRadius, t);
    }

    /// <summary>188.3 W13 — Homing 每帧 RotateTowards 目标方向 + Speed 推进位置。</summary>
    void UpdateHoming(float dt)
    {
        var mov = Definition.Movement;
        if (HomingTarget != null)
        {
            var toTarget = (HomingTarget.transform.position - _homingPos).normalized;
            var maxRad = mov.TurnRateDegPerSec * Mathf.Deg2Rad * dt;
            _homingDir = Vector3.RotateTowards(_homingDir, toTarget, maxRad, 0f);
            if (_homingDir.sqrMagnitude < 0.0001f) _homingDir = toTarget;
            else _homingDir.Normalize();
        }
        _homingPos += _homingDir * mov.Speed * dt;

        // MaxDistance 钳制
        if (mov.MaxDistance > 0f)
        {
            var traveled = (_homingPos - SpawnWorldPos).magnitude;
            if (traveled >= mov.MaxDistance)
            {
                IsExpired = true;
            }
        }
    }

    void DoOverlapAndApply(Collider[] scratch)
    {
        int n;
        // 188.3 W12 — Expand 模式：忽略 Shape 自身，用 CurrentExpandedRadius 球查询
        if (Definition.Movement.Kind == MovementKind.Expand)
        {
            n = Physics.OverlapSphereNonAlloc(
                CurrentWorldPos, CurrentExpandedRadius, scratch,
                Definition.QueryLayerMask.value);
        }
        else
        {
            n = Definition.Shape.Overlap(
                CurrentWorldPos, SpawnWorldRot, scratch,
                Definition.QueryLayerMask.value);
        }

        for (var i = 0; i < n; i++)
        {
            if (HitCountTotal >= Definition.Lifecycle.MaxTargets) break;
            var col = scratch[i];
            if (col == null) continue;

            var target = col.GetComponentInParent<Entity>();
            if (target == null) continue;
            if (target == Source) continue; // 不打自己

            if (!TargetFilterEvaluator.Passes(Definition.TargetFilter, Source, target))
            {
                continue;
            }

            // MaxHitsPerTarget 去重
            var id = target.GetInstanceID();
            HitsPerTarget.TryGetValue(id, out var hits);
            if (hits >= Definition.Lifecycle.MaxHitsPerTarget) continue;
            HitsPerTarget[id] = hits + 1;

            CombatHitDiagProbe.LogOverlap(target, hits + 1, col);
            ApplyDamage(target);
            HitCountTotal++;
        }
    }

    void ApplyDamage(Entity target)
    {
        var def = Definition.Damage;
        if (def == null) return;

        switch (def.Kind)
        {
            case DamageKind.Heal:
            {
                var hp = target.Resources;
                var cur = hp.GetCurrent(ResourceType.HP);
                var max = hp.GetMax(ResourceType.HP);
                hp.SetCurrent(ResourceType.HP, Mathf.Min(max, cur + def.Amount));
                break;
            }
            case DamageKind.Knockback:
            {
                // 188.3 W14 — 接 Player.SetPlanarVelocity（Player 是 Entity 子类时）
                var worldDir = SpawnWorldRot * def.KnockbackLocalDir.normalized;
                if (target is Player p)
                {
                    p.SetPlanarVelocity(worldDir * def.KnockbackForce);
                }
                break;
            }
            case DamageKind.Launch:
            {
                // 188.3 W14 — 向上推力（接 175.2 Jump Variant 后可走 LaunchUpSpeed）
                if (target is Player p)
                {
                    var current = p.PlanarVelocity;
                    p.SetPlanarVelocity(current + Vector3.up * def.LaunchUpSpeed);
                }
                break;
            }

            case DamageKind.Instant:
            case DamageKind.InstantPlusEffect:
            default:
            {
                // 走既有 DamagePipeline
                var ctx = new CombatContext(
                    attackerAttackPower: 0f,    // TODO: Source.Stats.AttackPower
                    defenderDefense: 0f,         // TODO: target.Stats.Defense
                    defenderCurrentHP: target.Resources.GetCurrent(ResourceType.HP),
                    defenderMaxHP: target.Resources.GetMax(ResourceType.HP),
                    attackerTags: 0UL,
                    defenderTags: 0UL);
                var hit = new HitContext(
                    baseDamage: def.Amount,
                    isCritical: false,
                    criticalMultiplier: 1f,
                    hitPoint: CurrentWorldPos);
                var result = DamagePipeline.Compute(in ctx, in hit);
                var pool = target.Resources;
                var cur = pool.GetCurrent(ResourceType.HP);
                pool.SetCurrent(ResourceType.HP, Mathf.Max(0f, cur - result.FinalDamage));
                CombatHitDiagProbe.LogDamage(target, result.FinalDamage, def.Kind);
                break;
            }
        }

        // OnHitEffect（216.3 M3 L3 — 与 HitReaction 共用 EffectSystem 单点）
        if (def.OnHitEffect != null && target is IEffectReceiver receiver)
        {
            EffectSystem.ApplyEffect(Source, receiver, def.OnHitEffect);
        }
    }
}
