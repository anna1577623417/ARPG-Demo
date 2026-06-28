using UnityEngine;

/// <summary>
/// MotionProfile 驱动位移时，剥离 Clip 在采样骨（默认 Hips）上的平面平移，
/// 使 Motor / Profile 独占 XZ 位移，骨骼只保留姿态与垂直偏移。
/// 与运行时 <c>applyRootMotion=false</c> + MotionExecutor 契约一致。
/// </summary>
public static class MotionProfileInPlaceBoneCompensator
{
    public static bool TryResolveHipsBone(Transform anchor, out Transform hips)
    {
        hips = null;
        if (anchor == null)
        {
            return false;
        }

        var animator = anchor.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                return true;
            }
        }

        hips = FindChildByName(anchor, "Hips");
        return hips != null;
    }

    public static Vector3 ReadHipsLocalOnAnchor(Transform anchor, Transform hips)
    {
        return anchor != null && hips != null
            ? anchor.InverseTransformPoint(hips.position)
            : Vector3.zero;
    }

    /// <summary>将 Hips 在 Anchor 局部空间的 XZ 复位到 baseline，保留 Y（蹲伏等姿态）。</summary>
    public static void CompensateHipsPlanarFromBaseline(
        Transform anchor,
        Transform hips,
        in Vector3 baselineHipsLocalOnAnchor)
    {
        if (anchor == null || hips == null)
        {
            return;
        }

        var local = anchor.InverseTransformPoint(hips.position);
        local.x = baselineHipsLocalOnAnchor.x;
        local.z = baselineHipsLocalOnAnchor.z;
        hips.position = anchor.TransformPoint(local);
    }

    static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var found = FindChildByName(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
