# 02_SystemInteraction — 系统交互图谱

> **生成时间**: 2026-06-08  
> **分析方法**: 代码调用链追踪 + 事件订阅分析

---

## 交互图谱 1: 输入→意图→动作 主链路

```
┌──────────────────────────────────────────────────────────────────┐
│                        INPUT LAYER                                │
│                                                                    │
│  Keyboard/Gamepad                                                  │
│     ↓                                                              │
│  InputReader (SO) ─── 连续量: MoveInput, IsAttackHeld, LookInput  │
│       │               SoA表: 17槽 pressedPulse/holdSeconds         │
│       │               MoveModifierBuffer: WASD 延迟缓冲            │
│       ▼                                                           │
│  InputSemanticResolver ── 每槽位状态机: Idle→Pressing→Tap/Combo/  │
│       │                    Charge/Directional 分流                │
│       ▼                                                           │
│  SkillEntryIntentFactory.ForEntryWithSemantic(...)                 │
│       │                                                           │
│       ▼                                                           │
└───────┬───────────────────────────────────────────────────────────┘
        │ GameplayIntent (struct, 零GC)
        ▼
┌──────────────────────────────────────────────────────────────────┐
│                      INTENT BUFFER                                │
│                                                                    │
│  GameplayIntentBuffer (环形缓冲, cap=16)                          │
│  · Enqueue(intent)                                                │
│  · TryPeek(out intent)                                            │
│  · Pop() / FlushExpired() / Clear()                               │
│       │                                                           │
└───────┬───────────────────────────────────────────────────────────┘
        │
        ▼  PlayerStateManager.OnPreLogicUpdate(dt) — 每帧仲裁
┌────────────────────────────────────────────────────────────────────────┐
│                      ARBITRATION LAYER                                  │
│                                                                         │
│  ┌─────────────────┐    ┌──────────────────┐    ┌───────────────────┐ │
│  │TransitionResolver│───→│SkillEntryService │───→│ActionInterrupt    │ │
│  │ 标签闸门         │    │ Route解析        │    │Resolver           │ │
│  │ · Forbidden tag  │    │ · CombatGraph    │    │ 本地窗口闸门      │ │
│  │ · Required tag   │    │ · Entry route    │    │ · Window category │ │
│  │ · 过期检查       │    │ · AbilityGate    │    │ · InterruptPriority│ │
│  └─────────────────┘    └──────────────────┘    └───────────────────┘ │
│                                  │                                     │
│                        ┌─────────▼─────────┐                          │
│                        │   CombatGraphRunner│                          │
│                        │   节点寻路+边条件  │  (仅 Combat 车道)        │
│                        └──────────────────┘                           │
│                                  │                                     │
│  ┌───────────────────────────────▼──────────────────────────────────┐ │
│  │                    IntentRouter.Route                            │ │
│  │  · Move   → Change<PlayerLocomotionState>                        │ │
│  │  · Jump   → Change<PlayerAirborneState>                          │ │
│  │  · Skill_Entry_* → Change<PlayerActionState> (ForceChange if in  │ │
│  │                    PlayerActionState already)                     │ │
│  └──────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────┘
        │
        ▼  State.Change → OnEnter
┌──────────────────────────────────────────────────────────────────┐
│                      EXECUTION LAYER                              │
│                                                                    │
│  PlayerActionState.OnEnter:                                        │
│    1. TryTakePendingAction → (kind, action)                       │
│    2. EnsureMotionPlumbing → MotionExecutor + MotorAdapter        │
│    3. MotionExecutor.Begin(profile, duration, dir, pos)           │
│    4. Tags.Add(PhaseStartup)                                      │
│    5. BeginActionMotorSession() + SuspendGravity()                │
│    6. RequestActionPresentation → Animation                       │
│       │                                                            │
│  PlayerActionState.OnLogicUpdate:                                  │
│    ┌──────────────────────────────────────────────────────┐      │
│    │ ActionTimelineRuntime.Tick:                           │      │
│    │  · EvaluatePhaseTags (nt → PhaseActive/Recovery)      │      │
│    │  · HitFrame 检测 → DamagePipeline                     │      │
│    │  · TeleportTrigger 检测 → TeleportTo                  │      │
│    │  · TimelineMarker 触发 → FX/Audio/Camera/TimeScale   │      │
│    └──────────────────────────────────────────────────────┘      │
│    ┌──────────────────────────────────────────────────────┐      │
│    │ SkillEntries.TickActive:                              │      │
│    │  · RouteRuntime.OnTick → Stage Transition 评估         │      │
│    │  · ChargeRoute 状态机推进                              │      │
│    │  · MultiStage Auto-advance                             │      │
│    └──────────────────────────────────────────────────────┘      │
│    ┌──────────────────────────────────────────────────────┐      │
│    │ MotionExecutor.Tick:  ChargeRoute通道                  │      │
│    │  · SetPlaybackContext (蓄力压速/循环窗/冻结)          │      │
│    │  · AxisCurves.SampleLocalDelta → DesiredVelocity       │      │
│    │  · AnimSpeed → SPEED_CTRL 委托                         │      │
│    └──────────────────────────────────────────────────────┘      │
│       │                                                            │
│  End Condition: nt≥1 && routeEnded                                │
│       │                                                            │
│  ExitToBaseline:                                                   │
│    · 空中战斗落地 + JumpLand → Change<PlayerActionState>          │
│    · 否则 → Change<PlayerLocomotionState>                          │
└──────────────────────────────────────────────────────────────────┘
```

---

## 交互图谱 2: Player → StateMachine → ActionSystem

```
┌───────────────────────────────────────────────────────────────┐
│                         Player                                │
│                                                               │
│  ┌──────────────────────────────────────────────────────────┐│
│  │                PlayerStateManager                        ││
│  │  (4支柱 FSM: Locomotion / Airborne / Action / Dead)      ││
│  │                                                          ││
│  │  ┌─────────────────┐  ┌──────────────────┐              ││
│  │  │ Locomotion      │  │ Airborne         │              ││
│  │  │ · MoveByLocomotion│ │ · MoveByAirborne │              ││
│  │  │ · TurnResolver  │  │ · JumpStart/     │              ││
│  │  │ · WASD → Motor  │  │   JumpLoop ctx   │              ││
│  │  │ · grounded tags  │  │ · airborne tags  │              ││
│  │  └────────┬────────┘  └────────┬─────────┘              ││
│  │           │                    │                         ││
│  │           │    IntentRouter    │                         ││
│  │           │  · Jump → Airborne │                         ││
│  │           │  · Move → Locomotion                        ││
│  │           │  · Skill_Entry_* → Action                   ││
│  │           │  · Grounded → Locomotion                    ││
│  │           │  · !Grounded → Airborne                     ││
│  │           │  · Dead → Dead                              ││
│  │           ▼                    ▼                         ││
│  │  ┌─────────────────┐  ┌──────────────────┐              ││
│  │  │ Action          │  │ Dead             │              ││
│  │  │ · PendingAction │  │ · ClearAll       │              ││
│  │  │ · MotionExecutor│  │ · StopMove       │              ││
│  │  │ · Timeline Tick │  │ · DisableInput   │              ││
│  │  │ · Skill Tick    │  │                  │              ││
│  │  └────────┬────────┘  └──────────────────┘              ││
│  │           │                                              ││
│  │  ┌────────▼────────────────────────────────────────┐    ││
│  │  │          SkillEntryService                      │    ││
│  │  │  · ActiveRoute (SkillRouteRuntime)              │    ││
│  │  │  · CombatGraphRunner                            │    ││
│  │  │  · CD管理 / ComboSession / GroupCooldown        │    ││
│  │  │  · RouteRuntimeFactory (Normal/Combo/Charge/    │    ││
│  │  │    MultiStage/Derivative)                        │    ││
│  │  └─────────────────────────────────────────────────┘    ││
│  └──────────────────────────────────────────────────────────┘│
└───────────────────────────────────────────────────────────────┘
```

### 状态迁移规则

```
                ┌──────────┐
       Jump     │Airborne  │  落地+JumpLand→Action
     ┌─────────→│          │──────────┐
     │          └──────────┘          │
     │             ↑ !Grounded        │
     │             │                  ▼
┌────┴─────┐  IntentRouter   ┌──────────────┐
│Locomotion│←────────────────│   Action     │
│          │  Skill_Entry_*  │              │
│          │────────────────→│              │
└──────────┘                 └──────┬───────┘
     ↑                              │
     │      动作结束 (nt≥1)         │
     └──────────────────────────────┘

     Dead (任意→Dead, 单向)
```

---

## 交互图谱 3: CombatGraph → Action → MotionProfile

```
CombatGraphAsset (SO)
  │
  ├── Nodes: CombatFlowGraphNode[]
  │     └── SkillRouteDefinition (NormalRoute / ComboRoute / ChargeRoute / MultiStage / Derivative)
  │           └── SkillStageDefinition[]
  │                 └── ActionDataSO (MainClip, Duration, Windows, MotionProfile, TimelineMarkers)
  │
  ├── Edges: CombatFlowGraphEdge[]
  │     └── CombatFlowConditionDefinition
  │           ├── MoveDirection8 条件
  │           ├── IsAirborne 条件
  │           ├── HitTally 条件
  │           ├── Resource 条件 (Stamina/MP)
  │           ├── Cooldown 条件
  │           └── Tag 条件 (State/Ability/Status)
  │
  └── EntryNode: 图入口
        │
        ▼
CombatGraphRunner (Runtime)
  │
  ├── Attach(asset): Compile + Validate
  ├── TryResolve(intent, context):
  │     · 在当前节点找匹配入边
  │     · 评估条件 (MoveDirection/IsAirborne/Hit/Resource/CD/Tag)
  │     · 返回 (RouteDefinition, StageIndex)
  │
  └── 通过 SkillEntryService 反馈
        │
        ▼
SkillStageDefinition
  └── ActionDataSO
        │
        ├── MainClip → PlayableGraph (Animation)
        ├── MotionProfile → MotionExecutor → PlayerKCCMotor (位移)
        ├── Windows → ActionTimelineRuntime (HitFrame/Tag/Teleport)
        └── TimelineMarkers → ActionTimelinePresentationPlayer (FX/Audio/Camera/TimeScale)
```

---

## 交互图谱 4: 位移控制权分配

```
                    ┌─────────────────────────────┐
                    │    Who drives movement?     │
                    └─────────────┬───────────────┘
                                  │
              ┌───────────────────┼───────────────────┐
              │                   │                   │
              ▼                   ▼                   ▼
    ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
    │ PlayerLocomotion │ │ PlayerAirborne  │ │ PlayerAction    │
    │ State            │ │ State           │ │ State           │
    │                  │ │                 │ │                 │
    │ MoveByLocomotion │ │ MoveByLocomotion│ │ MotionExecutor  │
    │ Intent:          │ │ Intent:         │ │ (if MotionProfile│
    │  accel→speedCap  │ │  airMoveMulti   │ │  != null)       │
    │ SetPlanarVelocity│ │ SetPlanarVelocity│ │                 │
    │                  │ │                 │ │ OR              │
    │ ApplyMotor       │ │ ApplyMotor      │ │                 │
    │ (Locomotion)     │ │ (Airborne)      │ │ (no Motion:     │
    │                  │ │                 │ │  无脚本位移)    │
    └────────┬────────┘ └────────┬────────┘ └────────┬────────┘
             │                   │                   │
             └───────────────────┼───────────────────┘
                                 │
                                 ▼
                    ┌─────────────────────┐
                    │   PlayerKCCMotor    │
                    │   (最终物理求解)     │
                    │                     │
                    │ · Gravity累积/悬浮  │
                    │ · CapsuleSweep      │
                    │ · Collide&Slide     │
                    │ · Ground Snap       │
                    │ · StepDown          │
                    │ · EdgeSlip          │
                    │ · Overlap Resolve   │
                    └─────────────────────┘
```

---

## 交互图谱 5: 事件总线旁路

```
┌─────────────────────────────────────────────────────────────┐
│                    Direct Call Pipeline                     │
│  (主链路: 帧序保证, 零异步)                                  │
│                                                             │
│  Input → Intent → Arbitration → State → Motor → Transform   │
│                                                             │
└──────────────┬──────────────────────────────────────────────┘
               │
               │ 事件发布 (PublishEvent)
               ▼
┌─────────────────────────────────────────────────────────────┐
│                  Event Bus (旁路解耦)                        │
│                                                             │
│  GlobalEventBus:                                            │
│  · PlayerActionPresentationRequest  → Animation Layer       │
│  · PlayerJumpEvent                  → VFX / Audio           │
│  · PlayerAttackStarted/Ended        → UI / Camera Shake     │
│  · PlayerLandedEvent               → VFX / Audio           │
│  · PlayerTeleportedEvent           → Visual Effects         │
│  · DamageResultEvent               → DamageTextSystem       │
│  · ResourceChangedEvent            → HUD Presenter          │
│  · PlayerJumpAirPhaseEvent         → Animation              │
│                                                             │
│  LocalEventBus (实体级):                                    │
│  · BuffApplied / BuffRemoved / BuffTick                     │
│  · StatChanged                                              │
│  · StateEntered / StateExited                               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 交互图谱 6: 数据流向 (SO→Runtime)

```
┌──────────────────────────────────────────────────────────────────┐
│                        DATA LAYER (4_Data)                        │
│                                                                   │
│  SkillEntryLoadoutSO                                              │
│  ├── Bindings[]: {Slot, SkillEntryDefinition}                     │
│  ├── CombatFlow: CombatGraphAsset                                 │
│  ├── AbilityMap: AbilityMapSO                                     │
│  ├── LocomotionGraphContext: LocomotionGraphContextBinding        │
│  └── ContextGroups[]: SkillContextGroupDefinition                │
│                                                                   │
│  SkillEntryDefinition                                             │
│  ├── PrimaryGroup (SkillGroupDefinition → Route[]/Dodge[]/Roll[]) │
│  ├── NormalRoute → SkillRouteDefinition → Stage[] → ActionDataSO │
│  ├── ComboRoute → ComboRouteDefinition → StageContainer[]         │
│  ├── ChargeRoute → ChargeRouteDefinition → HoldRelease, Tap      │
│  ├── MultiStageRoute                                               │
│  └── DerivativeRoute                                               │
│                                                                   │
│  ActionDataSO                                                     │
│  ├── MainClip → AnimationClip                                     │
│  ├── MotionProfile → MotionProfileSO → AxisCurves (XYZ)           │
│  ├── Windows[] → ActionWindow (标签/打断/Category)               │
│  └── TimelineMarkers[] → FX/Audio/Camera/TimeScale                 │
│                                                                   │
└───────────────────────────────┬───────────────────────────────────┘
                                │ Load/Rebuild
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│                      RUNTIME LAYER (3_Gameplay)                    │
│                                                                   │
│  SkillEntryService.Rebuild(loadout)                                │
│  ├── 遍历 Bindings → RegisterEntry → 创建 RouteRuntime            │
│  │   NormalRouteRuntime / ComboRouteRuntime / ChargeRouteRuntime  │
│  │   MultiStageRouteRuntime / DerivativeRouteRuntime              │
│  ├── AttachGraph(CombatFlow) → CombatGraphRunner                  │
│  └── 生成 HudHandles                                              │
│                                                                   │
│  PlayerActionState                                                │
│  ├── m_action: ActionDataSO (PendingAction)                       │
│  ├── m_motionExecutor: MotionExecutor (持有 MotionProfileSO)     │
│  ├── m_timelineState: ActionTimelinePlaybackState                 │
│  └── m_motorAdapter: PlayerMotorAdapter                          │
│                                                                   │
│  PlayerKCCMotor.ApplyMotorFromGameplayVelocity                    │
│  ├── MotionComposer.ComposeWorldVelocity (融合 Motion+Gravity)     │
│  └── KinematicMotorSolver.SolveDisplacementFromPivot              │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

---

## 关键系统间数据传递

| 传递方向 | 数据载体 | 传递方式 |
|---------|---------|---------|
| InputReader → PlayerController | `MoveInput`, `IsAttackHeld` 等属性 | 直接轮询 (Update) |
| InputReader → InputSemanticResolver | 槽位脉冲+hold秒数 | 方法调用 (OnPressEdge/OnHoldTick/OnReleaseEdge) |
| InputSemanticResolver → IntentBuffer | `GameplayIntent` (struct) | `Player.EnqueueGameplayIntent` |
| PlayerStateManager → SkillEntryService | `GameplayIntent` + `InputSnapshot` | `TryResolveForIntent` |
| SkillEntryService → PlayerActionState | `SkillRouteRuntime.Stage.Action` (ActionDataSO) | `ArmPendingAction` + `TryTakePendingAction` |
| MotionExecutor → PlayerKCCMotor | `DesiredVelocity` (Vector3) | `IMotorAdapter.SetDesiredVelocity` |
| ActionTimelineRuntime → DamagePipeline | `HitContext` | `DamagePipeline.Compute` 直接调用 |
| DamagePipeline → DamageTextSystem | `DamageResult` + 事件 | `GlobalEventBus.Publish` |
| ResourcePool → HUDPresenter | `ResourceChangedEvent` | `GlobalEventBus.Publish` |
| SkillEntryService → SkillSlotView | `IRouteRuntimeHandle` | HudHandles 列表轮询 |
| PlayerActionState → Animation | `PlayerActionPresentationRequestEvent` | `GlobalEventBus.Publish` |

---

## 控制权矩阵

| 控制域 | 谁拥有控制权 | 何时让出 |
|--------|-------------|---------|
| **水平位移** | MotionExecutor (Action中) / LocomotionState (默认) | Action结束时还给Locomotion |
| **垂直速度** | MotionExecutor (Curve Y) + 重力系统 | 落地/接地后重力接管 |
| **重力** | 可由 MotionProfile.SuspendGravity 挂起 | Action结束 ReleaseGravity |
| **接地判定** | PlayerKCCMotor.RefreshGroundedState | 每帧自刷新 |
| **动画播放** | PlayableGraph (AnimController + AnimSpeed委托) | [待验证具体控制] |
| **输入消费** | PlayerStateManager 仲裁阶段 | 意图被状态消费 |
| **相机** | GameModeManager → CameraController | 按游戏模式切换 |
