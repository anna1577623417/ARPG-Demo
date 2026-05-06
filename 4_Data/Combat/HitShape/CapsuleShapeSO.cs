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
}
