# 08_StateMachineCatalog — 状态机目录

> **生成时间**: 2026-06-08  
> **分析方法**: 代码全量扫描 4 支柱状态实现

---

## 状态拓扑图 (四支柱)

```
                    ┌──────────┐
                    │   DEAD   │ ← 终态，任意状态可进入
                    │ (终态)   │ ← 输入禁用，移动停止
                    └──────────┘
                         ↑ IsDead=true (任意状态)
                         │
    ┌────────────────────┼────────────────────┐
    │                    │                    │
    ▼                    ▼                    ▼
┌──────────┐   Jump   ┌──────────┐  Skill   ┌──────────┐
│LOCOMOTION│─────────→│ AIRBORNE │─────────→│  ACTION  │
│          │←─────────│          │←─────────│          │
│ Idle     │ Grounded │ Asc/Desc │ 结束     │ 动作播放 │
│ Walk     │ (自动)   │          │ (nt≥1)  │          │
│ Run      │          │ 落地+L  │          │JumpLand中│
│ Turn     │          │ ──────→ │          │←─────────│
└──────────┘          └──────────┘          └──────────┘
     ↑                                            │
     └──────────── 动作结束 ──────────────────────┘
```

---

## 状态详情

### 1. PlayerLocomotionState — 地面运动支柱

| 属性 | 值 |
|------|-----|
| **StateId** | `Locomotion` |
| **进入条件** | 动作结束 (nt≥1 && routeEnded) / Airborne→Grounded (无JumpLand) |
| **退出条件** | IsDead / !IsGrounded / IntentRouter.Route (→Action/Airborne) |
| **允许打断到** | 由 `locomotionAllowedCategories` 掩码控制 |
| **当前动画** | [待验证 — 由 Animator Controller Locomotion BlendTree 驱动] |
| **是否接入Action系统** | ✅ 是 (通过 `TryConsumeGameplayIntent` → `IntentRouter.Route`) |
| **输入** | `Player.MovementIntent`, `Player.WantsRun` |
| **输出** | `MoveByLocomotionIntent` → `SetPlanarVelocity` → `ApplyMotor(Locomotion)` |
| **核心行为** | |
| — 移动 | `MoveByLocomotionIntent`: accel→targetSpeed→SetPlanarVelocity |
| — 停止 | `StopMove`: decel→0 |
| — 转身 | `TurnResolver.Tick`: 平滑转身插值 |
| — 标签 | `Clear()` → `Add(Grounded)` → `EntityAbilitySystem.Update` |

**移动公式**:
```
inputMag = clamp01(input.magnitude)
speedCap = wantsRun ? RunSpeed : WalkSpeed
targetSpeed = speedCap * inputMag * externalMultiplier
newSpeed = MoveTowards(currentSpeed, targetSpeed, accel*dt)
SetPlanarVelocity(dir * newSpeed)
```

### 2. PlayerAirborneState — 滞空支柱

| 属性 | 值 |
|------|-----|
| **StateId** | `Airborne` |
| **进入条件** | !IsGrounded (非Action) / Jump intent / IntentRouter.Route |
| **退出条件** | IsDead / IsGrounded → TryExitToLandOrLocomotion |
| **允许打断到** | ascending/descending 分别控制 (由 `airborneAllowedCategories` 掩码) |
| **当前动画** | [待验证 — JumpStart/JumpLoop 动画] |
| **是否接入Action系统** | ✅ 是 (通过 `TryConsumeGameplayIntent` → `IntentRouter.Route`) |
| **输入** | `Player.MovementIntent`, `Player.WantsRun`, `Player.VerticalSpeed` |
| **输出** | `MoveByLocomotionIntent(airMoveMultiplier)` → `ApplyMotor(Airborne)` |
| **分相行为** | |
| — 上升 (vy>0) | `ascendingAllowedCategories` 闸门 |
| — 下降 (vy≤0) | `descendingAllowedCategories` 闸门, 发布 `JumpAirPhaseEvent`, SyncJumpLoopGraphContext |
| — 落地 | `JumpLand != null` → `ArmPendingAction` → `Change<ActionState>` (播落地动作) |
| — 无JumpLand | `ClearGraphContextAction` → `Change<LocomotionState>` |

**空中移动公式**:
```
MoveByLocomotionIntent(airMoveMultiplier * externalSpeedMultiplier, wantsRun)
  = 同 Locomotion 加速度模型, 但 targetSpeed *= airMoveMultiplier (默认 0.6)
```

### 3. PlayerActionState — 动作支柱

| 属性 | 值 |
|------|-----|
| **StateId** | `Action` |
| **进入条件** | IntentRouter.Route (Skill_Entry_* intent / JumpLand) |
| **退出条件** | nt≥1 && routeEnded → ExitToBaseline |
| **允许打断到** | 由 ActionWindow + `ActionInterruptResolver.CanInterrupt` 控制 |
| **当前动画** | `action.MainClip` (通过 `RequestActionPresentation` 事件 → Animation) |
| **是否接入Action系统** | ✅ 是 (本身就是Action的执行载体) |
| **核心行为** | |
| — 进入 | `TryTakePendingAction` → `MotionExecutor.Begin` → `BeginActionMotorSession` → `Tags.Add(PhaseStartup)` → `RequestActionPresentation` |
| — 每帧 | `EvaluatePhaseTags(nt)` → `ActionTimelineRuntime.Tick` → `SkillEntries.TickActive` → `MotionExecutor.Tick` |
| — 退出 | `MotionExecutor.End` → `ReleaseGravity` → `EndActionMotorSession` → `NotifyRouteExited` → CD启动 |
| — 换段 | `SwapToStageAction` (MultiStage 无缝衔接下一段) |

**打断协议**:
```
CanInterrupt 判定:
  1. 硬打断: incomingPriority > action.InterruptStability → true
  2. 窗口打断: IsCategoryAllowedAtWindow(action, nt, incomingCategory)
  3. 双闸门: GraphEnabled + Full参与 + !LastIntentResolvedViaGraph → BLOCK
```

### 4. PlayerDeadState — 死亡终态

| 属性 | 值 |
|------|-----|
| **StateId** | `Dead` |
| **进入条件** | `Player.IsDead == true` (任意状态检测) |
| **退出条件** | 外部复活系统 → `Change<LocomotionState>` (需代码支持) |
| **允许打断到** | 无 (不实现 `TryConsumeGameplayIntent`) |
| **当前动画** | [待验证 — 死亡动画] |
| **是否接入Action系统** | ❌ 否 (完全禁用) |
| **核心行为** | `Tags.ClearAll()` → `Tags.Add(Dead)` → `IntentBuffer.Clear()` → `StopMove` → `DisableGameplayExceptPartySwitch` → `ApplyMotor(DeadPhysics)` |

---

## 状态迁移规则

### 进入条件

| 源状态 | 目标状态 | 条件 | 触发位置 |
|--------|---------|------|---------|
| Any | **Locomotion** | 动作结束到地面 `ExitToBaseline` | `PlayerActionState.OnLogicUpdate` |
| Airborne | **Locomotion** | 落地且无 JumpLand | `PlayerAirborneState.TryExitToLandOrLocomotion` |
| Any | **Airborne** | `!IsGrounded` (Locomotion检测) | `PlayerLocomotionState.OnLogicUpdate` |
| Any | **Airborne** | Jump intent | `IntentRouter.Route` via `TryConsumeGameplayIntent` |
| Any | **Action** | Skill_Entry_* intent 仲裁通过 | `IntentRouter.Route` via `TryConsumeGameplayIntent` |
| Airborne | **Action** | JumpLand 落地 | `TryExitToLandOrLocomotion` |
| Action | **Action** | JumpLand after air combat | `ExitToBaseline` |
| Any | **Dead** | `IsDead == true` | 各状态 `OnLogicUpdate` |

### 退出条件

| 状态 | 退出条件 | 处理 |
|------|---------|------|
| **Locomotion** | Switch to Action/Airborne/Dead | `TurnResolver.ClearLock`, 清除转身 |
| **Airborne** | Landing or Switch to Action/Dead | 发布 `PlayerLandedEvent` |
| **Action** | nt≥1 && routeEnded | CD结算, 重力释放, 马达会话结束 |
| **Dead** | Resurrection | 恢复Input, 移除Dead标签 |

---

## 输入消费优先级

```
每帧仲裁 (PlayerStateManager.OnPreLogicUpdate):
  for intent in buffer (max 1):
    ① TransitionResolver (全局闸门: 过期/禁止标签/缺少必要标签)
    ② SkillEntryService (Combat车道: Graph/Route解析)
    ③ Current.TryConsumeGameplayIntent (状态本地闸门)
       ├── Locomotion: Category match (locomotionAllowedCategories)
       ├── Action: ActionWindow + InterruptPriority + DualGate
       └── Airborne: ascending/descending Category match
    ④ IntentRouter.Route → 状态迁移
```

---

## 帧内执行顺序

```
同一帧:
  1. PlayerController.Update [-50]:
     - 消费离散脉冲 → IntentBuffer
     - 连续移动采样 → SetMovementIntent
  2. PlayerStateManager.OnPreLogicUpdate:
     - CD减秒 + Intent仲裁 + 状态迁移
  3. PlayerStateManager.Update → LogicUpdate:
     - 当前状态推进 (Motion/Action Timeline/Skill Stage)
  4. Animator (由 PlayableGraph 驱动):
     - 响应 AnimSpeed 变化
```

---

## [待验证]

1. **Animator Controller 实际状态机**: BlendTree 结构, 过渡条件
2. **JumpStart/JumpLoop 动画**: LocomotionGraphContext 绑定的具体动画
3. **复活逻辑**: 是否已实现, 从 Dead→Locomotion 的路径
4. **日/夜/潜行等扩展状态**: 是否在规划中
5. **TurnSettings 具体参数**: 转身速度/阈值
