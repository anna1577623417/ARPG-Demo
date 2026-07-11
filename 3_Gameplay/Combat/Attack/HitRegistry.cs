using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 216.3 M2 — 命中登记（按 <see cref="HitPolicyParams"/> 裁决是否可再命中）。
/// <para>单一真相：可命中与否只经 <see cref="TryAccept"/>；禁止调用方自写 <c>alreadyHit</c>。</para>
/// </summary>
public sealed class HitRegistry
{
    readonly Dictionary<int, int> _hitCounts = new(8);
    readonly Dictionary<int, int> _intervalGenHit = new(8);

    int _uniqueTargets;
    bool _singleConsumed;
    float _nextIntervalAt;
    int _intervalGeneration;

    public void Clear()
    {
        _hitCounts.Clear();
        _intervalGenHit.Clear();
        _uniqueTargets = 0;
        _singleConsumed = false;
        _nextIntervalAt = 0f;
        _intervalGeneration = 0;
    }

    /// <summary>连段间重置（PerSwing）：下一挥击重新可命中。</summary>
    public void ResetSwing() => Clear();

    /// <summary>
    /// 每帧查询前调用。Interval 策略下仅在开窗帧返回 true；其它策略恒 true。
    /// <paramref name="intervalFired"/> 为 true 时打 <c>[Trace] SWEEP interval fire</c>。
    /// </summary>
    public bool BeginFrame(in HitPolicyParams policy, float elapsedSec, out bool intervalFired)
    {
        intervalFired = false;
        if (policy.Kind != HitPolicyKind.Interval)
        {
            return true;
        }

        var interval = Mathf.Max(0.01f, policy.IntervalSeconds);
        if (elapsedSec + 1e-4f < _nextIntervalAt)
        {
            return false;
        }

        intervalFired = true;
        _nextIntervalAt = elapsedSec + interval;
        _intervalGeneration++;
        return true;
    }

    /// <summary>
    /// 裁决并登记一次命中。返回 true = 本次有效命中。
    /// </summary>
    public bool TryAccept(in HitPolicyParams policy, int targetInstanceId)
    {
        var maxTargets = Mathf.Max(1, policy.MaxTargets);
        var maxPerTarget = Mathf.Max(1, policy.MaxHitsPerTarget);

        switch (policy.Kind)
        {
            case HitPolicyKind.Single:
                if (_singleConsumed)
                {
                    return false;
                }

                if (!TryTouchUnique(targetInstanceId, maxTargets))
                {
                    return false;
                }

                _hitCounts[targetInstanceId] = 1;
                _singleConsumed = true;
                return true;

            case HitPolicyKind.PerTarget:
            case HitPolicyKind.PerSwing:
                if (_hitCounts.ContainsKey(targetInstanceId))
                {
                    return false;
                }

                if (!TryTouchUnique(targetInstanceId, maxTargets))
                {
                    return false;
                }

                _hitCounts[targetInstanceId] = 1;
                return true;

            case HitPolicyKind.Interval:
                // 开窗帧内每目标一次（跨窗由 BeginFrame 升 generation）。
                if (_intervalGenHit.TryGetValue(targetInstanceId, out var gen)
                    && gen == _intervalGeneration)
                {
                    return false;
                }

                _hitCounts.TryGetValue(targetInstanceId, out var intervalHits);
                if (intervalHits >= maxPerTarget)
                {
                    return false;
                }

                if (intervalHits == 0 && !TryTouchUnique(targetInstanceId, maxTargets))
                {
                    return false;
                }

                _intervalGenHit[targetInstanceId] = _intervalGeneration;
                _hitCounts[targetInstanceId] = intervalHits + 1;
                return true;

            case HitPolicyKind.Continuous:
            {
                _hitCounts.TryGetValue(targetInstanceId, out var cHits);
                if (cHits >= maxPerTarget)
                {
                    return false;
                }

                if (cHits == 0 && !TryTouchUnique(targetInstanceId, maxTargets))
                {
                    return false;
                }

                _hitCounts[targetInstanceId] = cHits + 1;
                return true;
            }

            case HitPolicyKind.Multi:
            {
                _hitCounts.TryGetValue(targetInstanceId, out var mHits);
                if (mHits >= maxPerTarget)
                {
                    return false;
                }

                if (mHits == 0 && !TryTouchUnique(targetInstanceId, maxTargets))
                {
                    return false;
                }

                _hitCounts[targetInstanceId] = mHits + 1;
                return true;
            }

            default:
                if (_hitCounts.ContainsKey(targetInstanceId))
                {
                    return false;
                }

                if (!TryTouchUnique(targetInstanceId, maxTargets))
                {
                    return false;
                }

                _hitCounts[targetInstanceId] = 1;
                return true;
        }
    }

    /// <summary>兼容 M1 调用：等价 PerTarget。</summary>
    public bool TryRegister(int targetInstanceId) =>
        TryAccept(HitPolicyParams.Default, targetInstanceId);

    public bool Has(int targetInstanceId) => _hitCounts.ContainsKey(targetInstanceId);

    public int GetHitCount(int targetInstanceId) =>
        _hitCounts.TryGetValue(targetInstanceId, out var n) ? n : 0;

    bool TryTouchUnique(int targetInstanceId, int maxTargets)
    {
        if (_hitCounts.ContainsKey(targetInstanceId))
        {
            return true;
        }

        if (_uniqueTargets >= maxTargets)
        {
            return false;
        }

        _uniqueTargets++;
        return true;
    }
}
