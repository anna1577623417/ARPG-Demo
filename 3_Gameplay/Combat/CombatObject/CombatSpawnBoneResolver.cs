using UnityEngine;

/// <summary>
/// 214.4 — Humanoid 骨骼 Spawn 解析（Runtime + Editor 共用）。
/// </summary>
public static class CombatSpawnBoneResolver
{
    public static bool TryGetBoneTransform(Entity source, SpawnSource sourceKind, out Transform boneTf)
    {
        boneTf = null;
        if (source == null)
        {
            return false;
        }

        var animator = source.Animator;
        if (animator == null || !animator.isHuman)
        {
            return false;
        }

        if (!TryMapBone(sourceKind, out var bone))
        {
            return false;
        }

        boneTf = animator.GetBoneTransform(bone);
        return boneTf != null;
    }

    public static bool TryGetBoneTransform(Transform root, SpawnSource sourceKind, out Transform boneTf)
    {
        boneTf = null;
        if (root == null)
        {
            return false;
        }

        var animator = root.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            return false;
        }

        if (!TryMapBone(sourceKind, out var bone))
        {
            return false;
        }

        boneTf = animator.GetBoneTransform(bone);
        return boneTf != null;
    }

    static bool TryMapBone(SpawnSource sourceKind, out HumanBodyBones bone)
    {
        switch (sourceKind)
        {
            case SpawnSource.SelfHandR:
                bone = HumanBodyBones.RightHand;
                return true;
            case SpawnSource.SelfHandL:
                bone = HumanBodyBones.LeftHand;
                return true;
            case SpawnSource.SelfRootBone:
                bone = HumanBodyBones.Hips;
                return true;
            default:
                bone = HumanBodyBones.Hips;
                return false;
        }
    }
}
