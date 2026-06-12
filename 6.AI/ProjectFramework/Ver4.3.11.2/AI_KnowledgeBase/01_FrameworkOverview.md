# 01_FrameworkOverview — 框架全局概览

> **生成时间**: 2026-06-08  
> **分析依据**: 386 C# 文件全量扫描 + 核心代码精读

---

## 系统矩阵

| # | 系统名称 | 层级 | 职责 | 输入 | 输出 |
|---|---------|------|------|------|------|
| 1 | **Input** | 2_Framework | 硬件信号→连续量+离散脉冲 | InputAction 回调 | `InputReader` 属性 + SoA 槽位表 |
| 2 | **InputSemantic** | 2_Framework | Raw Input→语义意图分流 (Tap/Combo/Charge/Directional) | `InputReader` 脉冲 + 槽位配置 | 带语义的 `GameplayIntent` |
| 3 | **PlayerController** | 3_Gameplay | 连续移动采样+离散意图入队 | `InputReader` + `IGameModeMovementContext` | `Player.SetMovementIntent` + `IntentBuffer.Enqueue` |
| 4 | **GameplayIntentBuffer** | 3_Gameplay | 环形缓冲 (struct, cap=16, 零GC) | `GameplayIntent` 入队 | `TryPeek` / `Pop` / `FlushExpired` |
| 5 | **PlayerStateManager** | 3_Gameplay | 四支柱FSM仲裁核心 | `IntentBuffer` + `FrameContext` | 状态迁移 + Route 解析触发 |
| 6 | **TransitionResolver** | 3_Gameplay | 全局标签闸门 (Forbidden/Required Tag) | `FrameContext` + `GameplayIntent` | `CanOfferIntent` bool |
| 7 | **SkillEntryService** | 2_Framework | 技能入口总线 (Route管理/CombatGraph/CD/Combo) | `GameplayIntent` + `InputSnapshot` | `RouteRuntime` 解析结果 |
| 8 | **SkillRouteRuntime** | 2_Framework | Route运行时 (CD/Cost/Stage推进/Transition) | `SkillRouteContext` | Stage 切换 / Route 生命周期 |
| 9 | **CombatGraphRunner** | 2_Framework | CombatFlow图运行时 (节点寻路/边条件) | `CombatGraphAsset` + 帧上下文 | 图解析结果 + 游标推进 |
| 10 | **ActionInterruptResolver** | 3_Gameplay | Action窗口内打断仲裁 | 当前 `ActionDataSO` + `GameplayIntent` + 归一时间 | `CanInterrupt` bool |
| 11 | **IntentRouter** | 3_Gameplay | 意图→支柱状态路由 | `GameplayIntent` | `StateManager.Change<T>` |
| 12 | **PlayerActionState** | 3_Gameplay | Action支柱 (动作播放+Motion推进+Route推进) | `PendingAction` + `SkillEntries.ActiveRoute` | 标签写入 + 状态出口 |
| 13 | **PlayerLocomotionState** | 3_Gameplay | Locomotion支柱 (Idle/Walk/Run) | `Player.MovementIntent` + `TurnResolver` | `MoveByLocomotionIntent` + `ApplyMotor` |
| 14 | **PlayerAirborneState** | 3_Gameplay | Airborne支柱 (跳跃/下落) | `Player` 物理状态 + Graph 上下文 | `MoveByLocomotionIntent` + `ApplyMotor` + 落地过渡 |
| 15 | **PlayerDeadState** | 3_Gameplay | Dead支柱 (终态) | 死亡触发 | 禁用输入/停止移动 |
| 16 | **ActionDataSO** | 4_Data | 动作数据资产 (Clip/Windows/Motion/Category) | SO 配置 | `ActionTimelineRuntime.Tick` 消费 |
| 17 | **MotionProfileSO** | 4_Data | 运动曲线资产 (XYZ三轴曲线/重力/地面约束) | SO 配置 | `MotionExecutor.Tick` 消费 |
| 18 | **MotionExecutor** | 3_Gameplay | 位移执行器 (曲线采样→DesiredVelocity) | `MotionProfileSO` + 时间推进 | `DesiredVelocity` → `IMotorAdapter` |
| 19 | **PlayerKCCMotor** | 3_Gameplay | KCC物理马达 (CapsuleSweep+地面探针+StepDown) | `DesiredVelocity` + `MotorSolveContext` | Transform 位移 + 接地状态 |
| 20 | **Animation** | 5_Presentation | 动画播放 (PlayableGraph + AnimController) | `PlayerActionPresentationRequestEvent` | Clip 播放 + AnimSpeed 控制 |
| 21 | **Camera** | 2_Framework | 相机控制 (Action/FP/MOBA 多模式) | `GameModeManager` + 状态事件 | 相机位置/旋转 |
| 22 | **UI** | 2_Framework | UI框架 (ScreenStack/ModalStack/HUD/Presenter) | `UIRoot` + 事件订阅 | 屏幕切换 + HUD 更新 |
| 23 | **DamagePipeline** | 3_Gameplay | 伤害四阶段管线 (Base→Defense→Crit→Clamp) | `CombatContext` + `HitContext` | `DamageResult` |
| 24 | **BuffStack** | 3_Gameplay | Buff堆叠管理 (Tick/叠加/过期) | `BuffDefinitionSO` + 事件 | Buff 实例生命周期 |
| 25 | **StatSet** | 3_Gameplay | 属性计算 (Base + Modifier 三阶段) | `EntityStatsSO` | 运行时属性值 |
| 26 | **ResourcePool** | 3_Gameplay | 资源池 (HP/MP/Stamina, maxProvider 委托) | `RegisterSlot` / `Drain` | 资源变更事件 |
| 27 | **Editor/Authoring** | Editor | 资产创作工具链 (Motion提取/Action编辑/CombatFlow图) | SO 资产 | 编辑器修改 + 批处理 |
| 28 | **CombatFlowGraphWindow** | Editor | CombatFlow 可视化图编辑器 | `CombatGraphAsset` | 节点/边编辑 |

---

## 核心系统详解

### 1. Input 系统 (2_Framework/Input/)

```
物理键 (.inputactions)
  → PlayerInputSystem.IGamePlayActions 回调
  → InputReader (ScriptableObject) 写入:
      ① 连续量属性: MoveInput, LookInput, IsAttackHeld, IsJumpHeld
      ② 17 个 SkillEntrySlot SoA 表: pressedPulse[slot], held[slot], heldStartTime[slot]
      ③ Jump/Party 独立脉冲
  → InputModifierBuffer: WASD 延迟缓冲 (用于方向感知释放)
  → InputSemanticResolver: 每槽位独立状态机 (Idle→Pressing→Tap/Combo/Charge/Directional)
```

**核心类**: `InputReader`, `InputSemanticResolver`, `InputModifierBuffer`, `PlayerInputSystem`

**设计要点**:
- 只翻译不决策：Tap/Hold/Combo/Charge 分流在 `InputSemanticResolver` 完成
- 零旧符号：仅认识 `SkillEntrySlot`；不存在 `SkillSlotType`
- 每槽位独立语义状态机
- `MoveModifierBuffer` 提供 WASD 方向感知延迟窗口

### 2. Action 系统 (3_Gameplay/Combat/ActionSystem/)

```
输入意图 → IntentBuffer (环形缓冲, struct, cap=16)
  → PlayerStateManager.OnPreLogicUpdate:
      ① SkillEntries.TickCooldowns(dt)
      ② IntentBuffer.FlushExpired(now)
      ③ TryPeek → BuildFrameContext → TransitionResolver.CanOfferIntent (标签闸门)
      ④ SkillEntries.TryResolveForIntent (Combat车道走Graph/Entry解析)
      ⑤ Current.TryConsumeGameplayIntent (IntentRouter.Route + ActionInterruptResolver.CanInterrupt)
      ⑥ SkillEntries.NotifyRouteEntered
      ⑦ IntentBuffer.Pop()
  → PlayerActionState.OnEnter:
      消费 PendingAction → MotionExecutor.Begin → 标签写入 → 表现请求
  → PlayerActionState.OnLogicUpdate:
      ActionTimelineRuntime.Tick (窗口标签/HitFrame/Teleport)
      SkillEntries.TickActive (Stage Transition)
      MotionExecutor.Tick (曲线位移)
  → 结束: nt≥1 + routeEnded → ExitToBaseline (Locomotion 或 JumpLand→Action)
```

**核心类**: `GameplayIntent`, `GameplayIntentBuffer`, `TransitionResolver`, `ActionInterruptResolver`, `IntentRouter`, `ActionTimelineRuntime`, `GraphDualGatePolicy`

**ActionIntentCategory (A轴)**:
- `Combat` → 走 SkillEntryService → CombatGraph
- `Locomotion` → 走全局仲裁 (Move/Jump)
- `Reaction` → 受击响应
- `Interaction` → 交互

**GraphParticipation (C轴)**:
- `Auto` → 按 IntentCategory 派生
- `None` → 不参与图
- `SourceOnly` → 仅作图源节点
- `Full` → 完整图参与 (需双闸门: Graph Edge + Action Window)

### 3. MotionProfile 系统 (4_Data/3.Motion/ + 3_Gameplay/Motion/)

```
MotionProfileSO (SO 资产):
  AxisCurves: XYZ 三轴 AnimationCurve × Scale
  YMotion: None / Curve / GroundTargeted
  Gravity: UseGravity / SuspendGravity / AdditiveGravity
  GroundConstraint: ClampToGround / None
  MotionSpace: CharacterForward / CameraForward / LockTarget / WorldSpace
  AnimSpeedMode: Constant / Curve (SpeedOverTime)
  ScaleType: 属性缩放 (AttackSpeed 等)

   ↓ 运行时

MotionExecutor:
  Begin(profile, duration, direction, startPos, animSpeed)
  Tick(dt, timeScale, currentPosition):
    - 采样 AxisCurves(prevT→currT) → localDelta
    - 空间变换 (LocalToWorld using direction + right)
    - SetDesiredVelocity(desiredVelocity)
    - TickAnimSpeed (由 SPEED_CTRL 委托驱动 Animator.speed)
  SetPlaybackContext (Charge蓄力压速/循环窗/冻结时钟)

   ↓ DesiredVelocity

PlayerMotorAdapter → PlayerKCCMotor.ApplyMotorFromGameplayVelocity:
  MotionComposer.ComposeWorldVelocity (Motion+重力融合)
  CapsuleSweep + Collide&Slide (KinematicMotorSolver)
  Ground Snap + StepDown + EdgeSlip
```

**核心类**: `MotionProfileSO`, `MotionExecutor`, `MotionComposer`, `MotionContribution`, `GravityContribution`, `MotionGroundConstraint`, `MotionGroundLanding`, `MotionDurationResolver`

**位移控制权归属**: 当 `ActionDataSO.MotionProfile != null` 时，位移由 `MotionExecutor` 程序化控制；`MotionProfile == null` 时，位移由 `PlayerLocomotionState.MoveByLocomotionIntent` 或 `PlayerAirborneState.MoveByLocomotionIntent` 控制。

### 4. CombatGraph 系统 (4_Data/1.Skills/Routes/CombatFlow/ + 2_Framework/Skill/Routes/Runtime/)

```
CombatGraphAsset (SO):
  Nodes: CombatFlowGraphNode[] — 动作节点 (SkillRouteDefinition 引用)
  Edges: CombatFlowGraphEdge[] — 条件边 (CombatFlowConditionDefinition)
  EntryNode: 图入口

   ↓ 运行时

CombatGraphRunner:
  Attach(asset): 编译验证 + 初始化游标
  TryResolve(in intent, in context): 
    - 从当前节点找匹配入边
    - 评估 CombatFlowConditionDefinition (移动方向/空中/命中/资源/CD/标签...)
    - 返回目标 Route + Stage
  Tick(...): 游标推进 + 边评估

   ↓ 通过 SkillEntryService

DualGate: GraphDualGatePolicy
  - Full 参与: Graph 边命中 + ActionWindow 打断 双重闸门
  - SourceOnly: 只作图源，不要求在图中被边命中
  - None: 完全不走图
```

**核心类**: `CombatGraphAsset`, `CombatGraphRunner`, `CombatFlowConditionDefinition`, `CombatFlowGraphNodes`, `GraphDualGatePolicy`

### 5. Ability 系统 (2_Framework/Skill/Routes/Runtime/AbilityGateService.cs)

```
AbilityMapSO: AbilitySemantic → AbilityGateRuleSO[] 映射
AbilityGateRuleSO: AbilityTag 门控 (RequiredAll / Forbidden / Feature)

实体能力轨 (EntityCapabilityTag): 每帧由 EntityAbilitySystem.Update 写入
  例: CanAttack / CanJump / CanDodge / IsSilenced

Route.CanCast → AbilityGateService.Evaluate(route) → 检查 RequiredAll / Forbidden
```

**核心类**: `AbilityGateService`, `AbilityMapSO`, `AbilityGateRuleSO`, `EntityAbilitySystem`

---

## 系统调用关系

### PlayerStateManager.OnPreLogicUpdate (每帧仲裁帧序)

```
1. SkillEntries.TickCooldowns(dt)           — CD 减秒
2. IntentBuffer.FlushExpired(now)           — 清理过期意图
3. for each intent in buffer:
     a. TransitionResolver.CanOfferIntent   — 标签闸门
     b. SkillEntries.TryResolveForIntent    — Combat车道: Graph/Entry解析
     c. Current.TryConsumeGameplayIntent    — 状态本地闸门 + IntentRouter.Route
     d. SkillEntries.NotifyRouteEntered     — 提交ActiveRoute
     e. IntentBuffer.Pop()                  — 消费完成
```

### 调用链 (从按键到动作结束)

```
玩家按键
  → InputReader (InputAction回调 → SoA表)
  → PlayerController.Update:
      ConsumeDiscreteIntents → TryDispatchEntry → InputSemanticResolver
        → EnqueueSemantic → GameplayIntentBuffer.Enqueue
      Continuous: SetMovementIntent(worldDir, wantsRun)
  → PlayerStateManager.OnPreLogicUpdate (仲裁)
  → IntentRouter.Route → ForceChange<PlayerActionState>
  → PlayerActionState.OnEnter:
      MotionExecutor.Begin (MotionProfile)
      RequestActionPresentation (Animation)
  → PlayerActionState.OnLogicUpdate:
      ActionTimelineRuntime.Tick (HitFrame/标签)
      SkillEntries.TickActive (Stage推进)
      MotionExecutor.Tick (位移)
  → 结束条件: nt≥1 && routeEnded
  → ExitToBaseline: Change<PlayerLocomotionState>
```
