# 09_TechDebt — 架构债务与风险分析

> **生成时间**: 2026-06-08  
> **分析方法**: 全量代码扫描 + 调用链追踪 + 控制权分析  
> **原则**: 以代码事实为准，不脑补，不推测。无法确认处标记 [待验证]

---

## ═══════ 重点调查：11 个关键问题 ═══════

### 1. Locomotion 是否绕过 Action 系统？

**结论**: ❌ 不绕过。Locomotion 通过 IntentRouter 标准路由进入。

**证据**:
- `PlayerLocomotionState.OnLogicUpdate` 使用 `MoveByLocomotionIntent` + `SetPlanarVelocity` + `ApplyMotor(MotorSolveContext.Locomotion)` 驱动位移
- 移动到 Action 的过渡走 `TryConsumeGameplayIntent` → `IntentRouter.Route` (Skill_Entry_* → `Change<ActionState>`)
- Locomotion 本身不作为 "一个 Action"，而是作为独立支柱状态

**设计意图**: Walk/Run/Idle 属于基础运动状态，不属于 Action 语义范畴。这是合理的四支柱分离设计，不是绕过。

**风险级别**: 🟢 无风险

---

### 2. Jump 是否绕过 Action 系统？

**结论**: ⚠️ 部分绕过。Jump 本身不经过 SkillEntry 管线，但落地 (JumpLand) 可以走 Action。

**证据**:
- `IntentRouter.Route` 中: `Jump` → `RequestJumpFromIntent` + `Change<AirborneState>` — 不走 SkillEntryService
- `PlayerAirborneState.OnEnter`: 如果有 JumpStart (LocomotionGraphContext) → `SetGraphContextAction` — 写入 Graph 上下文但**不进入 ActionState**
- `PlayerAirborneState.OnLogicUpdate: 落地 + JumpLand != null` → `ArmPendingAction` → `Change<ActionState>` — 落地才进 Action

**影响**:
- Jump 期间不受 ActionWindow / Interrupt 体系约束
- Jump 期间不能通过 SkillEntryService 做 Combo/Routing 决策
- JumpStart 动作只写入 GraphContext，不实际播放 (除非通过 AirborneState 触发)

**风险级别**: 🟡 中等
**建议方向**: 如果要让跳跃中也能释放技能 → 当前架构已支持 (Airborne→Action via TryConsumeGameplayIntent)。
如果 JumpStart 需要播一个动作，可考虑在 AirborneState.OnEnter 中播 Action。

---

### 3. Land 是否绕过 Action 系统？

**结论**: ✅ 不绕过 (如果配置了 JumpLand)。但落地方式取决于配置。

**证据**:
- `PlayerAirborneState.TryExitToLandOrLocomotion`:
  ```
  if JumpLand != null:
    ArmPendingAction(Jump, JumpLand)
    Change<PlayerActionState>()  ← 经 Action 系统
  else:
    Change<PlayerLocomotionState>()  ← 直接回到 Locomotion
  ```
- 跳入 ActionState 后的落地 (空中战斗): `ExitToBaseline` → JumpLand 检测 → `ArmPendingAction` → `Change<ActionState>`

**影响**: 有 JumpLand 配置时 Land 走 Action 系统。没有配置时绕过。

**风险级别**: 🟢 低风险 (可选配置，设计合理)

---

### 4. TurnAround 是否绕过 Action 系统？

**结论**: ✅ 不绕过。在原地转身 (Turn-In-Place) 是在 LocomotionState 内通过 TurnResolver 处理的。

**证据**:
- `PlayerLocomotionState.OnLogicUpdate`: 
  ```
  TurnResolver.Tick(player, dt, turnSettings)
  → turnInfo (TurnState + Angle)
  ```
- TurnResolver 不产生 GameplayIntent，不进入 ActionState
- 动画由 `PlayerTurnBackFlowPresentation` / `PlayerTurnOrbPresentation` 在表现层驱动 (订阅 EventBus)

**设计意图**: 转身是基础运动行为，不是技能动作。与 Walk/Run 同级。

**风险级别**: 🟢 无风险 (设计合理)

---

### 5. Movement 是否直接驱动角色位移？

**结论**: ✅ 是的。Locomotion/Airborne 状态下 Movement 直接通过 SetPlanarVelocity 驱动 KCC。

**证据**:
- `MoveByLocomotionIntent` 直接调用 `SetPlanarVelocity(targetVelocity)`
- `ApplyMotor(MotorSolveContext.Locomotion)` 直接求解物理
- 不经过 MotionExecutor，不经过 ActionSystem

**风险级别**: 🟢 无风险 (这是 Locomotion 设计的本意)

---

### 6. MotionProfile 是否与 Movement 产生控制权竞争？

**结论**: ⚠️ 存在潜在竞争窗口。在 Action 过程中，MotionProfile 与 Locomotion Movement 使用同一个 KCC 马达。

**证据**:
- Action 期间: `MotionExecutor.Tick` → `SetDesiredVelocity` → `ApplyMotorFromGameplayVelocity` (useMotionComposer=true)
- Action 期间仍有 MoveIntent 入队: `TryEnqueueMoveInterruptIntent` 在 ActionWindow 放行时入队 Move 意图
- 但 Move 意图被 LocomotionState 消费 (需要 `IntentRouter.Route` → `Change<LocomotionState>`)
  - 这意味着在 Action **内部**，WASD 驱动的 Movement **不会**与 MotionProfile 竞争 —— 因为要切换到 LocomotionState 才行
- **但**: `PlayerActionState.TryConsumeGameplayIntent` 中:
  ```
  if ActionInterruptResolver.CanInterrupt → NotifyRouteExited(interrupted:true) → IntentRouter.Route → ForceChange<ActionState>
  或: → Change<LocomotionState> (Move intent)
  ```

**实际情况**: Action 内部 WASD 不会直接驱动位移，它通过 Move intent → 打断当前动作 → 切回 LocomotionState → 然后 Locomotion 接管位移。

**风险级别**: 🟡 中等
**建议方向**: 当前设计清晰——要么Action控制位移, 要么Locomotion控制位移。但不支持"动作中微调方向"(如: 前冲时可微调左右)。如需此特性，需要在 MotionExecutor 中处理 MoveIntent 叠加。

---

### 7. 技能释放期间是否仍允许 WASD 驱动角色？

**结论**: ⚠️ **不允许直接驱动，但可以在窗口内打断退出**。

**证据**:
- Action 期间，`PlayerController.TryEnqueueMoveInterruptIntent`:
  ```
  检测当前是 PlayerActionState
  → TryGetActiveActionInterruptProbe → 获取当前 action + nt
  → IsCategoryAllowedAtWindow(action, nt, ActionCategory.Locomotion)
  → 若允许：入队 Move intent
  ```
- 这个 Move intent 必须通过完整仲裁：
  `TransitionResolver → ActionInterruptResolver.CanInterrupt → IntentRouter.Route → Change<LocomotionState>`
- 只有切换到 LocomotionState 后，WASD 才开始驱动位移

**实际效果**: Action 后摇窗口内移动 → 打断退出 → 回到 Locomotion → WASD 控制移动。

**风险级别**: 🟢 低风险 (设计明确: Action优先, 窗口内可移动打断退出)

---

### 8. 落地动作是否可被移动打断？

**结论**: ⚠️ 取决于 JumpLand Action 的 Windows 配置。代码层面可能。

**证据**:
- JumpLand 是一个普通的 ActionDataSO → 进入 PlayerActionState 播放
- PlayerActionState.TryConsumeGameplayIntent 中:
  - Move intent → ResolveIncomingCategory → ActionCategory.Locomotion
  - CanInterrupt: 检查当前 JumpLand action 的 Windows
  - 若 Windows 中某窗口允许 Locomotion → 可以打断
  - JumpLand 需要 `AllowSelfInterrupt=false` 且 Windows 不包含 Locomotion 类别 = 不可被移动打断

**风险级别**: 🟡 中等 (取决于资产配置，需逐个 JumpLand Action 验证)
**建议方向**: 检查各 JumpLand Action 的 Windows 配置，确保"不可被移动打断"。

---

### 9. 滑铲期间是否可被 WASD 改变方向？

**结论**: 🔴 **[待验证]** — 项目中是否有"滑铲"Action 需要确认。

**分析**:
- 如果"滑铲"是一个 Action (有 MotionProfile)，则适用 ActionState 规则: WASD 可以打断 (如果窗口允许) 但不能改变滑铲方向
- MotionExecutor 的方向在 `Begin()` 时确定 (`m_burstFaceDir`)，Tick 中不重新读取 MovementIntent
- `MotionSpace.CharacterForward` → 方向 = 角色面朝 (进入时快照)
- 如果需要"滑铲中可微调方向" → 需要额外设计 (如 `MotionSpace.InputDirection`)
- 可通过 `MotionSpace.CameraForward` 实现方向跟随镜头但不跟随 WASD

**风险级别**: 🟡 中等 (取决于是否要实现此特性)
**建议方向**: 如果需要，增加 `MotionSpace.InputDirection` 枚举，Tick 中读取 MovementIntent 更新方向。

---

### 10. PlayableGraph 与 StateMachine 谁拥有动画最终控制权？

**结论**: ⚠️ **[待验证]** — 需要分析 Animator Controller 和 PlayableGraph 的实际设置。

**证据**:
- 动画播放请求通过 EventBus 发送: `PlayerActionPresentationRequestEvent`
- `EntityAnimController` / `PlayerAnimController` 订阅此事件 → 操作 PlayableGraph
- AnimSpeed 由 `MotionExecutor` 通过 `IAnimSpeedControl.SetSpeed()` → `Animator.speed` 控制
- **潜在冲突**: 
  - Animator Controller 有自己的状态机 (可能包含 BlendTree 过渡逻辑)
  - 代码通过 PlayableGraph CrossFade 强制播放 Clip
  - 代码通过 Animator.speed 控制速度
  - 三者在同一帧可能产生竞态

**需要验证的问题**:
1. Animator Controller 中是否使用了 StateMachineBehaviour 脚本
2. PlayableGraph 的操作方式 (CrossFade / Play / 自定义混合)
3. Animator.speed 是否被 Animator Controller 的过渡覆盖
4. RootMotion 是否被应用 (如果启用则可能与 MotionProfile 叠加)

**风险级别**: 🟡 中等

---

## ═══════ 架构债务清单 ═══════

### 🔴 严重问题

| # | 问题 | 现状 | 影响 | 文件 |
|---|------|------|------|------|
| 1 | **Player.cs 神对象** | ~500行，持有 Input/Movement/Skill/Tag/Combat/Motor 全部引用和转发 | 修改任何子系统都需动 Player; 测试困难; 职责边界模糊 | `Player.cs` |
| 2 | **PlayerStateManager 仲裁过重** | OnPreLogicUpdate 包含 TransitionResolver + SkillEntryService + State.Gate + IntentRouter 全部逻辑 | 无法单独测试仲裁逻辑; 扩展新状态需修改仲裁器 | `PlayerStateManager.cs` |
| 3 | **CombatGraph 与 EntryRoute 双轨** | SkillEntryService 内部同时维护 CombatGraph 路径和 Entry 单轨 fallback; GraphDualGatePolicy 双闸门 | 两条路径的交互边界不清晰; 调试困难 | `SkillEntryService.cs`, `GraphDualGatePolicy.cs` |
| 4 | **SkillEntryService 2000+ 行** | 单文件包含 Rebuild/TryResolve/CD/ComboSession/Graph/GroupCooldown/HitTally/ComboHandoff 全部职责 | God Object 风险; 难以理解和维护 | `SkillEntryService.cs` |

### 🟡 中等问题

| # | 问题 | 现状 | 影响 | 文件 |
|---|------|------|------|------|
| 5 | **GameplayIntentKind 枚举** | 17个 Skill_Entry_NN + Jump + Move = 19个值，虽然已清理历史遗留 | 每次加槽位需改枚举 | `GameplayIntent.cs` |
| 6 | **静态管线不可测** | TransitionResolver, ActionInterruptResolver, IntentRouter 全部 static | 无法注入 mock; 无法单元测试 | 多个文件 |
| 7 | **CostCommitPolicy 缺失** | SkillRouteRuntime 资源扣减在策略触发时就扣，不支持"命中才扣"的异步确认 | 资源回滚困难 | `SkillRouteRuntime.cs` |
| 8 | **EffectSystem 未统一** | 陷阱/Buff/技能效果的投递路径尚未完全统一 | 新增效果类型需走不同路径 | `EffectSystem.cs` (规划中) |
| 9 | **CombatGraph 图编辑器耦合** | CombatFlowGraphWindow 深度依赖 GraphProcessor 第三方库 | 第三方库升级可能破坏编辑器 | `CombatFlowGraphWindow.cs` |
| 10 | **PlayerAnimController 与代码动画控制关系不明确** | [待验证] 动画控制链路: Animator Controller → PlayableGraph → AnimSpeed → RootMotion | [待验证] | [待验证] |

### 🟢 轻微问题

| # | 问题 | 现状 | 影响 | 文件 |
|---|------|------|------|------|
| 11 | **Objsolete 属性残留** | MotionProfileSO 中有 BurstDurationSeconds/LegacyConstantPlanarSpeed 等 [Obsolete] 字段 | 序列化兼容性负担 | `MotionProfileSO.cs` |
| 12 | **MotionYAxisLegacyMapping** | 旧 YAxisPolicy 映射逻辑保留 | 迁移窗口已过 | `MotionYAxisLegacyMapping.cs` |
| 13 | **Starter Assets 残留** | Third_Party_Assets 中仍有 Unity Starter Assets 的 ThirdPersonController | 不使用但占用编译 | `Third_Party_Assets/Starter Assets/` |
| 14 | **Editor 中 #if UNITY_EDITOR 不统一** | 部分 Editor 文件用了 #if，部分没封装 | 编译边界不清晰 | Editor/ 目录 |

---

## ═══════ 控制权冲突检测 ═══════

### 位移控制权

| 场景 | XZ 控制者 | Y 控制者 | 潜在冲突 |
|------|----------|---------|---------|
| Locomotion | `MoveByLocomotionIntent` | 重力系统 | 🟢 无 |
| Airborne | `MoveByLocomotionIntent(0.6x)` | 重力系统 | 🟢 无 |
| Action + MotionProfile | `MotionExecutor.AxisCurves` | MotionProfile配置 | 🟢 无 |
| Action (无Motion) | 无脚本位移 | 可Suspend | 🟢 无 |
| Action → WASD打断 | Locomotion接管 | 重力系统 | 🟢 无 |

### 动画控制权

| 控制源 | 方式 | 优先级 |
|--------|------|--------|
| Animator Controller | BlendTree / StateMachine | [待验证] |
| PlayableGraph (代码) | CrossFade to Clip | [待验证] |
| AnimSpeed (代码) | `Animator.speed = value` | [待验证] |
| RootMotion | Animator.applyRootMotion | [待验证] |

### 状态控制权

| 控制源 | 方式 | 优先级 |
|--------|------|--------|
| PlayerStateManager | `Change<T>` / `ForceChange<T>` | 最高 |
| 各状态 OnLogicUpdate | 自动迁移 (!IsGrounded→Airborne) | 次高 |
| IntentRouter | 意图路由 → 状态切换 | 通过仲裁 |

---

## ═══════ 数据源冲突检测 ═══════

| 数据 | 唯一来源 | 多处写入 | 风险 |
|------|---------|---------|------|
| 位移 (XZ) | MotionExecutor 或 MoveByLocomotionIntent | ✅ 互斥 | 🟢 |
| 垂直速度 (Vy) | KCC Motor 内部 | ⚠️ MotionComposer + Gravity + GroundSnap 都可能改 | 🟡 |
| IsGrounded | KCC Motor.RefreshGroundedState | ⚠️ ActionAirborneLock 覆盖 | 🟡 |
| 动画速度 | MotionExecutor.AnimSpeed | ✅ 单点 | 🟢 |
| 技能CD | SkillEntryService.TickCooldowns | ✅ 单点 | 🟢 |
| 资源值 | ResourcePool | ✅ 单点 | 🟢 |
| 标签 | GameplayTagContainer | ⚠️ 各 State.OnEnter + EntityAbilitySystem + ActionPhase | 🟡 |
| GraphContextAction | Player.SetGraphContextAction | ⚠️ 4个调用点 (Locomotion/Airborne/Action/JumpLand) | 🟡 |
| PendingAction | Player.ArmPendingAction | ⚠️ SkillEntryService + ExitToBaseline + TryExitToLandOrLocomotion | 🟡 |

---

## ═══════ 职责重叠检测 ═══════

| 重叠域 | 参与方 | 说明 | 风险 |
|--------|--------|------|------|
| Intent → Route 解析 | SkillEntryService + CombatGraphRunner + RouteResolver | 多层嵌套解析，回退逻辑复杂 | 🟡 |
| CD 管理 | SkillRouteRuntime.基类 + SkillEntryService.TickCooldowns + GroupCooldown | CD 有 Route 级/Group 级/Combo Session 级三层 | 🟡 |
| 过渡条件评估 | SkillRouteRuntime.EvaluateTransitions + ConditionEvaluator + CombatFlowConditionDefinition | Route 内 Transition 和 Graph 边条件用了不同评估路径 | 🟡 |
| 标签刷新 | LocomotionState + AirborneState + ActionState.EvaluatePhaseTags | 每个状态有自己的标签清理/写入逻辑，时机不同 | 🟡 |

---

## ═══════ 已知架构风险 (来自 HEARTBEAT.md) ═══════

| 风险 | 状态 | 备注 |
|------|------|------|
| IntentKind 枚举爆炸 | 已缓解 | 从38值 → 19值 (Skill_Entry_NN 统一) |
| Player.cs 神对象 (~800行) | 待处理 | 规划 Phase 2 拆出 PlayerSkillComponent |
| 静态管线不可测 | 待处理 | Phase 3 实例化 |
| 双轨迁移未收尾 (weaponMoveset + skillLoadout) | 待处理 | 全技能化后退役 Moveset |
| CostCommitPolicy 缺失 | 待处理 | Phase 4 |

---

## ═══════ 建议优先级 ═══════

| 优先级 | 事项 | 影响范围 | 预估工作量 |
|--------|------|---------|-----------|
| P0 | PlayableGraph 动画控制权确认 | 动画系统 | 调研 |
| P0 | 落地动作不可被移动打断 (配置验证) | JumpLand Actions | 配置 |
| P1 | Player.cs 拆分 (SkillComponent) | 全系统 | 大 |
| P1 | SkillEntryService 职责拆分 | Skill 系统 | 中 |
| P1 | 静态管线实例化 (TransitionResolver 等) | Action 系统 | 中 |
| P2 | EffectSystem 统一入口 | Combat 系统 | 中 |
| P2 | CostCommitPolicy (命中才扣费) | Skill 系统 | 小 |
| P2 | CombatGraph 双轨简化 | Skill 系统 | 中 |
| P3 | MotionProfile Obsolete 字段清理 | 数据层 | 小 |
| P3 | 第三方资产清理 (Starter Assets) | 项目 | 小 |
