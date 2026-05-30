using UnityEngine;

/// <summary>
/// Route / Stage Runtime 共享的上下文（每帧由 PlayerStateManager / SkillEntryService 重填，0-GC）。
///
/// ═══ 设计意图 ═══
///   · 把 RouteRuntime 需要的所有外部参考折叠到一个 struct，避免运行时把 Player 透传到底层。
///   · 包含 Stats / Resources / Tags / Input / HitTally 五个面。
///   · HitTally 为 class（容器引用语义），即便 ctx 以 in 传也可写入。
/// </summary>
public struct SkillRouteContext
{
    public Entity Self;
    public Transform SelfTransform;
    public IStatSet Stats;
    public IResourcePool Resources;
    public GameplayTagContainer Tags;
    public InputSnapshot Input;
    public StageHitTally HitTally;
    public bool DebugVerbose;
    public float Now;
    public float DeltaTime;
    public CombatContextSnapshot CombatCtx;
}

/// <summary>
/// 输入面快照（Route 内部唯一可见的输入信号）。
/// 由 PlayerController / SkillEntryService 在 PreLogicUpdate 时填写；RouteRuntime 只读。
/// </summary>
public struct InputSnapshot
{
    public bool TriggerPressedEdge;
    public bool TriggerReleasedEdge;
    public bool TriggerHolding;
    public float TriggerHoldSeconds;
    public SkillEntrySlot TriggerSlot;
    public Vector2 MoveBuffered;
    public bool MoveBufferValid;
}

/// <summary>
/// 本 Stage 已发生的命中累计（class — 由 RouteRuntime 持有，跨 Stage 切换时 RouteRuntime 调 Reset）。
/// 之所以是 class：SkillRouteContext 以 in ref 传递；class 字段可在不破 readonly 语义下被修改。
/// </summary>
public sealed class StageHitTally
{
    public int TotalHits;
    public int HitsHero;
    public int HitsBoss;
    public int HitsMinion;

    public void Reset()
    {
        TotalHits = 0;
        HitsHero = 0;
        HitsBoss = 0;
        HitsMinion = 0;
    }

    public void Record(TransitionTargetRule rule)
    {
        TotalHits++;
        switch (rule)
        {
            case TransitionTargetRule.HeroOnly:   HitsHero++;   break;
            case TransitionTargetRule.Boss:       HitsBoss++;   break;
            case TransitionTargetRule.Minion:     HitsMinion++; break;
        }
    }

    public int CountByRule(TransitionTargetRule rule)
    {
        switch (rule)
        {
            case TransitionTargetRule.Any:       return TotalHits;
            case TransitionTargetRule.HeroOnly:  return HitsHero;
            case TransitionTargetRule.Boss:      return HitsBoss;
            case TransitionTargetRule.Minion:    return HitsMinion;
            case TransitionTargetRule.NonMinion: return HitsHero + HitsBoss;
            default:                              return TotalHits;
        }
    }
}
