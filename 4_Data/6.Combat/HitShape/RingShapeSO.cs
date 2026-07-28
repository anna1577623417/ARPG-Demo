using UnityEngine;

/// <summary>
/// 188.3 W10 — 环带形状（光环 / 扩散环）。
/// <para>仅 InnerRadius~OuterRadius 之间的目标命中（中心圈内不打）。</para>
/// </summary>
[CreateAssetMenu(menuName = "Combat/HitShape/Ring")]
public class RingShapeSO : HitShapeSO
{
    [Min(0f)] public float innerRadius = 1f;
    [Min(0f)] public float outerRadius = 3f;
    public Vector3 offset;

    public override int Overlap(
        Vector3 origin,
        Quaternion rotation,
        Collider[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (results == null || results.Length == 0 || outerRadius <= 0f)
        {
            return 0;
        }

        var center = origin + rotation * offset;
        var count = Physics.OverlapSphereNonAlloc(center, outerRadius, results, layerMask, queryTriggers);

        // 过滤内圈
        var inner2 = innerRadius * innerRadius;
        var write = 0;
        for (var i = 0; i < count && write < results.Length; i++)
        {
            var c = results[i];
            if (c == null) continue;
            var to = c.bounds.center - center;
            to.y = 0f; // 平面距离
            if (to.sqrMagnitude < inner2) continue;
            results[write++] = c;
        }

        for (var i = write; i < count && i < results.Length; i++)
        {
            results[i] = null;
        }
        return write;
    }

    public override void DrawGizmo(Vector3 origin, Quaternion rotation, Color color)
    {
        if (outerRadius <= 0f) return;
        var center = origin + rotation * offset;
        Gizmos.color = color;
        DrawCircle(center, outerRadius, 32);
        if (innerRadius > 0f && innerRadius < outerRadius)
        {
            DrawCircle(center, innerRadius, 24);
        }
    }

    static void DrawCircle(Vector3 center, float radius, int segments)
    {
        var step = Mathf.PI * 2f / segments;
        var prev = center + new Vector3(radius, 0f, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var a = step * i;
            var p = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
