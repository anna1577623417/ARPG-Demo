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
public class PlayerStateManager : EntityStateManager<Player>, IEntityIntentArbitrationPort<Player>
{
    [SerializeField] int maxIntentConsumptionsPerFrame = 1;

    const ActionCategory AllPillarCategories =
        ActionCategory.Movement | ActionCategory.Offense | ActionCategory.Defensive | ActionCategory.Utility
        | ActionCategory.Locomotion;

    [Header("Locomotion — interruption (categories)")]
    [SerializeField] ActionCategory locomotionAllowedCategories = AllPillarCategories;

    [Header("Airborne — System Hard Floor (168.3)")]
    [Tooltip("硬下限：仅声明【绝对禁止】的类别（Cinematic / Dead / 强制不可动）。\n" +
             "默认 None = 不参与判定；正常情况完全不该填。\n" +
             "空中状态可中断画像已下沉到 SkillEntryLoadoutSO.AirInterruptPolicy。")]
    [SerializeField] ActionCategory airborneHardFloorBlock = ActionCategory.None;

    [Header("Turn-In-Place")]
    [SerializeField] TurnSettings turnSettings = TurnSettings.Default;

    public TurnSettings LocomotionTurnSettings => turnSettings;

    public IIntentHost IntentHost => Entity;
    public ISkillHost SkillHost => Entity;
    public SkillEntryService SkillEntries => Entity?.SkillEntries;
    public IActionIntentCommitter ActionCommitter => Entity;
    public int MaxIntentConsumptionsPerFrame => maxIntentConsumptionsPerFrame;

    protected override List<EntityState<Player>> BuildStateList()
    {
        // 168.3 修订：PlayerStateManager 不再持有空中 allowed mask，仅传硬下限。
        // 空中可中断画像由 SkillEntryLoadoutSO.AirInterruptPolicy 提供。
        return new List<EntityState<Player>>
        {
            new PlayerLocomotionState(locomotionAllowedCategories, turnSettings),
            new PlayerAirborneState(airborneHardFloorBlock),
            new PlayerActionState(),
            new PlayerDeadState(),
        };
    }

    protected override void OnPreLogicUpdate(float deltaTime)
    {
        if (Entity == null || Current == null) return;

        ClashSession.Tick(Time.time);
        EntityIntentArbitrationPipeline.Tick(this, deltaTime, Time.time);

        // ─── 158.2 L2：ControlOwner 可观测写入（不参与裁决；仅供 Debug / Profiler）───
        WriteControlOwnerObservable();
    }

    public FrameContext BuildFrameContext(float deltaTime) => Entity.BuildFrameContext(deltaTime);

    public InputSnapshot BuildInputSnapshot(in GameplayIntent intent) => BuildPlayerInputSnapshot(in intent);

    public bool IsRouteAllowed(SkillRouteRuntime route, out string reason)
    {
        reason = null;
        return route != null;
    }

    public void LogTransitionBlocked(in GameplayIntent intent, string reason)
    {
        if (GameMainDebugSettings.IntentArbitration || GameMainDebugSettings.InterruptFlow)
        {
            Debug.Log($"[IntentArb] BLOCK by TransitionResolver | state={Current.StateId} | intent={intent.Kind} | reason={reason}", this);
        }
        InputActionProbe.LogIntentDropped(Entity, intent.Kind.ToString(), "TransitionResolver", reason ?? "(no-reason)");
    }

    public void LogResolveBlocked(in GameplayIntent intent, in ArbitrationDecision decision)
    {
        if (decision.DiscardIntent)
        {
            InputActionProbe.LogIntentDropped(Entity, intent.Kind.ToString(), "SkillEntry.Resolve", $"no-route-discard sem={intent.Semantic}");
            return;
        }

        if (GameMainDebugSettings.IntentArbitration || GameMainDebugSettings.InterruptFlow)
        {
            Debug.Log($"[IntentArb] BLOCK by SkillEntry resolve | intent={intent.Kind}", this);
        }
        InputActionProbe.LogIntentDropped(Entity, intent.Kind.ToString(), "SkillEntry.Resolve", $"no-route-queued sem={intent.Semantic}");
    }

    public void LogRouteRejected(in GameplayIntent intent, SkillRouteRuntime route, string reason)
    {
        InputActionProbe.LogIntentDropped(Entity, intent.Kind.ToString(), "Route.Policy", reason);
    }

    public void LogCommitBlocked(in GameplayIntent intent, SkillRouteRuntime route, string reason)
    {
        InputActionProbe.LogIntentDropped(Entity, intent.Kind.ToString(), "Action.Commit", reason);
    }

    public void LogStateGateBlocked(in GameplayIntent intent, SkillRouteRuntime route, string reason)
    {
        if (GameMainDebugSettings.IntentArbitration || GameMainDebugSettings.InterruptFlow)
        {
            Debug.Log($"[IntentArb] BLOCK by State gate | state={Current.StateId} | intent={intent.Kind} hold={intent.HoldDurationSeconds:F3} (intent stays queued)", this);
        }
        InputActionProbe.LogIntentDropped(Entity, intent.Kind.ToString(), "State.TryConsume", $"state={reason} sem={intent.Semantic} hold={intent.HoldDurationSeconds:F3}");
    }

    public void LogResolved(in GameplayIntent intent, in ArbitrationDecision decision)
    {
        if (SkillRouteDebug.IsDodge4TraceIntent(in intent))
        {
            SkillRouteDebug.LogDodge4(
                Entity,
                "Arbiter",
                $"RESOLVED intent={intent.Kind} semantic={intent.Semantic} axis={intent.DirectionAxis} " +
                $"→ route={decision.Route?.Definition?.name} action={decision.FirstAction?.name ?? "-"}");
        }

        if (GameMainDebugSettings.IntentArbitration)
        {
            Debug.Log($"[Lane] Combat→Graph intent={intent.Kind} route={decision.Route?.Definition?.name}", this);
        }
    }

    public void LogConsumed(in GameplayIntent intent, SkillRouteRuntime route, string reason)
    {
        if (GameMainDebugSettings.IntentArbitration || GameMainDebugSettings.InterruptFlow)
        {
            var consumedNote = intent.Kind == GameplayIntentKind.Move ? " → Locomotion" : string.Empty;
            Debug.Log($"[IntentArb] CONSUMED intent={intent.Kind}{consumedNote} | state={Current.StateId}", this);
        }
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
        if (GameMainDebugSettings.Locomotion)
        {
            Debug.Log(
                $"[LocoOwner] Owner: {prev} → {nextOwner} | state={Current.StateId} | frame={Time.frameCount}",
                this);
        }
    }

    InputSnapshot BuildPlayerInputSnapshot(in GameplayIntent intent)
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
