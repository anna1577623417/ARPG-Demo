#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 214.3/214.4 — Editor 预览用手骨 / Spawn 锚点解析（委托 Runtime 骨骼解析）。
/// </summary>
public static class CombatHitPreviewRig
{
    public static bool TryResolveSpawn(
        Transform anchor,
        SpawnSource source,
        Vector3 localOffset,
        Vector3 localEuler,
        out Vector3 worldPos,
        out Quaternion worldRot)
    {
        worldPos = Vector3.zero;
        worldRot = Quaternion.identity;
        if (anchor == null)
        {
            return false;
        }

        if (CombatSpawnBoneResolver.TryGetBoneTransform(anchor, source, out var boneTf))
        {
            worldPos = boneTf.position + boneTf.rotation * localOffset;
            worldRot = boneTf.rotation * Quaternion.Euler(localEuler);
            return true;
        }

        var anchorPos = anchor.position;
        var anchorRot = anchor.rotation;
        switch (source)
        {
            case SpawnSource.SelfHandR:
                worldPos = anchorPos + anchorRot * (localOffset + new Vector3(0.5f, 1.0f, 0.3f));
                worldRot = anchorRot * Quaternion.Euler(localEuler);
                return true;

            case SpawnSource.SelfHandL:
                worldPos = anchorPos + anchorRot * (localOffset + new Vector3(-0.5f, 1.0f, 0.3f));
                worldRot = anchorRot * Quaternion.Euler(localEuler);
                return true;

            default:
                worldPos = anchorPos + anchorRot * localOffset;
                worldRot = anchorRot * Quaternion.Euler(localEuler);
                return true;
        }
    }
}
#endif
