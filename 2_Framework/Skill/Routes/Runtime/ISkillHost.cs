using UnityEngine;

/// <summary>
/// 220.5 B3：SkillEntryService 面向实体宿主的最小能力契约。
/// <para>本阶段先固定 Loadout、时间、标签、PendingAction 与战斗上下文边界；</para>
/// <para>Service 内部的 Player-only Graph/HUD/Combo 依赖在后续小 Landing 逐步迁移。</para>
/// </summary>
public interface ISkillHost
{
    Entity Entity { get; }
    SkillEntryLoadoutSO SkillEntryLoadout { get; }
    GameplayTagContainer Tags { get; }
    InputSemanticResolver InputSemantic { get; }
    float SkillTime { get; }

    CombatContextSnapshot BuildCombatContext(
        bool hitConfirmedThisStage,
        Vector2 moveOverride,
        bool moveOverrideValid);

    void ArmPendingAction(
        GameplayIntentKind kind,
        ActionDataSO action,
        float normalizedStart = 0f);

    ActionDataSO PeekPendingAction();
    void ClearPendingAction();
    void NotifyRouteStageAction(ActionDataSO action);
    void RemoveTag(TagCategory category, ulong bits);
}
