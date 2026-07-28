using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 216.3 M1~M3 / M5 — 一次攻击判定的运行时实例（POCO，无 MonoBehaviour）。
/// <para>由 <c>HitClip</c> 的 Active 区间驱动：<see cref="Begin"/> → <see cref="TickSweep"/> → <see cref="End"/>。</para>
/// <para><b>职责边界（216.3 §1.3）</b>：Sweep/Overlap → Policy 裁决 → 产 <see cref="HitResult"/> →
/// <see cref="CombatEventBus.PublishResolved"/>；本类<b>不</b>直接写 HP。</para>
/// </summary>
public sealed class AttackInstance
{
    public HitClip Clip;
    public Entity Source;
    public Vector3 OriginPos;
    public Quaternion OriginRot;

    /// <summary>上一帧 Socket/形状采样位置（Sweep 起点）。</summary>
    public Vector3 PrevSamplePos;

    public bool Active;
    public bool IsExpired;

    /// <summary>自 Begin 起累计秒（Interval 策略用）。</summary>
    public float ElapsedSec;

    /// <summary>最近一次有效命中（M3 L1；L2 交 Resolver）。</summary>
    public HitResult LastHitResult { get; private set; }

    public bool HasLastHit { get; private set; }

    readonly HitRegistry _registry = new();
    readonly WeaponTraceProvider _weaponTrace = new();

    public HitRegistry Registry => _registry;
    public WeaponTraceProvider WeaponTrace => _weaponTrace;

    readonly HashSet<int> _clashedPartnerIds = new HashSet<int>();

    /// <summary>开判：Active 区间进入时由 ActionTimelineRuntime 调用。</summary>
    public void Begin(in HitClip clip, Entity source, Vector3 originPos, Quaternion originRot)
    {
        Clip = clip;
        Source = source;
        OriginPos = originPos;
        OriginRot = originRot;
        PrevSamplePos = originPos;
        Active = true;
        IsExpired = false;
        ElapsedSec = 0f;
        HasLastHit = false;
        LastHitResult = default;
        _clashedPartnerIds.Clear();

        if (clip.Policy.Kind == HitPolicyKind.PerSwing)
        {
            _registry.ResetSwing();
        }
        else
        {
            _registry.Clear();
        }

        _weaponTrace.ResetHistory();

        if (clip.ShapeMode == HitShapeMode.WeaponTrace)
        {
            AttackTraceRegistry.Register(source, this);
        }

        if (GameMainDebugSettings.CombatHit)
        {
            LogBegin(in clip);
        }
    }

    void LogBegin(in HitClip clip)
    {
        var targetSummary = TargetProfileEvaluator.DescribeProfile(in clip.Target);
        var mask = clip.QueryLayerMask.value;
        var header =
            $"[Attack] BEGIN clip={SafeName(clip.DebugName)} active={clip.ActiveStart:F2}~{clip.ActiveEnd:F2} " +
            $"mode={clip.ShapeMode} policy={clip.Policy.Kind} {targetSummary} mask={mask}";

        if (clip.ShapeMode == HitShapeMode.WeaponTrace)
        {
            var socketCount = clip.WeaponSockets != null ? clip.WeaponSockets.Count : 0;
            header += $" sockets={socketCount}";
            Debug.Log(header);
            LogWeaponSocketSummary(clip.WeaponSockets);
            return;
        }

        var shapeName = clip.Shape != null ? clip.Shape.name : "null";
        header += $" shape={shapeName} reach={clip.Reach:F2}";
        Debug.Log(header);
    }

    static void LogWeaponSocketSummary(WeaponSocketSetSO set)
    {
        if (set == null || set.Sockets == null || set.Sockets.Length == 0)
        {
            Debug.Log("[Attack] BEGIN trace sockets=(none)");
            return;
        }

        var sb = new StringBuilder(128);
        sb.Append("[Attack] BEGIN trace");
        for (var i = 0; i < set.Sockets.Length; i++)
        {
            var def = set.Sockets[i];
            var name = string.IsNullOrEmpty(def.DebugName) ? $"s{i}" : def.DebugName;
            var radius = def.Radius > 0.01f ? def.Radius : 0.05f;
            sb.Append(' ').Append(name).Append("(r=").Append(radius.ToString("F2")).Append(')');
        }

        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Active 期每帧：按 <see cref="HitShapeMode"/> 单轨判定 → Policy → HitResult。
    /// </summary>
    public void TickSweep(
        Vector3 curPos,
        Quaternion curRot,
        RaycastHit[] hitScratch,
        Collider[] overlapScratch)
    {
        if (!Active)
        {
            return;
        }

        ElapsedSec += Time.deltaTime;
        var policy = NormalizePolicy(Clip.Policy);
        var allowHits = _registry.BeginFrame(in policy, ElapsedSec, out var intervalFired);
        if (intervalFired && GameMainDebugSettings.CombatHit)
        {
            Debug.Log($"[Trace] SWEEP interval fire t={ElapsedSec:F2} clip={SafeName(Clip.DebugName)}");
        }

        var mask = Clip.QueryLayerMask.value;

        switch (Clip.ShapeMode)
        {
            case HitShapeMode.WeaponTrace:
                TickWeaponTrace(mask, allowHits, in policy, hitScratch, overlapScratch);
                break;

            case HitShapeMode.Volume:
            default:
                TickVolume(curPos, curRot, mask, allowHits, in policy, hitScratch, overlapScratch);
                break;
        }

        PrevSamplePos = curPos;
        OriginRot = curRot;
        OriginPos = curPos;
    }

    void TickWeaponTrace(
        int mask,
        bool allowHits,
        in HitPolicyParams policy,
        RaycastHit[] hitScratch,
        Collider[] overlapScratch)
    {
        if (Clip.WeaponSockets == null || Clip.WeaponSockets.Count <= 0)
        {
            if (GameMainDebugSettings.CombatHit)
            {
                Debug.LogWarning(
                    $"[Attack] mode=WeaponTrace but WeaponSockets missing clip={SafeName(Clip.DebugName)}");
            }

            return;
        }

        if (allowHits)
        {
            // CS1628：lambda 不能捕获 in 参数，先拷到局部。
            var policyLocal = policy;
            _weaponTrace.SweepSockets(
                Source,
                Clip.WeaponSockets,
                mask,
                hitScratch,
                overlapScratch,
                (col, point, normal) => TryHit(col, in policyLocal, point, normal, viaWeaponTrace: true));

            // 216.3 M5 L3：用本帧 Socket 样本做双 Trace 拼刀（无 Collider）。
            var sampleCount = Clip.WeaponSockets.Count;
            AttackTraceRegistry.UpdateSamples(Source, _weaponTrace.Scratch, sampleCount);
            TryResolveWeaponClash();
        }
        else
        {
            _weaponTrace.AdvanceHistory(Source, Clip.WeaponSockets);
            AttackTraceRegistry.UpdateSamples(Source, _weaponTrace.Scratch, Clip.WeaponSockets.Count);
        }
    }

    /// <summary>双 WeaponTrace Socket 球相交 → Publish Clash（每对手每挥一次）。</summary>
    void TryResolveWeaponClash()
    {
        if (!AttackTraceRegistry.TryFindClashOpponent(Source, out var opponent, out var point))
        {
            return;
        }

        var partnerId = opponent.GetInstanceID();
        if (!_clashedPartnerIds.Add(partnerId))
        {
            return;
        }

        var result = new HitResult(
            Source,
            opponent,
            point,
            Vector3.up,
            "WeaponClash",
            Clip.DebugName,
            hitCountOnTarget: 1,
            ElapsedSec);

        LastHitResult = result;
        HasLastHit = true;

        if (GameMainDebugSettings.CombatHit)
        {
            var selfName = Source != null ? Source.name : null;
            Debug.Log(
                $"[Attack] CLASH detect self={SafeName(selfName)} " +
                $"vs={opponent.name} point={point} clip={SafeName(Clip.DebugName)}");
        }

        CombatEventBus.PublishResolved(in result, in Clip.Reaction);
    }

    void TickVolume(
        Vector3 curPos,
        Quaternion curRot,
        int mask,
        bool allowHits,
        in HitPolicyParams policy,
        RaycastHit[] hitScratch,
        Collider[] overlapScratch)
    {
        if (Clip.Shape == null)
        {
            return;
        }

        var overlapCount = Clip.Shape.Overlap(curPos, curRot, overlapScratch, mask);
        var sweepCount = Clip.Shape.Sweep(PrevSamplePos, curPos, curRot, hitScratch, mask);

        if (GameMainDebugSettings.CombatHit && (overlapCount > 0 || sweepCount > 0))
        {
            Debug.Log($"[Trace] SWEEP clip={SafeName(Clip.DebugName)} overlap={overlapCount} sweep={sweepCount}");
        }

        if (!allowHits)
        {
            return;
        }

        for (var i = 0; i < overlapCount; i++)
        {
            var col = overlapScratch[i];
            if (col == null)
            {
                continue;
            }

            var point = col.ClosestPoint(curPos);
            var toOrigin = curPos - point;
            var normal = toOrigin.sqrMagnitude > 1e-6f ? toOrigin.normalized : Vector3.up;
            TryHit(col, in policy, point, normal, viaWeaponTrace: false);
        }

        for (var i = 0; i < sweepCount; i++)
        {
            ref var hit = ref hitScratch[i];
            if (hit.collider == null)
            {
                continue;
            }

            TryHit(hit.collider, in policy, hit.point, hit.normal, viaWeaponTrace: false);
        }
    }

    void TryHit(
        Collider col,
        in HitPolicyParams policy,
        Vector3 point,
        Vector3 normal,
        bool viaWeaponTrace)
    {
        if (col == null)
        {
            return;
        }

        var target = col.GetComponentInParent<Entity>();
        if (target == null || target == Source)
        {
            return;
        }

        if (!TargetProfileEvaluator.Passes(Clip.Target, Source, target))
        {
            if (GameMainDebugSettings.CombatHit)
            {
                var reason = TargetProfileEvaluator.DescribeReject(Clip.Target, Source, target);
                if (!string.IsNullOrEmpty(reason))
                {
                    Debug.Log(
                        $"[Attack] REJECT source={(Source != null ? Source.name : "null")} " +
                        $"sourceInfo=({TargetFilterEvaluator.DescribeEntity(Source)}) " +
                        $"target={target.name} targetInfo=({TargetFilterEvaluator.DescribeEntity(target)}) " +
                        $"relation={TargetFilterEvaluator.DescribeRelation(Source, target)} " +
                        $"reason={reason} clip={SafeName(Clip.DebugName)}");
                }
            }

            return;
        }

        var targetId = target.GetInstanceID();
        if (!_registry.TryAccept(in policy, targetId))
        {
            return;
        }

        var bone = ResolveBoneName(target, col.transform);
        var hitCount = _registry.GetHitCount(targetId);
        var result = new HitResult(
            Source,
            target,
            point,
            normal,
            bone,
            Clip.DebugName,
            hitCount,
            ElapsedSec);

        LastHitResult = result;
        HasLastHit = true;

        if (GameMainDebugSettings.CombatHit)
        {
            var relation = TargetFilterEvaluator.DescribeRelation(Source, target);
            Debug.Log(
                $"[Attack] POLICY={policy.Kind} allow target={target.name} " +
                $"clip={SafeName(Clip.DebugName)} hits={hitCount}");
            Debug.Log(
                $"[Attack] HIT clip={SafeName(Clip.DebugName)} target={target.name} " +
                $"kind={target.UnitKind} relation={relation}");
            if (viaWeaponTrace)
            {
                Debug.Log(
                    $"[Attack] HITRESULT via WeaponTrace target={target.name} bone={bone} " +
                    $"point={point} clip={SafeName(Clip.DebugName)}");
            }
            else
            {
                Debug.Log(
                    $"[Attack] HITRESULT target={target.name} bone={bone} " +
                    $"point={point} normal={normal} clip={SafeName(Clip.DebugName)}");
            }
        }

        CombatEventBus.PublishResolved(in result, in Clip.Reaction);
    }

    /// <summary>关判：Active 区间结束时调用。</summary>
    public void End()
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        IsExpired = true;

        if (Clip.ShapeMode == HitShapeMode.WeaponTrace)
        {
            AttackTraceRegistry.Unregister(Source, this);
        }

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log($"[Attack] END clip={SafeName(Clip.DebugName)}");
        }
    }

    /// <summary>Humanoid 骨骼名；非人形回落 Collider Transform 名。</summary>
    static string ResolveBoneName(Entity target, Transform hitTf)
    {
        if (hitTf == null)
        {
            return "Body";
        }

        var anim = target != null ? target.Animator : null;
        if (anim != null && anim.isHuman)
        {
            for (var t = hitTf; t != null && t != target.transform; t = t.parent)
            {
                for (var b = 0; b < (int)HumanBodyBones.LastBone; b++)
                {
                    var boneTf = anim.GetBoneTransform((HumanBodyBones)b);
                    if (boneTf == t)
                    {
                        return ((HumanBodyBones)b).ToString();
                    }
                }
            }
        }

        return hitTf.name;
    }

    static HitPolicyParams NormalizePolicy(in HitPolicyParams raw)
    {
        var p = raw;
        if (p.MaxHitsPerTarget < 1)
        {
            p.MaxHitsPerTarget = 1;
        }

        if (p.MaxTargets < 1)
        {
            p.MaxTargets = 999;
        }

        if (p.IntervalSeconds < 0.01f)
        {
            p.IntervalSeconds = 0.2f;
        }

        return p;
    }

    static string SafeName(string n) => string.IsNullOrEmpty(n) ? "(unnamed)" : n;
}
