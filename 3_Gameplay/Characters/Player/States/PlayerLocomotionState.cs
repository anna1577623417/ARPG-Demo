using UnityEngine;

/// <summary>
/// 地面运动支柱（Locomotion Pillar）— Idle/Walk/Run 的合一状态。
///
/// 职责：
/// 1. 维护地面标签（Grounded）与实体能力轨（<see cref="EntityCapabilityTag"/>）
/// 2. 消费意图：Jump → Airborne, Attack/Dodge → Action
/// 3. 驱动移动结算（MoveByLocomotionIntent + ApplyMotor）
/// 4. 【158.2 L4】查询 <see cref="LocomotionResolver"/>，按 <see cref="LocomotionProfile"/> 配置触发 WalkEnd/RunEnd/RunStart 等离散 Action
/// 5. 【162.1】原地转身由 <see cref="TurnResolver"/> 单轨驱动（Detect 不表达 Turn）
/// </summary>
public sealed class PlayerLocomotionState : PlayerState
{
    /// <summary>
    /// 由 <see cref="PlayerStateManager"/> 注入的允许的来袭 <see cref="ActionCategory"/> 掩码。
    /// Locomotion 无归一化时间轴，用类别闸门代替 ActionWindow。
    /// </summary>
    private readonly ActionCategory m_allowedCategories;

    /// <summary>原地转身解析器（仅在本状态生命周期内活跃；离开时 ClearLock）。</summary>
    private readonly TurnResolver m_turnResolver;

    // ─── 158.2 L4：Resolver 决策缓存 + 边沿检测状态 ───
    private LocomotionStateId m_lastResolvedState;
    private ControlOwner m_lastOwnerHint;
    private bool m_lastHadInput;

    // ─── 159.1 L0：起停四态 latch（release 边沿读 m_lastWantsRun；press 边沿读当帧 WantsRun）───
    // 维护时序：每帧 ProcessLocomotionResolve 末尾，hasInput=true 时写入 player.WantsRun。
    private bool m_lastWantsRun;
    private float m_nextMoveTraceTime;

    // 164.1 L4：转身 FIRE 时间戳（仅诊断去重，不阻断 TurnResolver）
    private float m_lastTurnFireTime;
    private bool m_wasTurningLastFrame;

    // 164.1 L3：连续 Locomotion Action 换片去重
    private ActionDataSO m_lastContinuousLocomotionAction;

    // 165.1 L3：End Action 打断回归后抑制边沿检测
    private int m_inputEdgeSuppressFrames;
    private const int EdgeSuppressFramesAfterEnd = 3;

    // 182.1 W4：press→release 按住时长（Tail Segment Tap 判定）
    private float m_movePressStartTime = -1f;

    public PlayerLocomotionState(ActionCategory allowedCategories, in TurnSettings turnSettings)
    {
        m_allowedCategories = allowedCategories;
        m_turnResolver = new TurnResolver(in turnSettings);
    }

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        if (intent.Kind == GameplayIntentKind.Move)
        {
            return true;
        }

        if (!IntentRouter.IsRoutable(in intent))
        {
            return false;
        }

        var incomingAction = IntentRouter.PeekActionDataForRouting(player, in intent);
        var incomingCategory = ActionInterruptResolver.ResolveIncomingCategory(in intent, incomingAction);
        if (incomingCategory != ActionCategory.None && (m_allowedCategories & incomingCategory) == 0)
        {
            if (GameMainDebugSettings.InterruptFlow)
            {
                Debug.Log(
                    $"[Locomotion] REJECT | intent={intent.Kind} | category={incomingCategory} | reason=not in locomotionAllowedCategories",
                    player);
            }
            return false;
        }

        return IntentRouter.Route(player, in intent, forceActionReentry: false);
    }

    protected override void OnEnter(Player player)
    {
        RefreshLocomotionTags(player);
        m_turnResolver.ClearLock("locomotion_enter");
        player.SetTurnInfo(default);
        player.ClearGraphContextAction("enter-locomotion");
        CombatGraphFinisherDiagnostics.EndTrace(player, "Locomotion");
        SyncInputLatchOnEnter(player);
        ApplyEndActionEdgeSuppress(player);
        player.SetLocomotionPresentation(default);
        m_lastContinuousLocomotionAction = null;

        // 164.1 L0 [Jump] 探针：进入 Locomotion 时机（与 Airborne/Action 探针共同重现 Jump 流程）
        if (GameMainDebugSettings.Locomotion)
        {
            Debug.Log(
                $"[Jump][LocoEnter] grounded={player.IsGrounded} hasInput={player.HasMovementIntent} " +
                $"wantsRun={player.WantsRun} frame={Time.frameCount}",
                player);
        }

        Locomotion165Diagnostics.LogLocoEnter(player, player.PlanarVelocity);
    }

    /// <summary>
    /// 163.3：边沿 latch 与真实输入同步（禁止无条件清零）。
    /// Action 结束回归 Locomotion 时若方向键仍按住，须视为「持续输入」而非 press 边沿，否则 WalkStart/RunStart 死循环。
    /// </summary>
    void SyncInputLatchOnEnter(Player player)
    {
        m_lastHadInput = player.HasMovementIntent;
        m_lastWantsRun = player.WantsRun;

        if (LocomotionDebug.IsTraceEnabled(player))
        {
            LocomotionDebug.Log(
                player,
                LocomotionDebug.CatResolve,
                $"OnEnter latch sync hasInput={m_lastHadInput} wantsRun={m_lastWantsRun} " +
                $"(press-edge uses !wasHadInput&&hasInput — must not false-reset after Action)");
        }
    }

    void ApplyEndActionEdgeSuppress(Player player)
    {
        var hint = player.ConsumeLastActionEndStateHint();
        if (hint != LocomotionStateId.WalkEnd && hint != LocomotionStateId.RunEnd)
        {
            return;
        }

        m_inputEdgeSuppressFrames = EdgeSuppressFramesAfterEnd;
        Locomotion165Diagnostics.LogSuppressEdge(player, m_inputEdgeSuppressFrames, hint);
    }

    protected override void OnExit(Player player)
    {
        // 离开 Locomotion 必须清除转身锁定，否则下次回到 Locomotion 第一帧仍会处于"locked"状态。
        m_turnResolver.ClearLock("locomotion_exit");
        player.SetTurnInfo(default);
    }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        if (!player.IsGrounded)
        {
            player.States.Change<PlayerAirborneState>();
            return;
        }

        RefreshLocomotionTags(player);

        if (player.ConsumeTurnPresentationInterruptRequest())
        {
            m_turnResolver.ClearLock("interrupt");
            player.SetTurnInfo(default);
        }

        // ─── 173.1：Ability 输入上下文窗口内冻结 Locomotion 转向 ───
        if (player.ShouldSuppressLocomotionRotation())
        {
            m_turnResolver.ClearLock("ability_context");
            player.SetTurnInfo(default);
        }
        else
        {
            // ─── 162.1：转身意图先于旋转采样（单轨 TurnResolver + Tuning 阈值）───
            var turnSettings = player.States.LocomotionTurnSettings;
            var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
            var turnInfo = m_turnResolver.Tick(player, Time.deltaTime, in turnSettings, tuning);
            player.SetTurnInfo(in turnInfo);
            LogTurnFireDedupIfNeeded(player, in turnInfo);

            if (turnSettings.DrawTurnDebugRays && player.HasMovementIntent)
            {
                var o = player.transform.position + Vector3.up * 0.08f;
                var f = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
                var intent = Vector3.ProjectOnPlane(player.MovementIntent, Vector3.up);
                if (f.sqrMagnitude > 1e-8f)
                {
                    Debug.DrawRay(o, f.normalized * 1.25f, Color.cyan);
                }

                if (intent.sqrMagnitude > 1e-8f)
                {
                    Debug.DrawRay(o, intent.normalized * 1.25f, Color.yellow);
                }
            }
        }

        // ─── 158.2 L4 / 159.1：Resolver 帧解算（离散 FIRE + 连续表现快照；不含 Turn）───
        if (ProcessLocomotionResolve(player))
        {
            LocomotionDebug.LogTrace(
                player,
                LocomotionDebug.CatResolve,
                "SKIP_MOVE ProcessLocomotionResolve→ActionState (WalkStart/End 等离散 FIRE)",
                ref m_nextMoveTraceTime);
            return;
        }

        // 198.x — 167.1 VelocityDecay Tick 已删除；182.1 StopStrategy 在 Action 退出时唯一权威处理速度
        if (player.HasMovementIntent)
        {
            player.MoveByLocomotionIntent(1f, player.WantsRun);
        }
        else
        {
            player.StopMove();
        }

        player.ApplyMotor(MotorSolveContext.Locomotion);
        LogLocomotionMoveTrace(player, player.CurrentTurnInfo);
    }

    void LogLocomotionMoveTrace(Player player, in TurnInfo turnInfo)
    {
        if (!player.HasMovementIntent)
        {
            return;
        }

        var planar = player.PlanarVelocity;
        var intent = player.MovementIntent;
        LocomotionDebug.LogTrace(
            player,
            LocomotionDebug.CatMove,
            $"intent=({intent.x:F2},{intent.z:F2}) vel=({planar.x:F2},{planar.z:F2}) spd={planar.magnitude:F2} " +
            $"turn={turnInfo.IsTurning} type={turnInfo.Type} ∠={turnInfo.Angle:F1}° fwd=({player.transform.forward.x:F2},{player.transform.forward.z:F2})",
            ref m_nextMoveTraceTime);
    }

    /// <summary>
    /// 单帧 Locomotion Resolve：Detect → Resolver → 表现快照 → 可选 Discrete FIRE。
    /// 返回 true 表示已切 ActionState，本帧后续不再驱动 Locomotion 位移。
    /// </summary>
    private bool ProcessLocomotionResolve(Player player)
    {
        var profile = player.LocomotionProfile;
        var hasInput = player.HasMovementIntent;

        var requested = DetectRequestedState(player, hasInput, out var strafeDir);

        var intent = new LocomotionIntent(
            requestedState: requested,
            rawInput: player.MovementIntent,
            wantsRun: player.WantsRun,
            turnAngleDeg: ComputeTurnAngleDeg(player),
            strafeDirection: strafeDir,
            turnDirection: TurnDirection4.None);

        var ctx = new LocomotionContext(
            isGrounded: player.IsGrounded,
            isLockedOn: player.IsLockedOn,
            planarSpeed: player.PlanarVelocity.magnitude);

        var decision = LocomotionResolver.Resolve(in intent, in ctx, profile);

        LogDecisionIfChanged(player, requested, in intent, in decision);

        PublishLocomotionPresentation(player, profile, in intent, in decision);
        PublishContinuousLocomotionIfNeeded(player, in decision);

        var wasHadInput = m_lastHadInput;
        if (!wasHadInput && hasInput)
        {
            m_movePressStartTime = Time.time;
        }

        m_lastHadInput = hasInput;
        if (hasInput)
        {
            m_lastWantsRun = player.WantsRun;
        }

        if (decision.IsContinuousLocomotion || decision.DiscreteAction == null || decision.DowngradedFromLogicLayer)
        {
            return false;
        }

        var pressAngleOk = IsPressAngleSmallEnoughForStart(player);
        var shouldTrigger = decision.ResolvedState switch
        {
            LocomotionStateId.WalkEnd   => wasHadInput && !hasInput,
            LocomotionStateId.RunEnd    => wasHadInput && !hasInput,
            LocomotionStateId.WalkStart => !wasHadInput && hasInput && pressAngleOk,
            LocomotionStateId.RunStart  => !wasHadInput && hasInput && pressAngleOk,
            _ => false,
        };

        if (!shouldTrigger)
        {
            return false;
        }

        // 198.x — Tail Segment 退役；统一从 0 进入。短按手感由 RecoveryMoveLockSeconds 软屏蔽承担。
        player.ArmPendingAction(GameplayIntentKind.Move, decision.DiscreteAction, 0f);
        LocomotionDebug.Log(
            player,
            LocomotionDebug.CatResolve,
            $"FIRE state={decision.ResolvedState} action={decision.DiscreteAction.name} (edge-triggered)");

        player.InterruptTurnPresentation(decision.ResolvedState.ToString());
        player.States.Change<PlayerActionState>();
        return true;
    }

    static void PublishLocomotionPresentation(
        Player player,
        LocomotionProfile profile,
        in LocomotionIntent intent,
        in LocomotionDecision decision)
    {
        if (profile != null
            && decision.ResolvedState == LocomotionStateId.StrafeLocomotion
            && (decision.IsContinuousLocomotion || decision.ContinuousClip != null))
        {
            var binding = profile.GetBinding(
                LocomotionStateId.StrafeLocomotion,
                intent.StrafeDirection,
                TurnDirection4.None,
                intent.WantsRun);
            player.SetLocomotionPresentation(LocomotionPresentationSnapshot.FromBinding(
                LocomotionStateId.StrafeLocomotion, intent.StrafeDirection, in binding));
            return;
        }

        player.SetLocomotionPresentation(LocomotionPresentationSnapshot.FromDecision(in decision, in intent));
    }

    static void PublishContinuousLocomotionIfNeeded(Player player, in LocomotionDecision decision)
    {
        if (!decision.IsContinuousLocomotion || decision.LocomotionAction == null)
        {
            return;
        }

        var state = player.States?.Current as PlayerLocomotionState;
        if (state == null)
        {
            return;
        }

        if (state.m_lastContinuousLocomotionAction == decision.LocomotionAction)
        {
            return;
        }

        state.m_lastContinuousLocomotionAction = decision.LocomotionAction;
        player.RequestContinuousLocomotionPresentation(decision.LocomotionAction);
    }

    static void LogPressStartSkippedForTurn(Player player)
    {
        var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
        var threshold = tuning != null ? tuning.Turn90ThresholdDeg : 70f;
        var angle = ComputeTurnAngleDeg(player);
        LocomotionDebug.LogTurnPhase(
            player,
            "SKIP",
            $"reason=pressAngle abs={angle:F1}°≥{threshold:F1} (WalkStart deferred → TurnResolver)");
    }

    /// <summary>
    /// 162.1 Detect 优先级（转身不在 Detect —— 由 TurnResolver 表现叠加）：
    ///   1. release → WalkEnd / RunEnd（读 m_lastWantsRun）
    ///   2. press   → WalkStart / RunStart（读当帧 WantsRun）
    ///   3. LockedOn + hasInput → StrafeLocomotion + StrafeDir
    ///   4. 持续 hasInput + !WantsRun → Walk
    ///   5. 持续 hasInput +  WantsRun → Run
    ///   6. !hasInput     → Idle
    /// </summary>
    private LocomotionStateId DetectRequestedState(Player player, bool hasInput,
        out StrafeDirection8 strafeDir)
    {
        strafeDir = StrafeDirection8.None;

        if (m_inputEdgeSuppressFrames > 0)
        {
            m_inputEdgeSuppressFrames--;
            m_lastHadInput = hasInput;
            m_lastWantsRun = player.WantsRun;
            return hasInput ? LocomotionStateId.Walk : LocomotionStateId.Idle;
        }

        // 1) release 边沿 → WalkEnd / RunEnd
        if (m_lastHadInput && !hasInput)
        {
            return m_lastWantsRun
                ? LocomotionStateId.RunEnd
                : LocomotionStateId.WalkEnd;
        }

        // 2) press 边沿 → WalkStart / RunStart（164.1 L5：大角差交给 TurnResolver，不请求起步离散段）
        if (!m_lastHadInput && hasInput)
        {
            if (IsPressAngleSmallEnoughForStart(player))
            {
                return player.WantsRun
                    ? LocomotionStateId.RunStart
                    : LocomotionStateId.WalkStart;
            }

            LogPressStartSkippedForTurn(player);
        }

        // 3) LockedOn + 持续输入 → StrafeLocomotion + 8 向
        if (player.IsLockedOn && hasInput)
        {
            strafeDir = ComputeStrafeDirection8(player.MovementIntent, player.LogicForward);
            return LocomotionStateId.StrafeLocomotion;
        }

        // 4/5) 持续帧：Walk / Run 对称（159.3）；Profile 未注册时 Resolver 一级降级。
        if (!hasInput)
        {
            return LocomotionStateId.Idle;
        }

        return player.WantsRun ? LocomotionStateId.Run : LocomotionStateId.Walk;
    }

    /// <summary>
    /// 159.1 §5.3：MovementIntent 投影到 forward 局部空间，按 45° 切片 → 8 向。
    /// 调用方应在 LockedOn + hasInput 时调用。
    /// </summary>
    private static StrafeDirection8 ComputeStrafeDirection8(Vector3 worldInput, Vector3 forward)
    {
        var fwd = Vector3.ProjectOnPlane(forward, Vector3.up);
        var input = Vector3.ProjectOnPlane(worldInput, Vector3.up);
        if (fwd.sqrMagnitude < 1e-6f || input.sqrMagnitude < 1e-6f) return StrafeDirection8.None;

        // 转到角色局部空间：右手系，local.z=前向、local.x=右向
        var local = Quaternion.Inverse(Quaternion.LookRotation(fwd.normalized, Vector3.up)) * input.normalized;
        var angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg; // [-180, 180]
        var sector = Mathf.RoundToInt(angle / 45f);
        switch (sector)
        {
            case  0: return StrafeDirection8.Forward;
            case  1: return StrafeDirection8.ForwardRight;
            case  2: return StrafeDirection8.Right;
            case  3: return StrafeDirection8.BackwardRight;
            case  4: case -4: return StrafeDirection8.Backward;
            case -3: return StrafeDirection8.BackwardLeft;
            case -2: return StrafeDirection8.Left;
            case -1: return StrafeDirection8.ForwardLeft;
            default: return StrafeDirection8.Forward;
        }
    }

    /// <summary>164.1 L5：press 边沿起步须角色朝向与输入夹角小于 Turn90 阈值。</summary>
    private static bool IsPressAngleSmallEnoughForStart(Player player)
    {
        var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
        var threshold = tuning != null ? tuning.Turn90ThresholdDeg : 70f;
        return ComputeTurnAngleDeg(player) < threshold;
    }

    void LogTurnFireDedupIfNeeded(Player player, in TurnInfo turnInfo)
    {
        var turningNow = turnInfo.IsTurning;
        if (turningNow && !m_wasTurningLastFrame)
        {
            var dt = Time.time - m_lastTurnFireTime;
            if (m_lastTurnFireTime > 0f && dt < 0.05f)
            {
                LocomotionDebug.LogTurnPhase(
                    player,
                    "SKIP",
                    $"reason=dedup dt={dt:F3}s type={turnInfo.Type} ∠={turnInfo.Angle:F1}° (defensive only)");
            }

            m_lastTurnFireTime = Time.time;
        }

        m_wasTurningLastFrame = turningNow;
    }

    /// <summary>角色 forward 与 MovementIntent 在水平面的夹角（度，[0, 180]，无符号）。</summary>
    private static float ComputeTurnAngleDeg(Player player)
    {
        if (!player.HasMovementIntent) return 0f;
        var fwd = Vector3.ProjectOnPlane(player.LogicForward, Vector3.up);
        var intent = Vector3.ProjectOnPlane(player.MovementIntent, Vector3.up);
        if (fwd.sqrMagnitude < 1e-6f || intent.sqrMagnitude < 1e-6f) return 0f;
        return Vector3.Angle(fwd.normalized, intent.normalized);
    }

    private void LogDecisionIfChanged(
        Player player,
        LocomotionStateId requested,
        in LocomotionIntent intent,
        in LocomotionDecision decision)
    {
        if (!LocomotionDebug.IsEnabled(player)) return;
        if (decision.ResolvedState == m_lastResolvedState && decision.ControlOwnerHint == m_lastOwnerHint) return;

        var clipName = decision.ContinuousClip != null ? decision.ContinuousClip.name : "null";
        LocomotionDebug.Log(
            player,
            LocomotionDebug.CatResolve,
            $"requested={requested} resolved={decision.ResolvedState} " +
            $"strafeDir={intent.StrafeDirection} wantsRun={intent.WantsRun} " +
            $"discrete={(decision.DiscreteAction != null ? decision.DiscreteAction.name : "null")} " +
            $"clip={clipName} owner={decision.ControlOwnerHint} downgraded={decision.DowngradedFromLogicLayer}");
        m_lastResolvedState = decision.ResolvedState;
        m_lastOwnerHint = decision.ControlOwnerHint;
    }

    private static void RefreshLocomotionTags(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Grounded);
        EntityAbilitySystem.Update(player);
    }
}
