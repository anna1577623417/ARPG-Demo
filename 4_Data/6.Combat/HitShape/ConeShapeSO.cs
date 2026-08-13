using UnityEngine;

/// <summary>
/// 圆锥形粗检：先球形 BroadPhase，再角度筛选；结果写入 buffer 前若干项（可能小于物理返回数）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Combat/Hit Shape/Cone")]
public class ConeShapeSO : HitShapeSO
{
    [Tooltip("半角（度）")]
    public float angleDegrees = 45f;

    public float length = 3f;
    public Vector3 offset;

    public override int Overlap(
        Vector3 origin,
        Quaternion rotation,
        Collider[] results,
        int layerMask,
        QueryTriggerInteraction queryTriggers)
    {
        if (results == null || results.Length == 0 || length <= 0f)
        {
            return 0;
        }

        var basePos = origin + rotation * offset;
        var forward = rotation * Vector3.forward;
        var cosLimit = Mathf.Cos(angleDegrees * Mathf.Deg2Rad);
        var sphereRadius = length;
        var count = Physics.OverlapSphereNonAlloc(basePos, sphereRadius, results, layerMask, queryTriggers);

        var fwd = new Vector3(forward.x, 0f, forward.z);
        if (fwd.sqrMagnitude < 1e-8f)
        {
            fwd = Vector3.forward;
        }
        else
        {
            fwd.Normalize();
        }

        var write = 0;
        for (var i = 0; i < count && write < results.Length; i++)
        {
            var c = results[i];
            if (c == null)
            {
                continue;
            }

            var to = c.bounds.center - basePos;
            var flat = new Vector3(to.x, 0f, to.z);
            var dist = flat.magnitude;
            if (dist > length + c.bounds.extents.magnitude)
            {
                continue;
            }

            if (dist < 1e-6f)
            {
                results[write++] = c;
                continue;
            }

            var dir = flat / dist;
            if (Vector3.Dot(dir, fwd) < cosLimit)
            {
                continue;
            }

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
        if (length <= 0f || angleDegrees <= 0f) return;
        var basePos = origin + rotation * offset;
        var forward = rotation * Vector3.forward;
        Gizmos.color = color;
        // 扇形左右两条边
        var leftRot = Quaternion.AngleAxis(-angleDegrees, Vector3.up) * forward;
        var rightRot = Quaternion.AngleAxis(angleDegrees, Vector3.up) * forward;
        Gizmos.DrawLine(basePos, basePos + leftRot * length);
        Gizmos.DrawLine(basePos, basePos + rightRot * length);
        // 圆弧近似：分 12 段
        const int seg = 12;
        var prev = leftRot;
        var step = (angleDegrees * 2f) / seg;
        for (var i = 1; i <= seg; i++)
        {
            var ang = -angleDegrees + step * i;
            var dir = Quaternion.AngleAxis(ang, Vector3.up) * forward;
            Gizmos.DrawLine(basePos + prev * length, basePos + dir * length);
            prev = dir;
        }
    }
}
