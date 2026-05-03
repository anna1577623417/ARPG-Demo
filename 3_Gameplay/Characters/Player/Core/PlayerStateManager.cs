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
    //   连续状态的"打断许可掩码"
    //   设计动机：Action 状态用归一化时间窗口（ActionWindow）描述打断；
    //   而 Locomotion / Airborne 是无总时长的连续物理状态，没有 t ∈ [0,1] 概念。
    //   这里改用"按物理相位切换的整段掩码"——
    //     · Locomotion：单一掩码（地面相位单一）
    //     · Airborne  ：上升 / 下落两份掩码（按 VerticalSpeed 切换）
    //   每个掩码勾选 StateTag.AllowInterruptBy* 位，由各状态的 TryConsume 与
    //   ActionInterruptResolver.MapIntentToInterruptTag 做位与判定。
    // ───────────────────────────────────────────────────────────────────────────

    [Header("Locomotion — interrupt mask")]
    [Tooltip("地面状态允许哪些意图打断。默认全部允许（保持 v3.1.1 之前行为）。")]
    [StateTagMask]
    [SerializeField, InspectorName("Locomotion — mask")]
    private ulong locomotionAllowedInterrupts =
        (ulong)(StateTag.AllowInterruptByDodge
              | StateTag.AllowInterruptBySwordDash
              | StateTag.AllowInterruptByLight
              | StateTag.AllowInterruptByHeavy
              | StateTag.AllowInterruptByCharged
              | StateTag.AllowInterruptByJump);

    [Header("Airborne — interrupt (ascending)")]
    [Tooltip("空中上升阶段（VerticalSpeed > 0）允许哪些意图打断。默认空 = 起跳后保留动量、不允许任何打断。")]
    [StateTagMask]
    [SerializeField, InspectorName("Airborne up — mask")]
    private ulong airborneAscendingAllowedInterrupts;

    [Tooltip("空中下落阶段（VerticalSpeed ≤ 0）允许哪些意图打断。默认允许各类攻击/闪避；不含 Jump（无二段跳）。")]
    [StateTagMask]
    [SerializeField, InspectorName("Airborne down — mask")]
    private ulong airborneDescendingAllowedInterrupts =
        (ulong)(StateTag.AllowInterruptByDodge
              | StateTag.AllowInterruptBySwordDash
              | StateTag.AllowInterruptByLight
              | StateTag.AllowInterruptByHeavy
              | StateTag.AllowInterruptByCharged);

    [Header("Turn-In-Place (locomotion presentation augmentation)")]
    [Tooltip("原地转身的触发/解锁/分类阈值。详见 TurnSettings 字段 Tooltip。")]
    [SerializeField] private TurnSettings turnSettings = TurnSettings.Default;

    /// <summary>供 Locomotion 每帧传入 <see cref="TurnResolver"/>，使 Inspector 调试开关与阈值即时生效。</summary>
    public TurnSettings LocomotionTurnSettings => turnSettings;

    protected override List<EntityState<Player>> BuildStateList()
    {
        return new List<EntityState<Player>>
        {
            new PlayerLocomotionState(locomotionAllowedInterrupts, turnSettings),
            new PlayerAirborneState(airborneAscendingAllowedInterrupts, airborneDescendingAllowedInterrupts),
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

        Entity.IntentBuffer.FlushExpired(Time.time);

        for (var i = 0; i < maxIntentConsumptionsPerFrame; i++)
        {
            if (!Entity.IntentBuffer.TryPeek(out var intent))
            {
                break;
            }

            var ctx = Entity.BuildFrameContext(deltaTime);
            var canOffer = TransitionResolver.CanOfferIntent(in ctx, in intent, out var rejectReason);
            if (!canOffer)
            {
                if (debugIntentArbitration || Entity.DebugInterruptFlow)
                {
                    Debug.Log(
                        $"[IntentArb] BLOCK by TransitionResolver | state={Current.StateId} | intent={intent.Kind} | reason={rejectReason} | tags=0x{ctx.CurrentTags.Value:X}",
                        this);
                }
                break;
            }

            if (!Current.TryConsumeGameplayIntent(Entity, in ctx, in intent))
            {
                if (debugIntentArbitration || Entity.DebugInterruptFlow)
                {
                    Debug.Log(
                        $"[IntentArb] BLOCK by State gate | state={Current.StateId} | intent={intent.Kind}",
                        this);
                }
                break;
            }

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
