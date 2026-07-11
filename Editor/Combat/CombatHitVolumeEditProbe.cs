#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 214.5 — Scene 攻击盒编辑诊断（前缀 [CombatHitEdit]）。
/// 开关与运行时命中共用 <see cref="GameMainDebugSettings.CombatHit"/>。
/// </summary>
public static class CombatHitVolumeEditProbe
{
    public const string Prefix = "[CombatHitEdit]";

    public static bool IsEnabled => GameMainDebugSettings.CombatHit;

    public static void LogActivate(CombatObjectDefinitionSO def, Transform anchor)
    {
        if (!IsEnabled || def == null)
        {
            return;
        }

        Debug.Log(
            $"{Prefix} ACTIVE def={def.name} anchor={(anchor != null ? anchor.name : "null")} " +
            $"spawn={def.SpawnSource} offset={def.LocalOffset} euler={def.LocalEulerOffset}");
    }

    public static void LogTransform(
        string op,
        CombatObjectDefinitionSO def,
        Vector3 worldPos,
        Quaternion worldRot,
        Vector3 localOffset,
        Vector3 localEuler)
    {
        if (!IsEnabled || def == null)
        {
            return;
        }

        Debug.Log(
            $"{Prefix} {op} def={def.name} worldPos={worldPos} worldRot={worldRot.eulerAngles} " +
            $"localOffset={localOffset} localEuler={localEuler}");
    }

    public static void LogScale(HitShapeSO shape, string detail)
    {
        if (!IsEnabled || shape == null)
        {
            return;
        }

        Debug.Log($"{Prefix} SCALE shape={shape.name} ({shape.GetType().Name}) {detail}");
    }
}
#endif
