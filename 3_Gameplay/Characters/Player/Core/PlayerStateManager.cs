using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家状态机管理器（Ver4.3.6+） — 4 支柱拓扑驱动核心。
///
/// ═══ 帧序（每帧 Update） ═══
///   1. SkillEntries.TickCooldowns(dt)：所有 RouteRuntime CD 减秒
///   2. IntentBuffer.FlushExpired(now)
///   3. TryPeek → BuildFrameContext → TransitionResolver.CanOfferIntent
///   4. SkillEntries.TryResolveForIntent → RouteRuntime.CanCast
///   5. Current.TryConsumeGameplayIntent（IntentRouter.Route 切支柱）
///   6. SkillEntries.NotifyRouteEntered（提交 Active Route）
///   7. LogicUpdate（当前支柱推进 — Action.Tick 内会 SkillEntries.TickActive）
/// </summary>
[AddComponentMenu("GameMain/Player/Player State Manager")]
public class PlayerStateManager : EntityStateManager<Player>
{
    [SerializeField] int maxIntentConsumptionsPerFrame = 1;
    [SerializeField] bool debugIntentArbitration;

    const ActionCategory AllPillarCategories =
        ActionCategory.Movement | ActionCategory.Offense | ActionCategory.Defensive | ActionCategory.Utility
        | ActionCategory.Locomotion;

    [Header("Locomotion — interruption (categories)")]
    [SerializeField] ActionCategory locomotionAllowedCategories = AllPillarCategories;

    [Header("Airborne — interruption (ascending / descending)")]
    [SerializeField] ActionCategory airborneAscendingAllowedCategories;
    [SerializeField] ActionCategory airborneDescendingAllowedCategories = AllPillarCategories;

    [Header("Turn-In-Place")]
    [SerializeField] TurnSettings turnSettings = TurnSettings.Default;

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
        if (Entity == null || Current == null) return;

        Entity.SkillEntries?.TickCooldowns(deltaTime);
        Entity.IntentBuffer.FlushExpired(Time.time);

        for (var i = 0; i < maxIntentConsumptionsPerFrame; i++)
        {
            if (!Entity.IntentBuffer.TryPeek(out var intent)) break;

            // ─── 1) 资格闸门：TransitionResolver 标签 + 过期 ───
            var ctx = Entity.BuildFrameContext(deltaTime);
            if (!TransitionResolver.CanOfferIntent(in ctx, in intent, out var reason))
            {
                if (debugIntentArbitration || Entity.DebugInterruptFlow)
                {
                    Debug.Log($"[IntentArb] BLOCK by TransitionResolver | state={Current.StateId} | intent={intent.Kind} | reason={reason}", this);
                }
                break;
            }

            // ─── 2) Skill 解析：仅 Combat 车道走 SkillEntryService（157.2 L3）───
            SkillRouteRuntime resolvedRoute = null;
            var lane = ActionIntentRouting.ResolveLane(in intent, pendingAction: null);
            if (lane == ActionIntentCategory.Combat)
            {
                var inputSnap = BuildInputSnapshot(in intent);
                var discardIntent = false;
                resolvedRoute = Entity.SkillEntries?.TryResolveForIntent(
                    in intent, in inputSnap, Time.time, out discardIntent);

                if (resolvedRoute == null)
                {
                    Entity.ClearPendingAction();
                    if (discardIntent)
                    {
                        Entity.IntentBuffer.Pop();
                        continue;
                    }

                    if (debugIntentArbitration || Entity.DebugInterruptFlow)
                    {
                        Debug.Log($"[IntentArb] BLOCK by SkillEntry resolve | intent={intent.Kind}", this);
                    }
                    break;
                }

                // 把首段 Action 注入 PendingAction（MultiStage 跨次直进 Stage1 等）
                var firstStage = Entity.SkillEntries.ResolveStartStage(resolvedRoute, Time.time);
                if (firstStage?.Action != null)
                {
                    Entity.ArmPendingAction(intent.Kind, firstStage.Action);
                }

                if (SkillRouteDebug.IsDodge4TraceIntent(in intent))
                {
                    SkillRouteDebug.LogDodge4(
                        Entity,
                        "Arbiter",
                        $"RESOLVED intent={intent.Kind} semantic={intent.Semantic} axis={intent.DirectionAxis} " +
                        $"→ route={resolvedRoute?.Definition?.name} stage={firstStage?.name}");
                }

                if (debugIntentArbitration)
                {
                    Debug.Log($"[Lane] Combat→Graph intent={intent.Kind} route={resolvedRoute?.Definition?.name}", this);
                }
            }
            else if (debugIntentArbitration || Entity.DebugInterruptFlow)
            {
                Debug.Log($"[Lane] {lane}→Global intent={intent.Kind}", this);
            }

            // ─── 3) 当前支柱本地闸门 ───
            if (!Current.TryConsumeGameplayIntent(Entity, in ctx, in intent))
            {
                Entity.ClearPendingAction();
                if (debugIntentArbitration || Entity.DebugInterruptFlow)
                {
                    Debug.Log($"[IntentArb] BLOCK by State gate | state={Current.StateId} | intent={intent.Kind} hold={intent.HoldDurationSeconds:F3} (intent stays queued)", this);
                }
                break;
            }

            // ─── 4) 提交：SkillEntries 进入 RouteRuntime ───
            if (resolvedRoute != null && Entity.SkillEntries != null
                && GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))
            {
                Entity.SkillEntries.NotifyRouteEntered(resolvedRoute, slot);
            }

            if (SkillRouteDebug.IsDodge4TraceIntent(in intent) && resolvedRoute == null)
            {
                SkillRouteDebug.LogDodge4Warn(
                    Entity,
                    "Arbiter",
                    $"NO_ROUTE intent={intent.Kind} semantic={intent.Semantic} axis={intent.DirectionAxis}");
            }

            if (debugIntentArbitration || Entity.DebugInterruptFlow)
            {
                var consumedNote = intent.Kind == GameplayIntentKind.Move ? " → Locomotion" : string.Empty;
                Debug.Log($"[IntentArb] CONSUMED intent={intent.Kind}{consumedNote} | state={Current.StateId}", this);
            }

            Entity.IntentBuffer.Pop();
        }

        // ─── 158.2 L2：ControlOwner 可观测写入（不参与裁决；仅供 Debug / Profiler）───
        WriteControlOwnerObservable();
    }

    /// <summary>
    /// 158.2 §6.2：当前支柱 → ControlOwner 映射；只观测，不裁决。
    /// PlayerActionState → Action；其余支柱 → Locomotion；CutsceneState 占位（暂未引入）。
    /// </summary>
    void WriteControlOwnerObservable()
    {
        if (Entity == null || Current == null) return;

        var nextOwner = Current is PlayerActionState
            ? ControlOwner.Action
            : ControlOwner.Locomotion;

        var prev = Entity.CurrentControlOwner;
        if (prev == nextOwner) return;

        Entity.CurrentControlOwner = nextOwner;
        if (Entity.DebugLocomotion)
        {
            Debug.Log(
                $"[LocoOwner] Owner: {prev} → {nextOwner} | state={Current.StateId} | frame={Time.frameCount}",
                this);
        }
    }

    InputSnapshot BuildInputSnapshot(in GameplayIntent intent)
    {
        var reader = Entity?.InputReader;
        InputSnapshot snap = default;
        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out snap.TriggerSlot))
        {
            return snap;
        }

        snap.TriggerSlot = CanonicalEntry(snap.TriggerSlot);
        snap.TriggerHoldSeconds = intent.HoldDurationSeconds;
        snap.MoveBuffered = intent.MoveBuffered;
        snap.MoveBufferValid = intent.MoveBufferValid;

        if (reader != null)
        {
            snap.TriggerHolding = reader.IsSkillEntryHeld(snap.TriggerSlot);
            // 本函数由"当前待消费 intent"驱动，边沿严格依意图语义判定：
            // - PressedEdge: 无 hold（按下帧入队）
            // - ReleasedEdge: 带 hold 且当前已不按住（松开帧入队）
            // 这样与 PlayerActionState 内部 TriggerReleasedEdge 语义一致，不再出现两套标准。
            var hasHoldPayload = snap.TriggerHoldSeconds > 0.0001f;
            snap.TriggerPressedEdge = true;
            snap.TriggerReleasedEdge = hasHoldPayload && !snap.TriggerHolding;
        }

        return snap;
    }

    static SkillEntrySlot CanonicalEntry(SkillEntrySlot slot)
    {
        return (int)slot == 2 ? SkillEntrySlot.LM : slot;
    }
}
