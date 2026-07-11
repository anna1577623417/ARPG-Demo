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
    }

    public bool IsValid => Target != null;
}
