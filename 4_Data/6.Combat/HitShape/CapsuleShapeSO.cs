using UnityEngine;

[CreateAssetMenu(menuName = "Combat/HitShape/Capsule")]
public class CapsuleShapeSO : HitShapeSO
{
    public float radius = 0.35f;
    public float height = 1.6f;
    public Vector3 offset;

    public override int Overlap(
        Vector3 origin,
        Quaternion rotation,
        Collider[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (results == null || results.Length == 0 || height <= 0f || radius <= 0f)
        {
            return 0;
        }

        var axis = rotation * Vector3.up;
        var half = Mathf.Max(height * 0.5f - radius, 0f);
        var center = origin + rotation * offset;
        var p0 = center + axis * half;
        var p1 = center - axis * half;
        return Physics.OverlapCapsuleNonAlloc(p0, p1, radius, results, layerMask, queryTriggers);
    }

    public override int Sweep(
        Vector3 from,
        Vector3 to,
        Quaternion rotation,
        RaycastHit[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (results == null || results.Length == 0 || height <= 0f || radius <= 0f)
        {
            return 0;
        }

        var delta = to - from;
        var dist = delta.magnitude;
        if (dist < 1e-4f)
        {
            return 0;
        }

        var axis = rotation * Vector3.up;
        var half = Mathf.Max(height * 0.5f - radius, 0f);
        var center = from + rotation * offset;
        var p0 = center + axis * half;
        var p1 = center - axis * half;
        return Physics.CapsuleCastNonAlloc(p0, p1, radius, delta / dist, results, dist, layerMask, queryTriggers);
    }

    public override void DrawGizmo(Vector3 origin, Quaternion rotation, Color color)
    {
        if (radius <= 0f || height <= 0f) return;
        var axis = rotation * Vector3.up;
        var half = Mathf.Max(height * 0.5f - radius, 0f);
        var center = origin + rotation * offset;
        var p0 = center + axis * half;
        var p1 = center - axis * half;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(p0, radius);
        Gizmos.DrawWireSphere(p1, radius);
        // 简化：两端球之间画 4 条侧线（前后左右）
        var right = rotation * Vector3.right * radius;
        var forward = rotation * Vector3.forward * radius;
        Gizmos.DrawLine(p0 + right, p1 + right);
        Gizmos.DrawLine(p0 - right, p1 - right);
        Gizmos.DrawLine(p0 + forward, p1 + forward);
        Gizmos.DrawLine(p0 - forward, p1 - forward);
    }
}
