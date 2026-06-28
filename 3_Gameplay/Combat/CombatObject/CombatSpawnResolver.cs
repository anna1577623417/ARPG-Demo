using UnityEngine;

/// <summary>
/// 188.3 W9 — 把 SpawnSource enum 解析为世界空间 (pos, rot)。
/// <para>Player / Entity 接入时调用。CombatObjectRuntime 不直接依赖此类，避免循环。</para>
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
        // Override 优先
        var src = ev.OverrideSpawn ? ev.SpawnSourceOverride : def.SpawnSource;
        var localOffset = ev.OverrideSpawn ? ev.LocalOffsetOverride : def.LocalOffset;
        var localEuler = ev.OverrideSpawn ? ev.LocalEulerOffsetOverride : def.LocalEulerOffset;

        var sourceTf = source != null ? source.transform : null;
        var sourcePos = sourceTf != null ? sourceTf.position : Vector3.zero;
        var sourceRot = sourceTf != null ? sourceTf.rotation : Quaternion.identity;

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
                // 暂用 source 前方 5m 作锁定占位；后续接入锁敌系统
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

    static Vector3 ProjectToGround(Vector3 fromPos)
    {
        // 向下 SphereCast 取地面；找不到时保持原 Y
        if (Physics.Raycast(fromPos + Vector3.up * 0.5f, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            return new Vector3(fromPos.x, hit.point.y, fromPos.z);
        }
        return fromPos;
    }
}
