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

    /// <summary>
    /// 188.3 W0 — 编辑期/调试期画 Wire Gizmo 显示形状几何。
    /// <para>子类各自 Override；基类默认画一个小标记球，便于子类漏实现时仍可看到位置。</para>
    /// <para>调用方负责 <c>Gizmos.color</c> 复位；本方法可重写 color 但不修复。</para>
    /// </summary>
    public virtual void DrawGizmo(Vector3 origin, Quaternion rotation, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin, 0.1f);
    }
}
