using UnityEngine;

/// <summary>
/// 188.3 W9 — 把 SpawnSource enum 解析为世界空间 (pos, rot)。
/// 214.4 — SelfHandR/L/Root 优先 Humanoid 骨骼，无 Rig 时回退根骨偏移。
/// </summary>
public static class CombatSpawnResolver
{
    /// <summary>解析 CombatEvent + Definition 对应的世界 Spawn 位姿。</summary>
    public static void Resolve(
        Entity source,
        CombatObjectDefinitionSO def,
        in CombatEvent ev,
        out Vector3 worldPos,
        out Quaternion worldRot)
    {
        var src = ev.OverrideSpawn ? ev.SpawnSourceOverride : def.SpawnSource;
        var localOffset = ev.OverrideSpawn ? ev.LocalOffsetOverride : def.LocalOffset;
        var localEuler = ev.OverrideSpawn ? ev.LocalEulerOffsetOverride : def.LocalEulerOffset;

        var sourceTf = source != null ? source.transform : null;
        var sourcePos = sourceTf != null ? sourceTf.position : Vector3.zero;
        var sourceRot = sourceTf != null ? sourceTf.rotation : Quaternion.identity;

        if (TryResolveFromBone(source, src, localOffset, localEuler, out worldPos, out worldRot))
        {
            return;
        }

        switch (src)
        {
            case SpawnSource.SelfRootBone:
                worldPos = sourcePos + sourceRot * localOffset;
                worldRot = sourceRot * Quaternion.Euler(localEuler);
                break;

            case SpawnSource.SelfHandR:
                worldPos = sourcePos + sourceRot * (localOffset + new Vector3(0.5f, 1.0f, 0.3f));
                worldRot = sourceRot * Quaternion.Euler(localEuler);
                break;

            case SpawnSource.SelfHandL:
                worldPos = sourcePos + sourceRot * (localOffset + new Vector3(-0.5f, 1.0f, 0.3f));
                worldRot = sourceRot * Quaternion.Euler(localEuler);
                break;

            case SpawnSource.GroundUnderSelf:
                worldPos = ProjectToGround(sourcePos + sourceRot * localOffset);
                worldRot = sourceRot * Quaternion.Euler(localEuler);
                break;

            case SpawnSource.GroundUnderTarget:
                worldPos = ProjectToGround(sourcePos + sourceRot * (Vector3.forward * 5f + localOffset));
                worldRot = sourceRot * Quaternion.Euler(localEuler);
                break;

            case SpawnSource.WorldFromCamera:
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var camFwd = cam.transform.forward;
                    var camPos = cam.transform.position;
                    worldPos = camPos + camFwd * 10f + localOffset;
                    worldRot = Quaternion.LookRotation(camFwd) * Quaternion.Euler(localEuler);
                }
                else
                {
                    worldPos = sourcePos + sourceRot * localOffset;
                    worldRot = sourceRot * Quaternion.Euler(localEuler);
                }
                break;
            }

            case SpawnSource.AtSelfPosition:
                worldPos = sourcePos + localOffset;
                worldRot = sourceRot * Quaternion.Euler(localEuler);
                break;

            default:
                worldPos = sourcePos + sourceRot * localOffset;
                worldRot = sourceRot * Quaternion.Euler(localEuler);
                break;
        }
    }

    static bool TryResolveFromBone(
        Entity source,
        SpawnSource src,
        Vector3 localOffset,
        Vector3 localEuler,
        out Vector3 worldPos,
        out Quaternion worldRot)
    {
        worldPos = Vector3.zero;
        worldRot = Quaternion.identity;

        if (!CombatSpawnBoneResolver.TryGetBoneTransform(source, src, out var boneTf))
        {
            return false;
        }

        worldPos = boneTf.position + boneTf.rotation * localOffset;
        worldRot = boneTf.rotation * Quaternion.Euler(localEuler);
        return true;
    }

    static Vector3 ProjectToGround(Vector3 fromPos)
    {
        if (Physics.Raycast(fromPos + Vector3.up * 0.5f, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            return new Vector3(fromPos.x, hit.point.y, fromPos.z);
        }

        return fromPos;
    }
}
