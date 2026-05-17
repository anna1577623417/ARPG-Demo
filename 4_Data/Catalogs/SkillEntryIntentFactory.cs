using UnityEngine;

/// <summary>
/// 槽位 → GameplayIntent 的工厂（替代旧 PlayerIntentCatalog）。
///
/// ═══ 设计契约 ═══
///   · 不存任何 SkillData / SkillRoute 引用 — 工厂只负责"形状"，类型由 SkillEntryService 在仲裁阶段决定。
///   · Buffer 时长按 EntrySlot 分级（默认 0.18s；位移类如 Space 给 0.28s）。
///   · 输入要求标签由本工厂集中维护，避免散落到 Controller。
/// </summary>
public static class SkillEntryIntentFactory
{
    public static float ResolveBufferSeconds(SkillEntrySlot slot)
    {
        switch (slot)
        {
            case SkillEntrySlot.Shift:
            case SkillEntrySlot.Space:
                return 0.28f;
            default:
                return 0.18f;
        }
    }

    public static GameplayIntent ForEntry(
        SkillEntrySlot slot,
        float time,
        float holdSeconds = 0f,
        Vector2 moveBuffered = default,
        bool moveBufferValid = false)
    {
        var buffer = ResolveBufferSeconds(slot);
        var forbidden = (ulong)StateTag.Dead;
        ulong requiredAbility = ResolveRequiredAbilityTags(slot);
        return GameplayIntent.ForEntry(
            slot, time, buffer,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: forbidden,
            requiredAllAbility: requiredAbility,
            forbiddenAbility: 0UL,
            holdSeconds: holdSeconds,
            moveBuffered: moveBuffered,
            moveBufferValid: moveBufferValid);
    }

    /// <summary>
    /// Ver4.3.7+ Resolver 专用：附带 SemanticType / DirectionAxis / ComboIndex 的完整 intent。
    /// Phase B 期间与旧 <see cref="ForEntry"/> 并存；Phase C 切换后旧重载会被本方法接管。
    /// </summary>
    public static GameplayIntent ForEntryWithSemantic(
        SkillEntrySlot slot,
        float time,
        InputSemanticType semantic,
        float holdSeconds = 0f,
        int comboIndex = 0,
        Vector2 directionAxis = default,
        Vector2 moveBuffered = default,
        bool moveBufferValid = false)
    {
        var intent = ForEntry(slot, time, holdSeconds, moveBuffered, moveBufferValid);
        intent.Semantic = semantic;
        intent.DirectionAxis = directionAxis;
        intent.ComboIndex = comboIndex;
        return intent;
    }

    /// <summary>
    /// Ver4.3.7+ 兜底：当 InputSemanticResolver 不可用时（例如离散槽位但 cfg 未注入），
    /// 仍把 <see cref="GameplayIntent.Semantic"/> 标记为 <see cref="InputSemanticType.Tap"/>
    /// 以让 SkillEntryService 单轨 Resolve 不再走"无 Semantic 兼容路径"。
    /// </summary>
    public static GameplayIntent ForEntryTapFallback(SkillEntrySlot slot, float time,
        float holdSeconds = 0f, Vector2 moveBuffered = default, bool moveBufferValid = false)
    {
        return ForEntryWithSemantic(slot, time, InputSemanticType.Tap,
            holdSeconds: holdSeconds, comboIndex: 0,
            moveBuffered: moveBuffered, moveBufferValid: moveBufferValid);
    }

    public static GameplayIntent ForJump(float time)
    {
        var forbidden = (ulong)StateTag.Dead;
        return GameplayIntent.ForJump(time, 0.12f, requiredAll: 0UL, forbidden: forbidden);
    }

    static ulong ResolveRequiredAbilityTags(SkillEntrySlot slot)
    {
        switch (slot)
        {
            case SkillEntrySlot.Q:     return (ulong)EntityCapabilityTag.CanCastAbility1;
            case SkillEntrySlot.R:     return (ulong)EntityCapabilityTag.CanCastUltimate;
            case SkillEntrySlot.Shift: return (ulong)EntityCapabilityTag.CanSwordDash;
            default:                                return 0UL;
        }
    }
}
