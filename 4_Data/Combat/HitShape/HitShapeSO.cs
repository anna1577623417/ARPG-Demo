using UnityEngine;

/// <summary>技能/阶段命中判定形状（数据层）。</summary>
public abstract class HitShapeSO : ScriptableObject
{
    /// <summary>写入 <paramref name="results"/>，返回命中碰撞体数量。</summary>
    public abstract int Overlap(
        Vector3 origin,
        Quaternion rotation,
        Collider[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers = QueryTriggerInteraction.UseGlobal);
}
