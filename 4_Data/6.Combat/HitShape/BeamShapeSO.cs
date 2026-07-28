using UnityEngine;

/// <summary>
/// 188.3 W10 — 线段形状（激光 / 长枪）。
/// <para>从 origin 沿 forward 方向延伸 length 米，宽度 width。底层用 OverlapBox 近似。</para>
/// </summary>
[CreateAssetMenu(menuName = "Combat/HitShape/Beam")]
public class BeamShapeSO : HitShapeSO
{
    [Min(0f)] public float width = 0.5f;
    [Min(0f)] public float length = 10f;
    [Min(0f)] public float height = 1f;
    public Vector3 offset;

    public override int Overlap(
        Vector3 origin,
        Quaternion rotation,
        Collider[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (results == null || results.Length == 0 || length <= 0f || width <= 0f)
        {
            return 0;
        }

        var forward = rotation * Vector3.forward;
        var center = origin + rotation * offset + forward * (length * 0.5f);
        var halfExtents = new Vector3(width * 0.5f, height * 0.5f, length * 0.5f);
        return Physics.OverlapBoxNonAlloc(center, halfExtents, results, rotation, layerMask, queryTriggers);
    }

    public override void DrawGizmo(Vector3 origin, Quaternion rotation, Color color)
    {
        if (length <= 0f || width <= 0f) return;
        var forward = rotation * Vector3.forward;
        var center = origin + rotation * offset + forward * (length * 0.5f);
        var halfExtents = new Vector3(width * 0.5f, height * 0.5f, length * 0.5f);
        var prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.color = color;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = prev;
    }
}
