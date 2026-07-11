/// <summary>214.4 — CombatObject 目标过滤种类（内嵌于 <see cref="TargetFilterParams"/>）。</summary>
public enum TargetFilterKind : byte
{
    /// <summary>任意目标，排除施法者自己。</summary>
    AnyExceptSelf = 0,

    /// <summary>阵营敌对（Faction 轨或 TeamId 不同）。</summary>
    HostileOnly = 1,

    /// <summary>阵营友方（治疗 / 增益）。</summary>
    FriendlyOnly = 2,

    /// <summary>仅施法者自己。</summary>
    SelfOnly = 3,
}
