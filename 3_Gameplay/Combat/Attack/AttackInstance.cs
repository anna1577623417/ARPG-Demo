using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ActionContact 的一次运行时实例。
/// ContactEvent.Active 区间只负责开关，本类负责采样、命中去重、产出 ContactFact，
/// 不直接写 HP；最终效果统一经 CombatEventBus/Resolver 提交。
/// </summary>
public sealed partial class AttackInstance
{
    public Entity Source;
    public Vector3 OriginPos;
    public Quaternion OriginRot;
    public Vector3 PrevSamplePos;
    public bool Active;
    public bool IsExpired;
    public float ElapsedSec;

    public HitResult LastHitResult { get; private set; }
    public bool HasLastHit { get; private set; }

    readonly HitRegistry _registry = new();
    readonly HashSet<int> _clashedPartnerIds = new HashSet<int>();

    public HitRegistry Registry => _registry;

    /// <summary>关判并释放 Contact Runtime；重复调用安全。</summary>
    public void End()
    {
        if (!Active)
        {
            return;
        }

        if (_contactMode)
        {
            EndContactRuntime();
            return;
        }

        Active = false;
        IsExpired = true;
    }

    internal void RecordHit(in HitResult result)
    {
        LastHitResult = result;
        HasLastHit = true;
    }

    internal static HitPolicyParams NormalizePolicy(in HitPolicyParams raw)
    {
        var p = raw;
        if (p.MaxHitsPerTarget < 1) p.MaxHitsPerTarget = 1;
        if (p.MaxTargets < 1) p.MaxTargets = 999;
        if (p.IntervalSeconds < 0.01f) p.IntervalSeconds = 0.2f;
        return p;
    }

    internal static string SafeName(string value) =>
        string.IsNullOrEmpty(value) ? "(unnamed)" : value;
}
