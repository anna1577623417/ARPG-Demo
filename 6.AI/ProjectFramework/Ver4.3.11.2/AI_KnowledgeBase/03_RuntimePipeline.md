# 03_RuntimePipeline — 运行时流程全追踪

> **生成时间**: 2026-06-08  
> **追踪方法**: 从按键到动作结束，逐帧代码路径分析

---

## 完整时序图: 玩家按下一个技能键

```
帧 N: 玩家按下 SkillEntrySlot.RM (鼠标右键)

═══════════════ INPUT PHASE (PlayerInputSystem 回调) ═══════════════
│
│ ① InputAction.started 回调
│    → InputReader.OnSkillEntry01 (performed)
│    → _slotPressedPulse[(int)slot] = true
│    → _slotHeld[(int)slot] = true
│    → _slotHeldStartTime[(int)slot] = Time.time
│    → _moveBuffer.Record(Time.time, MoveInput)
│
│ ② InputAction.canceled 回调 (松手时)
│    → InputReader.OnSkillEntry01 (canceled)
│    → _slotHeld[(int)slot] = false
│    → ConsumeSkillEntryPressed 写入 holdSeconds
│
═══════════════ CONTROLLER PHASE (PlayerController.Update) ═════════
│
│ ③ PlayerController.Update()  [-50 ExecutionOrder]
│    ├── ConsumeDiscreteIntents():
│    │     TryDispatchEntry(SkillEntrySlot.RM)
│    │       → inputReader.ConsumeSkillEntryPressed(slot, out holdSeconds)
│    │       → 获取 moveBuf = MoveModifierBuffer.GetBufferedMove(now)
│    │       → inputSemantic.OnDiscretePulse(slot, now, holdSeconds, moveBuf, valid)
│    │
│    ├── 连续移动:
│    │     rawInput = inputReader.MoveInput
│    │     TryEnqueueMoveInterruptIntent (Action中放行Locomotion)
│    │     DetectDoubleTapCardinalStickyRun (WASD双击→粘性跑步)
│    │     worldDirection = ResolveWorldDirection(rawInput)
│    │     wantsRun = ResolveRunIntent(rawInput, releaseSq)
│    │     player.SetMovementIntent(worldDirection, wantsRun)
│    │
│    └── PrimaryAttackPressTracker / SecondaryInteractPressTracker.Tick
│
│ ④ InputSemanticResolver.OnDiscretePulse(slot, now, holdSeconds, ...)
│    ├── cfg.EnableDirectional && moveBufferValid? 
│    │     → EnqueueSemantic(slot, InputSemanticType.Directional)
│    ├── 否则: EvaluateComboTapOrAdvance:
│    │     gap = SkillEntries.GetComboGapSinceLastSegmentEnd
│    │     · gap < 0 (无Session) → EnqueueFirstTap (ComboIndex=0)
│    │     · gap > ComboWindow → EnqueueNewChain (ComboIndex=0)
│    │     · gap in window + edge timing ok → EnqueueAdvance (ComboIndex++)
│    │     · edge min gap not met → Suppress
│    ├── EnqueueSemantic → SkillEntryIntentFactory.ForEntryWithSemantic
│    └── player.EnqueueGameplayIntent(intent) → IntentBuffer
│
═══════════════ ARBITRATION PHASE (PlayerStateManager.OnPreLogicUpdate) ═══
│
│ ⑤ PlayerStateManager.OnPreLogicUpdate(dt)
│    ├── SkillEntries.TickCooldowns(dt)
│    │     · 所有 RouteRuntime.CdRemainingSeconds -= dt
│    │     · 所有 ComboRouteRuntime Session CD
│    │     · InputSemanticResolver.OnHoldTick(slot, now)
│    │         → hold ≥ TapThreshold → Enqueue Charge intent
│    │
│    ├── IntentBuffer.FlushExpired(Time.time)
│    │
│    └── for (i = 0; i < maxIntentConsumptionsPerFrame; i++):
│          if (!IntentBuffer.TryPeek(out intent)) break
│
│          [Step A] TransitionResolver.CanOfferIntent(ctx, intent, out reason)
│            · ctx.Time >= intent.ExpireTime → "expired"
│            · (ctx.CurrentTags & intent.ForbiddenTags) != 0 → "forbidden"
│            · (ctx.CurrentTags & intent.RequiredAllTags) != intent.RequiredAllTags → "missing required"
│            · (ctx.CurrentAbilityTags & intent.RequiredAllAbilityTags) failed → "missing ability"
│            · Pass → continue
│
│          [Step B] Skill Route 解析 (仅 Combat 车道)
│            ActionIntentRouting.ResolveLane(intent) == Combat:
│              snap = BuildInputSnapshot(intent)
│              resolvedRoute = SkillEntries.TryResolveForIntent(intent, snap, now, out discard)
│                ├── IntentSemantic=Tap → TryRouteForTap
│                │     ├── PrimaryGroup 过滤方向 → DirectionalRoute
│                │     ├── NormalRoute → CanCast? → OnEnter
│                │     └── RouteRuntimeFactory.Create
│                ├── IntentSemantic=Combo → TryRouteForCombo
│                │     ├── 检查 ComboSession (active? index match?)
│                │     ├── TryComboLinkWindow (边窗口门控)
│                │     └── ComboRouteRuntime → 推进到对应 SubRoute
│                ├── IntentSemantic=Charge → TryRouteForCharge
│                │     └── ChargeRouteRuntime.CanCast
│                ├── IntentSemantic=Directional → TryGroupDirectionalRoute
│                │     └── PrimaryGroup.Find(DirectionalType(directionAxis))
│                └── CombatGraphRunner.TryResolve (若Graph装配)
│                      · 从当前节点找匹配入边
│                      · 评估 CombatFlowConditionDefinition
│                      · LastIntentResolvedViaGraph = true
│                → firstStage = ResolveStartStage(route, now)
│                → ArmPendingAction(kind, firstStage.Action)
│                → return route
│
│          [Step C] 当前状态本地闸门
│            Current.TryConsumeGameplayIntent(player, ctx, intent)
│
│            LocomotionState:
│              · Move → consume (返回true，保持Locomotion)
│              · 其他 → IsRoutable → category check → CanInterrupt
│              · → IntentRouter.Route → Change<ActionState/Airborne>
│
│            ActionState:
│              · IsRoutable → PeekActionDataForRouting → ResolveIncomingCategory
│              · ActionInterruptResolver.CanInterrupt:
│                  ├── incomingPriority > current.InterruptStability → HARD BREAK ✓
│                  └── IsCategoryAllowedAtWindow(currentAction, nt, incomingCategory)
│                      → 遍历 currentAction.Windows:
│                          t in [w.Start, w.End] &&
│                          (w.InterruptibleByCategories & incomingCategory) != 0 &&
│                          priority >= w.MinIncomingPriority → ✓
│              · DualGate: GraphEnabled + Full参与 + !LastIntentResolvedViaGraph → BLOCK
│              · NotifyRouteExited(wasInterrupted: true) ← 打断旧Route
│              · IntentRouter.Route → ForceChange<ActionState>
│
│            AirborneState:
│              · Move → consume
│              · 其他 → category check (ascending/descending 分开)
│              · → IntentRouter.Route → Change<ActionState>
│
│          [Step D] 提交 ActiveRoute
│            if resolvedRoute != null:
│              SkillEntries.NotifyRouteEntered(resolvedRoute, slot)
│
│          [Step E] Pop
│            IntentBuffer.Pop()
│
═══════════════ STATE TRANSITION (PlayerStateManager.Change) ══════
│
│ ⑥ Current.OnExit → NewState.OnEnter
│
│    若从 Locomotion → Action:
│      LocomotionState.OnExit:
│        · TurnResolver.ClearLock()
│        · ClearTurnInfo
│        · ClearGraphContextAction
│
│    若从 Action → Action (打断):
│      ActionState.OnExit(old):
│        · MotionExecutor.End()
│        · ReleaseGravity()
│        · EndActionMotorSession()
│        · NotifyRouteExited(wasInterrupted: true)
│        · ClearPhaseStartup tag
│        · ForceEndAttackIfActive
│
│    ActionState.OnEnter(new):
│      ① TryTakePendingAction(out kind, out action)
│         → 若空则立即退回 Locomotion
│      ② EnsureMotionPlumbing:
│         · m_motorAdapter = new PlayerMotorAdapter(player)
│         · m_statsProvider = new PlayerMotionStatsProvider(player)
│         · m_motionExecutor = new MotionExecutor(motorAdapter, animSpeed, stats, player)
│      ③ m_baseDuration = MotionDurationResolver.Resolve(action, statsProvider)
│      ④ m_useMotionProfile = (action.MotionProfile != null)
│      ⑤ m_burstFaceDir = ResolveMotionFacingDirection(player, action.MotionProfile)
│      ⑥ Tags.Add(PhaseStartup)
│      ⑦ if m_useMotionProfile:
│         · BeginActionMotorSession()
│         · ShouldSuspendMotorGravity → SuspendGravity()
│         · MotionExecutor.Begin(profile, duration, dir, pos, baseAnimSpeed)
│      ⑧ BeginAttackWithManualCompletion()
│      ⑨ RequestActionPresentation(kind, action) → Event → Animation
│
═══════════════ LOGIC UPDATE (帧 N+1, N+2, ...) ═══════════════
│
│ ⑦ PlayerActionState.OnLogicUpdate(player)
│    ├── dt = Time.deltaTime
│    ├── Charge Freeze检查: ActiveRoute is ChargeRouteRuntime && FreezeNormalizedAdvance
│    │     → m_elapsed 暂停
│    ├── nt = Clamp01(m_elapsed / m_baseDuration)
│    │
│    ├── [Phase Tags] EvaluatePhaseTags(nt, ref GameplayTags)
│    │     · 清理 PhaseStartup/PhaseActive/PhaseRecovery
│    │     · 按 Windows 写入当前切片标签
│    │
│    ├── [Timeline] ActionTimelineRuntime.Tick(player, action, prevNt, nt, faceDir, state)
│    │     · Window 边界检测 (prevNt→nt 跨越 Start/End)
│    │     · HitFrame: 窗口标记 Invulnerable/ComboInput → 触发事件
│    │     · TeleportTrigger: nt 跨过 TriggerTime → player.TeleportTo
│    │     · TimelineMarkers: FX/Audio/Camera/TimeScale 效果触发
│    │       → ActionTimelinePresentationPlayer
│    │
│    ├── [Skill] SkillEntries.TickActive(input, dt)
│    │     · ActiveRoute.OnTick(ctx)
│    │       ├── Stage.Tick(dt) → Stage.NormalizedTime, Stage.Completed
│    │       ├── EvaluateTransitions(ctx)
│    │       │     · 检查 Stage.Transitions
│    │       │     · OnInput / OnHit / OnRelease / OnTimer trigger
│    │       │     · ConditionEvaluator.EvaluateAll
│    │       │     → AdvanceTo(transition) → next Stage / next Route
│    │       ├── ChargeRouteRuntime.OnTick (蓄力状态机)
│    │       ├── ComboRouteRuntime.OnTick (段位推进)
│    │       ├── MultiStageRouteRuntime.OnTick (Auto-advance)
│    │       └── Stage结束→下一个Stage (SwapToStageAction)
│    │
│    ├── [Motion] if m_useMotionProfile:
│    │     · ChargeRoute.Playback → SetPlaybackContext (压速/循环/冻结)
│    │     · MotionExecutor.Tick(dt, 1f, transform.position)
│    │       ├── dtScale = FreezeNormalizedAdvance ? 0 : dt * timeScale
│    │       ├── _elapsed += dtScale
│    │       ├── ApplyLoopWindowIfNeeded (Charging 循环窗)
│    │       ├── TickAxisCurves(prevT, currT, dt):
│    │       │     · GroundTargeted: 单独Y路 (LandingCurve)
│    │       │     · Normal: AxisCurves.SampleLocalDelta → localDelta
│    │       │     · LocalDeltaToWorld → worldDelta
│    │       │     · desiredVelocity = worldDelta / dt
│    │       │     · SetDesiredVelocity(desiredVelocity)
│    │       │     · SetMotionComposeContext(yAxisConfig)
│    │       │
│    │       └── TickAnimSpeed(motionT):
│    │             · profile.SampleAnimSpeed(motionT) * baseAnimSpeed
│    │             · animSpeed.SetSpeed(finalSpeed) → Animator.speed
│    │     · m_motorAdapter.ApplyToPlayer()
│    │       → player.ApplyMotorFromGameplayVelocity(desiredVelocity, ctx, yAxis, useComposer=true)
│    │     · m_motionExecutor.SyncPostMotorPosition(transform.position)
│    │
│    ├── m_prevNormalizedTime = nt
│    │
│    └── [End Condition]
│         routeEnded = ActiveRoute==null || !ActiveRoute.IsActive
│         actionEnded = nt >= 0.9999f
│         if actionEnded && routeEnded:
│           → ExitToBaseline(player)
│
═══════════════ MOTOR PHASE (位移物理求解) ═════════════════
│
│ ⑧ PlayerKCCMotor.ApplyMotorFromGameplayVelocity(gameplayVel, ctx, yAxis, useComposer)
│    ├── ApplySimpleGravity(): _verticalSpeed -= gravity * dt
│    ├── ClampTerminalVelocityVertical()
│    │
│    ├── if useMotionComposer:
│    │     · MotionComposer.ComposeWorldVelocity(motionContrib, gravityContrib, gameplayVel, yAxis)
│    │       → 融合曲线位移 + 重力贡献
│    │     · MotionGroundConstraint.ApplyClamp(velocity, yAxis, wasGrounded)
│    │
│    ├── velocity * dt → displacement
│    │
│    ├── KinematicMotorSolver.SolveDisplacementFromPivot:
│    │     · CapsuleSweep 穿透检测
│    │     · Collide&Slide (沿面滑动)
│    │     · 9道闸门: 墙面消毒/下半球/半身墙/凹角/凸角/接地下沉/EdgeSlip/中心射线/脱地
│    │     · StepDown (下台阶吸附)
│    │     → solvedDelta
│    │
│    ├── transform.position += solvedDelta
│    ├── ResolveMotorOverlapsIfNeeded (去穿插)
│    ├── RefreshGroundedState(ctx):
│    │     · SphereCast 探针 (可选二次稳定下探/中心射线兜底)
│    │     · 坡度检测 (MaxSlopeAngle)
│    │     · Hard Snap (吸附到地面)
│    │     · Stair Band 楼梯处理
│    │     · ActionAirborneLock 自动释放
│    │     · Edge Tolerance 边缘落地放行
│    │     · 更新 _isGrounded / _lastGroundNormal
│    │
│    └── ApplyAirborneEdgeSlipIfStuck (反滞空卡死)
│
═══════════════ PRESENTATION PHASE (动画/表现更新) ═════════════
│
│ ⑨ Animation (PlayableGraph + Animator Controller)
│    · PlayerActionPresentationRequestEvent 触发
│      → EntityAnimController / PlayerAnimController
│      → Play ActionDataSO.MainClip with CrossfadeTime
│    · AnimSpeed 由 MotionExecutor.SpeedControl 委托驱动
│      → Animator.speed = finalSpeed
│
│ ⑩ VFX / Audio / CameraShake
│    · ActionTimelinePresentationPlayer
│      → 在 TimelineMarker 触发点播放 FX/Audio
│      → ActionCameraController 响应 CameraShake
│
═══════════════ EXIT PHASE (动作结束) ═════════════════
│
│ ⑪ ActionState.OnExit(player)
│    · MotionExecutor.End() → DesiredVelocity=0, AnimSpeed=1
│    · ReleaseGravity()
│    · EndActionMotorSession()
│    · NotifyRouteExited(wasInterrupted: false) → CD启动
│    · Remove PhaseStartup tag
│    · ForceEndAttackIfActive
│    · TimelineState.OnActionExit → Camera/TimeScale 恢复
│
│ ⑫ ExitToBaseline(player)
│    · 空中战斗落地 + JumpLand ≠ null:
│        SetGraphContextAction(JumpLand)
│        ArmPendingAction(Jump, JumpLand)
│        Change<ActionState> (播放落地动作)
│    · 否则:
│        Change<LocomotionState> (回到Idle/Walk/Run)
│
═══════════════ RETURN TO LOCOMOTION ═════════════════════
│
│ ⑬ LocomotionState.OnEnter(player)
│    · RefreshLocomotionTags: Clear → Add(Grounded) → EntityAbilitySystem.Update
│    · TurnResolver.ClearLock
│    · ClearTurnInfo / ClearGraphContextAction
│
│ ⑭ LocomotionState.OnLogicUpdate(player)
│    · IsDead? → Change<DeadState>
│    · !IsGrounded? → Change<AirborneState>
│    · TurnResolver.Tick (转身插值)
│    · HasMovementIntent? → MoveByLocomotionIntent → SetPlanarVelocity
│    · !HasMovementIntent? → StopMove
│    · ApplyMotor(MotorSolveContext.Locomotion)
│
```

---

## 数据流总览

```
InputAction Callbacks
    │
    ▼
InputReader (SoA表: 17槽 pressed/hold/time)
    │
    ├─→ PlayerController.Update (连续移动采样)
    │     └─→ Player.SetMovementIntent(worldDir, wantsRun)
    │
    └─→ ConsumeDiscreteIntents → InputSemanticResolver
          └─→ EnqueueSemantic → IntentBuffer
                │
                ▼
PlayerStateManager.OnPreLogicUpdate (仲裁)
    │
    ├─→ [验证] TransitionResolver.CanOfferIntent (Tag gates)
    ├─→ [解析] SkillEntryService.TryResolveForIntent (Route + Graph)
    ├─→ [闸门] Current.TryConsumeGameplayIntent (Interrupt + Route)
    │     └─→ IntentRouter.Route → ForceChange<ActionState>
    │
    ▼
PlayerActionState.OnEnter
    │
    ├─→ [位移初始化] MotionExecutor.Begin(profile, dur, dir, pos)
    ├─→ [标签初始化] Tags.Add(PhaseStartup)
    ├─→ [马达会话] BeginActionMotorSession + SuspendGravity
    └─→ [表现请求] RequestActionPresentation → Animation
          │
          ▼
PlayerActionState.OnLogicUpdate (每帧)
    │
    ├─→ [标签更新] EvaluatePhaseTags(nt, GameplayTags)
    ├─→ [时间轴] ActionTimelineRuntime.Tick (HitFrame/Teleport/Markers)
    ├─→ [技能推进] SkillEntries.TickActive (Stage Transition)
    ├─→ [位移更新] MotionExecutor.Tick → DesiredVelocity
    │     └─→ PlayerKCCMotor.ApplyMotorFromGameplayVelocity
    │           ├─→ MotionComposer (融合)
    │           └─→ CapsuleSweep + GroundSnap
    └─→ [结束判定] nt≥1 && routeEnded → ExitToBaseline
```

---

## 控制流: 状态机驱动

```
Update Order:
  [-50] PlayerController.Update      ← 输入采样+入队优先
  [ 0 ]  PlayerStateManager.Update    ← 仲裁+状态迁移
  [ 0 ]  EntityStateManager.LogicUpdate ← 当前状态推进
  [ 0 ]  PlayerKCCMotor (LateUpdate via ApplyMotor) ← 物理求解
```

### 关键控制流分支

```
IsDead?
  ├─ Yes → Change<DeadState> (终态，不再响应)
  └─ No → continue

IsGrounded?
  ├─ Yes → LocomotionState (Idle/Walk/Run/Turn)
  └─ No → AirborneState (Jump/Fall)

Has PendingAction?
  ├─ Yes → ActionState 播 Action
  └─ No → 退回 Locomotion

ActiveRoute.IsActive?
  ├─ Yes → TickActive (推进 Stage)
  └─ No → 等待 action ended → Exit
```

---

## Motion 流: 位移管道

```
[Locomotion 模式]
  Player.MovementIntent → MoveByLocomotionIntent
    ├── accel = moveAcceleration / moveDeceleration
    ├── targetSpeed = speedCap * inputMag * externalMultiplier
    ├── newSpeed = MoveTowards(current, target, accel*dt)
    └── SetPlanarVelocity(dir * newSpeed)
  → ApplyMotor(MotorSolveContext.Locomotion)
  → SimpleGravity + CapsuleSweep + GroundSnap

[Action/MotionProfile 模式]
  MotionExecutor.Tick
    ├── AxisCurves.SampleLocalDelta(prevT, currT) → localDelta (XYZ 米)
    ├── LocalDeltaToWorld → worldDelta
    ├── desiredVelocity = worldDelta / dt
    └── SetDesiredVelocity(desiredVelocity)
  → ApplyMotorFromGameplayVelocity(desiredVelocity, yAxis, useComposer=true)
  → MotionComposer (融合 Motion+重力)
  → CapsuleSweep + GroundSnap (MotionProfile路径也走StepDown)

[Airborne 模式]
  MoveByLocomotionIntent(airMoveMultiplier, wantsRun)
  → SetPlanarVelocity + SimpleGravity
  → ApplyMotor(MotorSolveContext.Airborne)
```

---

## Animation 流

```
PlayerActionState.RequestActionPresentation(kind, action)
  → GlobalEventBus.Publish(PlayerActionPresentationRequestEvent)
  → PlayerAnimController / EntityAnimController 订阅
  → PlayableGraph: CrossFade to action.MainClip with action.CrossfadeTime
  → Animator.speed 由 MotionExecutor.AnimSpeedControl 驱动:
      baseAnimSpeed * profile.SpeedOverTime(motionT)
      或 ChargeRoute.Playback.AnimatorSpeedOverride
  → 动画结束: PlayableGraph 自然完成
  → Action结束 → Animator 过渡回 Locomotion BlendTree
```

---

## 落地特殊流程 (JumpLand)

```
PlayerAirborneState.OnLogicUpdate:
  if player.IsGrounded:
    TryExitToLandOrLocomotion(player)
      ├── PublishEvent(PlayerLandedEvent)
      │
      ├── ctx.JumpLand != null:
      │     SetGraphContextAction(JumpLand, "jump-land")
      │     ArmPendingAction(Jump, JumpLand)
      │     Change<PlayerActionState>()  ← 播落地后摇动作
      │
      └── ctx.JumpLand == null:
            ClearGraphContextAction("land-no-jump-land-action")
            Change<PlayerLocomotionState>()  ← 直接回到移动

PlayerActionState.ExitToBaseline:
  if startedWhileAirborne && IsGrounded && JumpLand != null && action is Combat:
    SetGraphContextAction(JumpLand, "jump-land-after-air-combat")
    ArmPendingAction(Jump, JumpLand)
    Change<PlayerActionState>()  ← 空中战斗落地→播落地动作
```

---

## 帧内执行顺序总结

```
同一帧内:
  1. InputReader (InputAction回调, 先于Update)
  2. PlayerController.Update (ExecutionOrder=-50)
     - 消费离散脉冲 → IntentBuffer
     - 采集连续移动 → SetMovementIntent
  3. PlayerStateManager.OnPreLogicUpdate
     - CD减秒
     - 仲裁: 标签→Route→Interrupt→Route
     - 状态迁移 (OnEnter)
  4. PlayerStateManager.Update → EntityStateManager.LogicUpdate
     - 当前状态的 OnLogicUpdate
     - ActionState: Time线→SkillTick→MotionTick
     - LocomotionState: TurnTick→MoveByLocomotion→ApplyMotor
  5. PlayerKCCMotor (LateUpdate or state-driven ApplyMotor)
     - 物理求解: 重力+碰撞+接地
  6. Animation (PlayableGraph, 自动同步)
     - 响应 Animator.speed 变化
```
