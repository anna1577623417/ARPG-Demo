using UnityEngine;

/// <summary>
/// 216.3 M3 — 一次物理命中的结果（判定层产出）。
/// <para>只回答「打到谁 / 打在哪 / 法线 / 骨骼」；<b>不含</b>伤害数值与交互裁决
/// （裁决 → CombatResolver；表现 → CombatEvent，M3 L2+）。</para>
/// </summary>
public readonly struct HitResult
{
    public readonly Entity Source;
    public readonly Entity Target;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly string BoneName;
    public readonly string ClipDebugName;
    public readonly int HitCountOnTarget;
    public readonly float ElapsedSec;
    public readonly bool HasContactFact;
    public readonly ContactFact ContactFact;
    public readonly bool HasCombatContactFact;
    public readonly CombatContactFact CombatContactFact;

    public HitResult(
        Entity source,
        Entity target,
        Vector3 point,
        Vector3 normal,
        string boneName,
        string clipDebugName,
        int hitCountOnTarget,
        float elapsedSec)
    {
        Source = source;
        Target = target;
        Point = point;
        Normal = normal;
        BoneName = boneName ?? "Body";
        ClipDebugName = clipDebugName ?? "(unnamed)";
        HitCountOnTarget = hitCountOnTarget;
        ElapsedSec = elapsedSec;
        HasContactFact = false;
        ContactFact = default;
        HasCombatContactFact = false;
        CombatContactFact = default;
    }

    public HitResult(
        Entity source,
        Entity target,
        Vector3 point,
        Vector3 normal,
        string boneName,
        string clipDebugName,
        int hitCountOnTarget,
        float elapsedSec,
        in ContactFact contactFact)
    {
        Source = source;
        Target = target;
        Point = point;
        Normal = normal;
        BoneName = boneName ?? "Body";
        ClipDebugName = clipDebugName ?? "(unnamed)";
        HitCountOnTarget = hitCountOnTarget;
        ElapsedSec = elapsedSec;
        HasContactFact = true;
        ContactFact = contactFact;
        HasCombatContactFact = false;
        CombatContactFact = default;
    }

    public HitResult(
        in CombatContactFact fact,
        string debugName)
    {
        Source = fact.Source;
        Target = fact.Target;
        Point = fact.Point;
        Normal = fact.Normal;
        BoneName = fact.BoneName;
        ClipDebugName = debugName ?? fact.EventId;
        HitCountOnTarget = fact.HitCountOnTarget;
        ElapsedSec = fact.ElapsedSeconds;
        HasContactFact = false;
        ContactFact = default;
        HasCombatContactFact = true;
        CombatContactFact = fact;
    }

    public HitResult(
        in CombatContactFact fact,
        in ContactFact legacyContactFact,
        string debugName)
    {
        Source = fact.Source;
        Target = fact.Target;
        Point = fact.Point;
        Normal = fact.Normal;
        BoneName = fact.BoneName;
        ClipDebugName = debugName ?? fact.EventId;
        HitCountOnTarget = fact.HitCountOnTarget;
        ElapsedSec = fact.ElapsedSeconds;
        HasContactFact = true;
        ContactFact = legacyContactFact;
        HasCombatContactFact = true;
        CombatContactFact = fact;
    }

    public bool IsValid => Target != null;
}
