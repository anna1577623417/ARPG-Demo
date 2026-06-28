/// <summary>185.2 — Graph Edge 战斗态势快照（与 Damage.CombatContext 区分；186+ 切片接通数据源）。</summary>
public readonly struct EdgeCombatContext
{
    public readonly bool HasLockOnTarget;
    public readonly bool TargetInFront;
    public readonly bool TargetBehind;
    public readonly bool TargetAirborne;
    public readonly bool NearWall;
    public readonly bool NearLedge;
    public readonly bool EnemyUnaware;
    public readonly bool EnemyBroken;

    public EdgeCombatContext(
        bool hasLockOnTarget,
        bool targetInFront,
        bool targetBehind,
        bool targetAirborne,
        bool nearWall,
        bool nearLedge,
        bool enemyUnaware,
        bool enemyBroken)
    {
        HasLockOnTarget = hasLockOnTarget;
        TargetInFront = targetInFront;
        TargetBehind = targetBehind;
        TargetAirborne = targetAirborne;
        NearWall = nearWall;
        NearLedge = nearLedge;
        EnemyUnaware = enemyUnaware;
        EnemyBroken = enemyBroken;
    }

    public static EdgeCombatContext Default => default;

    public static EdgeCombatContext FromPlayer(Player player)
    {
        if (player == null)
        {
            return default;
        }

        return new EdgeCombatContext(
            hasLockOnTarget: player.IsLockedOn,
            targetInFront: false,
            targetBehind: false,
            targetAirborne: false,
            nearWall: false,
            nearLedge: false,
            enemyUnaware: false,
            enemyBroken: false);
    }

    public bool HasFlag(EdgeCombatContextFlag flag) => flag switch
    {
        EdgeCombatContextFlag.HasLockOnTarget => HasLockOnTarget,
        EdgeCombatContextFlag.TargetInFront => TargetInFront,
        EdgeCombatContextFlag.TargetBehind => TargetBehind,
        EdgeCombatContextFlag.TargetAirborne => TargetAirborne,
        EdgeCombatContextFlag.NearWall => NearWall,
        EdgeCombatContextFlag.NearLedge => NearLedge,
        EdgeCombatContextFlag.EnemyUnaware => EnemyUnaware,
        EdgeCombatContextFlag.EnemyBroken => EnemyBroken,
        _ => false,
    };
}

[System.Flags]
public enum EdgeCombatContextFlag : ushort
{
    None = 0,
    HasLockOnTarget = 1 << 0,
    TargetInFront = 1 << 1,
    TargetBehind = 1 << 2,
    TargetAirborne = 1 << 3,
    NearWall = 1 << 4,
    NearLedge = 1 << 5,
    EnemyUnaware = 1 << 6,
    EnemyBroken = 1 << 7,
}
