using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家状态机管理器 — 4 支柱拓扑的驱动核心。
///
/// ═══ 帧序（每帧 Update）═══
///
/// 1. FlushExpired：清理过期意图
/// 2. TryPeek → BuildFrameContext → TransitionResolver.CanOfferIntent（标签仲裁）
/// 3. Current.TryConsumeGameplayIntent（当前状态决定是否消费并切换）
/// 4. LogicUpdate（当前状态执行逻辑 — 移动/物理/时间轴推进）
///
/// ═══ 支柱拓扑 ═══
///
/// [0] PlayerLocomotionState（默认）
/// [1] PlayerAirborneState
/// [2] PlayerActionState（万能动作）
/// [3] PlayerDeadState（终态）
/// </summary>
[AddComponentMenu("GameMain/Player/Player State Manager")]
public class PlayerStateManager : EntityStateManager<Player>
{
    [SerializeField] private int maxIntentConsumptionsPerFrame = 1;
    [SerializeField] private bool debugIntentArbitration;

    // ───────────────────────────────────────────────────────────────────────────
    //   连续状态的「来袭类别」闸门（ActionCategory 掩码）
    //   Action 用 ActionWindow 的时间切片；Locomotion / Airborne 无归一化时间，
    //   用整段允许的语义类别与 ActionDataSO.Category / 槽位默认映射对齐。
    //   · Locomotion：单份掩码
    //   · Airborne：上升 / 下降两份（按 VerticalSpeed 切换），与旧版上升空掩码、下落全开一致。
    // ───────────────────────────────────────────────────────────────────────────

    const ActionCategory AllPillarCategories =
        ActionCategory.Movement | ActionCategory.Offense | ActionCategory.Defensive | ActionCategory.Utility;

    [Header("Locomotion — interruption (categories)")]
    [Tooltip("地面连续状态下允许的来袭动作类别（与 ActionDataSO.Category 一致；可多选）。")]
    [SerializeField, InspectorName("locomotion_allowed_categories")]
    private ActionCategory locomotionAllowedCategories = AllPillarCategories;

    [Header("Airborne — interruption (ascending)")]
    [Tooltip("上升相（VerticalSpeed > 0）：允许的来袭类别。默认无（对应旧版上升期不打断常见输入）。")]
    [SerializeField, InspectorName("airborne_ascending_allowed_categories")]
    private ActionCategory airborneAscendingAllowedCategories;

    [Tooltip("下降相（VerticalSpeed ≤ 0）：允许的来袭类别。")]
    [SerializeField, InspectorName("airborne_descending_allowed_categories")]
    private ActionCategory airborneDescendingAllowedCategories = AllPillarCategories;

    [Header("Turn-In-Place (locomotion presentation augmentation)")]
    [Tooltip("原地转身的触发/解锁/分类阈值。详见 TurnSettings 字段 Tooltip。")]
    [SerializeField] private TurnSettings turnSettings = TurnSettings.Default;

    /// <summary>供 Locomotion 每帧传入 <see cref="TurnResolver"/>，使 Inspector 调试开关与阈值即时生效。</summary>
    public TurnSettings LocomotionTurnSettings => turnSettings;

    protected override List<EntityState<Player>> BuildStateList()
    {
        return new List<EntityState<Player>>
        {
            new PlayerLocomotionState(locomotionAllowedCategories, turnSettings),
            new PlayerAirborneState(airborneAscendingAllowedCategories, airborneDescendingAllowedCategories),
            new PlayerActionState(),
            new PlayerDeadState(),
        };
    }

    protected override void OnPreLogicUpdate(float deltaTime)
    {
        if (Entity == null || Current == null)
        {
            return;
        }

        Entity.TickSkillRuntimes(deltaTime);
        Entity.IntentBuffer.FlushExpired(Time.time);

        for (var i = 0; i < maxIntentConsumptionsPerFrame; i++)
        {
            if (!Entity.IntentBuffer.TryPeek(out var intent))
            {
                break;
            }

            if (!SkillSystem.TryPrepareIntentForSkills(Entity, ref intent))
            {
                if (debugIntentArbitration || Entity.DebugInterruptFlow)
                {
                    Debug.Log("[IntentArb] BLOCK SkillSystem.CanCast/CD | intent remains queued", this);
                }
                break;
            }

            Entity.IntentBuffer.ReplaceFront(in intent);

            var ctx = Entity.BuildFrameContext(deltaTime);
            var canOffer = TransitionResolver.CanOfferIntent(in ctx, in intent, out var rejectReason);
            if (!canOffer)
            {
                Entity.CancelDeferredSkillPlanning();
                if (debugIntentArbitration || Entity.DebugInterruptFlow)
                {
                    Debug.Log(
                        $"[IntentArb] BLOCK by TransitionResolver | state={Current.StateId} | intent={intent.Kind} | reason={rejectReason} | stateTags=0x{ctx.CurrentTags.Value:X} | abilityTags=0x{ctx.CurrentAbilityTags.Value:X}",
                        this);
                }
                break;
            }

            Entity.FinalizeDeferredSkillPlanning();

            if (!Current.TryConsumeGameplayIntent(Entity, in ctx, in intent))
            {
                Entity.RevertCommittedSkillPlanningAfterFailedConsume();
                if (debugIntentArbitration || Entity.DebugInterruptFlow)
                {
                    Debug.Log(
                        $"[IntentArb] BLOCK by State gate | state={Current.StateId} | intent={intent.Kind}",
                        this);
                }
                break;
            }

            Entity.AcknowledgeCommittedSkillConsumed();

            if (debugIntentArbitration || Entity.DebugInterruptFlow)
            {
                Debug.Log(
                    $"[IntentArb] CONSUMED | state={Current.StateId} | intent={intent.Kind}",
                    this);
            }
            Entity.IntentBuffer.Pop();
        }
    }
}
