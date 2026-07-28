using UnityEngine;

[CreateAssetMenu(menuName = "Combat/HitShape/Box")]
public class BoxShapeSO : HitShapeSO
{
    public Vector3 halfExtents = new Vector3(0.5f, 0.5f, 1f);
    public Vector3 offset;

    public override int Overlap(
        Vector3 origin,
        Quaternion rotation,
        Collider[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (results == null || results.Length == 0)
        {
            return 0;
        }

        var center = origin + rotation * offset;
        return Physics.OverlapBoxNonAlloc(center, halfExtents, results, rotation, layerMask, queryTriggers);
    }

    public override int Sweep(
        Vector3 from,
        Vector3 to,
        Quaternion rotation,
        RaycastHit[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (results == null || results.Length == 0)
        {
            return 0;
        }

        var delta = to - from;
        var dist = delta.magnitude;
        if (dist < 1e-4f)
        {
            return 0;
        }

        var center = from + rotation * offset;
        return Physics.BoxCastNonAlloc(center, halfExtents, delta / dist, results, rotation, dist, layerMask, queryTriggers);
    }

    public override void DrawGizmo(Vector3 origin, Quaternion rotation, Color color)
    {
        if (halfExtents.x <= 0f && halfExtents.y <= 0f && halfExtents.z <= 0f) return;
        var center = origin + rotation * offset;
        var prevMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = prevMatrix;
    }
}
