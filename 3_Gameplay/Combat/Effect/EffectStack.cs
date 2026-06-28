using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 180.1 W2+W5 — Effect 实例栈管理（5 Stack 策略 + while 消化 FixedTick）。
/// <para>对外只暴露 <see cref="Apply"/> / <see cref="Tick"/> / <see cref="Remove"/> / <see cref="RemoveAllBySource"/>。</para>
/// <para>不直接挂 Modifier；调用方按 EffectInstance 派发到 StatSet（保留 Source 可回收性）。</para>
/// </summary>
public sealed class EffectStack
{
    static long s_nextRuntimeId = 1;

    readonly List<EffectInstance> _instances = new(16);
    double _clock; // 累积世界时

    public IReadOnlyList<EffectInstance> Instances => _instances;
    public int Count => _instances.Count;
    public double Clock => _clock;

    public event Action<EffectInstance> Applied;
    public event Action<EffectInstance> StackChanged;
    public event Action<EffectInstance> PeriodElapsed;
    public event Action<EffectInstance, EffectRemoveReason> Removed;

    /// <summary>主入口：按 StackPolicy 处理。返回最终实例（可能是既有的）。</summary>
    public EffectInstance Apply(EffectDefinitionSO def, object source, EffectSourceKind sourceKind)
    {
        if (def == null) return null;
        if (def.MaxStack < 1) def.MaxStack = 1; // 强制 ≥1

        switch (def.StackPolicy)
        {
            case StackPolicy.Refresh:
                {
                    var existing = FindByDef(def);
                    if (existing != null)
                    {
                        existing.RemainingSeconds = def.DurationSeconds;
                        existing.PeriodTimer = 0;
                        return existing;
                    }
                    return CreateNew(def, source, sourceKind, 1);
                }

            case StackPolicy.Independent:
                return CreateNew(def, source, sourceKind, 1);

            case StackPolicy.RefreshIndependent:
                {
                    var count = CountByDef(def);
                    if (count < def.MaxStack) return CreateNew(def, source, sourceKind, 1);
                    // 满 → 刷最早
                    var oldest = FindOldestByDef(def);
                    if (oldest != null)
                    {
                        oldest.RemainingSeconds = def.DurationSeconds;
                        oldest.PeriodTimer = 0;
                    }
                    return oldest;
                }

            case StackPolicy.AddStack:
                {
                    var existing = FindByDef(def);
                    if (existing == null) return CreateNew(def, source, sourceKind, 1);

                    if (existing.StackCount >= def.MaxStack)
                    {
                        existing.RemainingSeconds = def.DurationSeconds;
                        return existing;
                    }
                    existing.StackCount++;
                    existing.RemainingSeconds = def.DurationSeconds;
                    StackChanged?.Invoke(existing);
                    return existing;
                }

            case StackPolicy.ReplaceLower:
                {
                    var sameTag = FindBySelfTag(def.SelfTags);
                    if (sameTag != null)
                    {
                        if (sameTag.Definition.Power >= def.Power)
                        {
                            return sameTag; // 已有更强 → 拒绝新 Apply
                        }
                        Remove(sameTag, EffectRemoveReason.Replaced);
                    }
                    return CreateNew(def, source, sourceKind, 1);
                }
        }

        return null;
    }

    /// <summary>每帧推进。180.1 §7.1 — while 消化超额 dt 防止 Tick 漂移。</summary>
    public void Tick(float deltaTime, float realtimeDeltaTime = -1f)
    {
        if (deltaTime < 0f) deltaTime = 0f;
        _clock += deltaTime;

        for (var i = _instances.Count - 1; i >= 0; i--)
        {
            var inst = _instances[i];
            var def = inst.Definition;
            var dt = def.RealtimeClock
                ? (realtimeDeltaTime < 0f ? deltaTime : realtimeDeltaTime)
                : deltaTime;

            // ① Period（while 消化）
            if (def.PeriodSeconds > 0f)
            {
                inst.PeriodTimer += dt;
                while (inst.PeriodTimer >= def.PeriodSeconds)
                {
                    inst.PeriodTimer -= def.PeriodSeconds;
                    PeriodElapsed?.Invoke(inst);
                }
            }

            // ② Duration
            if (def.Duration == DurationPolicy.Timed)
            {
                inst.RemainingSeconds -= dt;
                if (inst.RemainingSeconds <= 0)
                {
                    Remove(inst, EffectRemoveReason.Expired);
                    continue;
                }
            }
        }
    }

    public bool Remove(EffectInstance inst, EffectRemoveReason reason)
    {
        if (inst == null) return false;
        var idx = _instances.IndexOf(inst);
        if (idx < 0) return false;
        _instances.RemoveAt(idx);
        Removed?.Invoke(inst, reason);
        return true;
    }

    public int RemoveAllByDef(EffectDefinitionSO def, EffectRemoveReason reason)
    {
        var n = 0;
        for (var i = _instances.Count - 1; i >= 0; i--)
        {
            if (_instances[i].Definition == def)
            {
                var inst = _instances[i];
                _instances.RemoveAt(i);
                Removed?.Invoke(inst, reason);
                n++;
            }
        }
        return n;
    }

    /// <summary>180.1 §7.2 — 按 Source 精确回收（卸装备 / 角色死亡）。</summary>
    public int RemoveAllBySource(object source, EffectRemoveReason reason = EffectRemoveReason.SourceRevoked)
    {
        if (source == null) return 0;
        var n = 0;
        for (var i = _instances.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_instances[i].Source, source))
            {
                var inst = _instances[i];
                _instances.RemoveAt(i);
                Removed?.Invoke(inst, reason);
                n++;
            }
        }
        return n;
    }

    public int RemoveAllBySourceKind(EffectSourceKind kind, EffectRemoveReason reason = EffectRemoveReason.SourceRevoked)
    {
        var n = 0;
        for (var i = _instances.Count - 1; i >= 0; i--)
        {
            if (_instances[i].SourceKind == kind)
            {
                var inst = _instances[i];
                _instances.RemoveAt(i);
                Removed?.Invoke(inst, reason);
                n++;
            }
        }
        return n;
    }

    public void ClearAll(EffectRemoveReason reason = EffectRemoveReason.SceneUnload)
    {
        for (var i = _instances.Count - 1; i >= 0; i--)
        {
            var inst = _instances[i];
            _instances.RemoveAt(i);
            Removed?.Invoke(inst, reason);
        }
    }

    // ─── Stack 公式 ─────────────────────────────────────────

    /// <summary>180.1 §4.2 — 按 StackCount + MergeMode 放大 Modifier 值。</summary>
    public static float ResolveStackedValue(float baseValue, int stack, StackMergeMode mode)
    {
        var s = Mathf.Max(1, stack);
        return mode switch
        {
            StackMergeMode.Multiply => baseValue * s,
            StackMergeMode.Linear => baseValue * s,
            StackMergeMode.Logarithmic => baseValue * Mathf.Log(1f + s, 2f),
            StackMergeMode.NoEffect => baseValue,
            _ => baseValue,
        };
    }

    // ─── Helpers ────────────────────────────────────────────

    EffectInstance CreateNew(EffectDefinitionSO def, object source, EffectSourceKind sourceKind, int initialStack)
    {
        var inst = new EffectInstance(
            s_nextRuntimeId++,
            def,
            source,
            sourceKind,
            _clock,
            initialStack);
        _instances.Add(inst);
        Applied?.Invoke(inst);
        return inst;
    }

    EffectInstance FindByDef(EffectDefinitionSO def)
    {
        for (var i = 0; i < _instances.Count; i++)
        {
            if (_instances[i].Definition == def) return _instances[i];
        }
        return null;
    }

    int CountByDef(EffectDefinitionSO def)
    {
        var n = 0;
        for (var i = 0; i < _instances.Count; i++)
        {
            if (_instances[i].Definition == def) n++;
        }
        return n;
    }

    EffectInstance FindOldestByDef(EffectDefinitionSO def)
    {
        EffectInstance oldest = null;
        for (var i = 0; i < _instances.Count; i++)
        {
            if (_instances[i].Definition != def) continue;
            if (oldest == null || _instances[i].AppliedAt < oldest.AppliedAt)
            {
                oldest = _instances[i];
            }
        }
        return oldest;
    }

    EffectInstance FindBySelfTag(GameplayTagMask tag)
    {
        for (var i = 0; i < _instances.Count; i++)
        {
            if (_instances[i].Definition.SelfTags.Value == tag.Value
                && tag.Value != 0)
            {
                return _instances[i];
            }
        }
        return null;
    }
}
