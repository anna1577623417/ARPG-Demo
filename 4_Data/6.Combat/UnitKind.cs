/// <summary>
/// 217.2 L1 — 对战单位主类（MOBA/ARPG 通用语义）。
/// <para>一条实体主类唯一；与 Faction/Team 正交，由 <see cref="TargetProfileEvaluator"/> 消费。</para>
/// </summary>
public enum UnitKind : byte
{
    Hero = 0,
    HeroClone = 1,
    Summon = 2,
    Minion = 3,
    Monster = 4,
    Structure = 5,
    Ward = 6,
    Prop = 7,
    ProjectileProxy = 8,
}
