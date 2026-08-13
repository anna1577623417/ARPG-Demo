using UnityEngine;

[CreateAssetMenu(menuName = "GameMain/Combat/Hit Shape/Sphere")]
public class SphereShapeSO : HitShapeSO
{
    public float radius = 1f;

    public override int Overlap(
        Vector3 origin,
        Quaternion rotation,
        Collider[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (results == null || results.Length == 0 || radius <= 0f)
        {
            return 0;
        }

        return Physics.OverlapSphereNonAlloc(origin, radius, results, layerMask, queryTriggers);
    }

    public override int Sweep(
        Vector3 from,
        Vector3 to,
        Quaternion rotation,
        RaycastHit[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (results == null || results.Length == 0 || radius <= 0f)
        {
            return 0;
        }

        var delta = to - from;
        var dist = delta.magnitude;
        if (dist < 1e-4f)
        {
            return 0;
        }

        return Physics.SphereCastNonAlloc(from, radius, delta / dist, results, dist, layerMask, queryTriggers);
    }

    public override void DrawGizmo(Vector3 origin, Quaternion rotation, Color color)
    {
        if (radius <= 0f) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin, radius);
    }
}
