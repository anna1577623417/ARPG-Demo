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
}
