using UnityEngine;

/// <summary>
/// 214.3 — CombatObject 命中诊断 Log（前缀固定 [CombatHit]）。
/// 开关：<see cref="GameMainDebugSettings.CombatHit"/>。
/// </summary>
public static class CombatHitDiagProbe
{
    public const string Prefix = "[CombatHit]";

    public static bool IsEnabled => GameMainDebugSettings.CombatHit;

    public static void LogSpawn(
        CombatObjectDefinitionSO def,
        Entity source,
        Vector3 pos,
        string debugLabel = null)
    {
        if (!IsEnabled || def == null)
        {
            return;
        }

        var shape = def.Shape != null ? def.Shape.GetType().Name : "null";
        var label = string.IsNullOrEmpty(debugLabel) ? def.name : debugLabel;
        Debug.Log(
            $"{Prefix} SPAWN def={def.name} shape={shape} src={FormatEntity(source)} " +
            $"pos={pos} mov={def.Movement.Kind} label={label}");
    }

    public static void LogOverlap(Entity target, int hitIndexOnTarget, Collider col)
    {
        if (!IsEnabled || target == null)
        {
            return;
        }

        var colName = col != null ? col.name : "?";
        Debug.Log(
            $"{Prefix} OVERLAP target={FormatEntity(target)} hitIdx={hitIndexOnTarget} col={colName}");
    }

    public static void LogDamage(Entity target, float amount, DamageKind kind)
    {
        if (!IsEnabled || target == null)
        {
            return;
        }

        Debug.Log($"{Prefix} DAMAGE target={FormatEntity(target)} amount={amount:F1} kind={kind}");
    }

    public static void LogReject(string reason, CombatObjectDefinitionSO def = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        var name = def != null ? def.name : "?";
        Debug.LogWarning($"{Prefix} REJECT def={name} reason={reason}");
    }

    static string FormatEntity(Entity e) => e != null ? e.name : "null";
}
