using UnityEngine;

[CreateAssetMenu(menuName = "Combat/HitShape/Sphere")]
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
}
