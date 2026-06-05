# Ver 4.3.5 — 输入 → 技能响应 全景技术文档

> 本文档以 Ver 4.3.5 当前代码仓为基准，覆盖「物理按键 → 翻译层 → 意图缓冲 → 仲裁 → 技能装配 → Action 执行 → Motion/动画/伤害收尾」的完整数据流，并列出沿途所有旁路（UI、Camera、Pause、Party、Rebind）。文档不使用流程图语言，统一采用「分层框格 + 显式调用链」表达。

---

## 1. 顶层全景（Big Picture）

整套管线共分 7 层。任何一帧的「按下一个键 → 角色播一个技能」一定按这个顺序穿过：

| # | 层 | 关键类型 | 责任（一句话） |
|---|----|---------|----------------|
| 1 | 设备 | Unity Input System (.inputactions) | 把物理按键归一为命名 `InputAction` |
| 2 | 翻译 | `InputReader (ScriptableObject)` | 物理回调 → 连续量属性 + 离散脉冲 + Slot 双写表 |
| 3 | 采样 | `PlayerController.Update()` | 每帧把脉冲转成 `GameplayIntent` 入队、把 MoveInput 转成 `MovementIntent` |
| 4 | 缓冲 | `GameplayIntentBuffer` (cap=16, ring) | 带 ExpireTime 的零 GC 环形缓冲 |
| 5 | 仲裁 | `SkillSystem` → `TransitionResolver` → 当前 `EntityState` | 三道闸门：技能资格 / 标签 / 状态本地 |
| 6 | 装配 | `SkillSegmentResolver`+`SkillCastHandlerFactory`+`SkillChargeCommit` | 解析 Stage、装配 Cast 处理器、Charge 替身 |
| 7 | 执行 | `PlayerActionState` + `TaskExecutor` + `MotionExecutor` + `PlayerAnimController` | 跑动作时间线、派发 Task、推动位移与动画 |

---

## 2. 输入 → 技能响应 主路由（核心管线）

下面是「按下 Q 释放一个 Ability_06 普通技能」从硬件到伤害的完整调用链（其它槽位机理一致，仅在 §4 列差异）：

```
[ 1 ] 硬件 / 设备
      └─ Keyboard "Q" / Mouse / Gamepad
         └─ Unity Input System: InputAction "SlotAbility_06" (performed)

[ 2 ] 翻译层  (InputReader.cs : IGamePlayActions)
      └─ OnSlotAbility_06(ctx)
         ├─ _ability06PressedPulse = true            // 兼容旧 API
         └─ WriteSlotEdge(SkillSlotType.Ability_06, pressed:true)
             ├─ _slotPressedPulses[Ability_06] = true
             ├─ _slotHeld       [Ability_06] = true
             └─ _slotHeldStartTime[Ability_06] = Time.time
         // 设计准则：InputReader 只翻译，不决策；不入 EventBus。

[ 3 ] 采样层  (PlayerController.Update → ConsumeDiscreteIntents)
      └─ TryDispatchSlot(SkillSlotType.Ability_06)
         ├─ PlayerIntentCatalog.HasFactoryFor(slot) → true
         ├─ inputReader.ConsumeSkillSlotPressed(slot, out holdSeconds)
         ├─ intent = PlayerIntentCatalog.ForSlot(slot, Time.time, primaryHold:holdSeconds)
         │           // ForSlot 内部: Kind=CastAbility1, HasSkillSlot=true, SkillSlot=Ability_06
         │           //               ExpireTime = time + buffer (输入缓冲窗口)
         │           //               RequiredAll/Forbidden 标签 (由 IntentCatalog 配置)
         └─ player.EnqueueGameplayIntent(intent)
             └─ GameplayIntentBuffer.Enqueue(in intent)   // 环形, 容量 16, 0-GC

[ 4 ] 缓冲层  (GameplayIntentBuffer)
      └─ 队列首意图等待下一次 Pre-Logic Tick 出列
      └─ FlushExpired 会按 Time.time > intent.ExpireTime 丢弃过期项

[ 5 ] 仲裁层  (PlayerStateManager.OnPreLogicUpdate, exec order = -50)
      帧序固定，maxIntentConsumptionsPerFrame = 1：
      ├─ Entity.TickSkillRuntimes(dt)         // 推 CD / 回充 / 二段窗 / Combo 超时
      ├─ Entity.IntentBuffer.FlushExpired(now)
      ├─ TryPeek 队首
      ├─ [5.1] SkillSystem.TryPrepareIntentForSkills(player, ref intent)
      │        ├─ TryMapIntentToSlot(...)           // (slot 优先 / Catalog / 默认表)
      │        ├─ TryGetResolvedSkill(slot)         // SkillLoadoutSO.Resolve(slot)
      │        ├─ TryGetSkillRuntime(slot)          // 取 SkillRuntime
      │        ├─ Combo 过期 → runtime.ResetCombo
      │        ├─ chargeHoldSeconds = (HeavyAttack|CastUltimate?
      │        │                       intent.SecondaryHoldDurationSeconds :
      │        │                       intent.PrimaryHoldDurationSeconds)
      │        ├─ segment =  rootData.castType==Charge ? rootData
      │        │              : SkillSegmentResolver.ResolveSegment(runtime)
      │        ├─ runtime.CanCast(stats, resources, ref tags, self)     // CD/资源/Status.Stun
      │        ├─ SkillGcd.IsGlobalCooldownActive() (跳过 ignoreGlobalCooldown)
      │        ├─ secondBumpDeferred = (SecondStageAvailable && stages.Length>1 && idx==0)
      │        ├─ stage = SkillSegmentResolver.ResolveActiveStage(segment, effectiveIdx)
      │        ├─ actionOut = stage.action
      │        ├─ SkillChargeCommit.TryApplyChargeOverride(
      │        │       segment, slot, intent.Kind, chargeHoldSeconds,
      │        │       ref stageResolved, ref actionOut, out chMul)
      │        │     // Charge 替身: 命中 ChargeLevel 时替换 stage/action/damage
      │        ├─ intent.Action = actionOut
      │        ├─ ctx = player.BorrowSkillContext()
      │        │       ctx.Runtime / Stage / ResolvedSheet / Target / CastHandler
      │        ├─ ctx.CastHandler = SkillCastHandlerFactory.Create(segment)
      │        │     // Instant / CastTime / Channel / Charge / HoldRelease
      │        ├─ BootstrapCastHandshake → handler.OnInputPressed(ctx)
      │        └─ player.BeginDeferredSkillPlanning(ctx, slot, runtime, secondBumpDeferred)
      │           // ※ 注意：此时尚未真正提交，等待 TransitionResolver 通过
      │
      ├─ IntentBuffer.ReplaceFront(in intent)      // 写回 intent.Action
      │
      ├─ [5.2] TransitionResolver.CanOfferIntent(in ctx, in intent, out reason)
      │        ├─ time < ExpireTime
      │        ├─ stateTags  & ForbiddenTags        == 0
      │        ├─ stateTags  & RequiredAllTags      == RequiredAllTags
      │        ├─ abilityTags & ForbiddenAbilityTags == 0
      │        ├─ abilityTags & RequiredAllAbilityTags == RequiredAllAbilityTags
      │        └─ RequiredAnyTags 至少命中一位
      │        ── 失败：player.CancelDeferredSkillPlanning(); break
      │
      ├─ Entity.FinalizeDeferredSkillPlanning()    // 把 SkillContext 提交为 ActiveSkillContext
      │
      ├─ [5.3] Current.TryConsumeGameplayIntent(player, in ctx, in intent)
      │        ├─ Locomotion → 整段 ActionCategory 掩码 → 切到 ActionState
      │        ├─ Airborne  → 上升/下降 两份掩码（按 vy 切换）
      │        ├─ Action    → ActionInterruptResolver.CanInterrupt(action, t, intent, incomingAction)
      │        │             命中 ActionWindow 时切招；硬打断走优先级 > InterruptStability
      │        └─ Dead      → 拒绝一切
      │        ── 失败：player.RevertCommittedSkillPlanningAfterFailedConsume(); break
      │
      └─ Entity.AcknowledgeCommittedSkillConsumed(); IntentBuffer.Pop()

[ 6 ] 装配出口 → 路由
      └─ IntentRouter.Route(player, in intent, forceActionReentry)
         ├─ Jump        → player.RequestJumpFromIntent() + States.Change<PlayerAirborneState>
         └─ (其余全部) → player.ArmPendingAction(kind, ResolveActionData(player, in intent))
                          + States.Change<PlayerActionState>()   (或 ForceChange)

[ 7 ] 执行层  (PlayerActionState.OnEnter / Update)
      ├─ 读取 player.PendingAction / ActiveSkillContext
      ├─ MotionExecutor.Begin(action.MotionProfile, duration, motionDir, pos,
      │                       baseAnimSpeed = action.AnimSpeed)
      │     ── v4.5 三层动画速率：finalClipSpeed = ActionData.AnimSpeed × profileFactor
      │        profileFactor ∈ {Constant=1 | Curve=SpeedOverTime(t) | StrideMatch=clamp(v/Ref)}
      ├─ TaskExecutor.StartAll(stage.tasks, ctx)
      │     ── ShowIndicator / WaitConfirm / WaitWindowSignal / Dash / MoveToTarget
      │     ── SpawnProjectile / SpawnSummon / ApplyDamage / ApplyEffect / ModifyCooldown
      ├─ 每帧：
      │   ├─ ctx.CastHandler.OnUpdate(ctx, dt)
      │   ├─ SkillCastGating.ShouldDispatchTasksAndWindows(handler, sheet)
      │   │     // Charge/CastTime/Channel/HoldRelease 通过 ShouldTrigger 闸门
      │   ├─ ActionWindow 派发 (HitFrame / ChargeHoldEnter|Exit / Sound / VFX)
      │   ├─ MotionExecutor.Tick(dt, timeScale, currentPos) → 期望速度推到 IMotorAdapter
      │   ├─ PlayerKCCMotor 执行 (CapsuleSweep + Collide&Slide + 9-pass 法向 sanitize)
      │   ├─ MotionExecutor.SyncPostMotorPosition(player.Position)
      │   └─ Tick 蓄力释放：玩家松手 → Player.TryNotifySkillCastInputReleasedForSlot
      │        → handler.OnInputReleased(ctx)；ChargeCastHandler 决出 Tap/Full/Cancel
      ├─ OnExit：
      │   ├─ MotionExecutor.End() (animSpeed 复位 1)
      │   ├─ Skill 收尾：CDPolicy(OnFirstCast/OnLastCast/OnSkillEnd) → runtime.StartCooldown
      │   ├─ SuppressLastCastCooldownThisExit 抑制中间段 CD
      │   └─ TaskExecutor.CancelAll / Complete
      └─ DamagePipeline (CombatContext + BaseDamage → DefenseReduction → Crit → FinalClamp)
            → DamageResult → DamageTextEmitStage → UI 浮字
```

---

## 3. 旁路与并行路由（非主管线，但同源于 InputReader）

主管线之外，InputReader 还会驱动以下 5 条独立路由，互不阻塞核心 Gameplay 状态机：

| 旁路 | 触发 | 数据走向 | 终点 |
|-----|------|---------|------|
| **Camera 视角** | OnLook (连续) | `InputReader.LookInput` → CameraController 每帧轮询 | Cinemachine / ActionCameraController |
| **暂停 / 菜单** | OnPause (performed) | `GlobalEventBus.Publish(PauseInputEvent)` | UI 系统 + `SetFocus(InputFocusMode.UI)` |
| **场景模式切换** | OnSwitchCamera | `GlobalEventBus.Publish(SwitchGameModeInputEvent)` | GameMode 切换器 |
| **拾取 / 对话** | OnInteract (performed) | `GlobalEventBus.Publish(InteractInputEvent)` | Interactable 监听者 |
| **Party 切换** | OnPartyNext/Prev/SlotN | `_partyNextPulse / _partySlotPulseIndex` | Party 系统轮询 ConsumePartyXxx |

并且 InputReader 暴露 3 个"焦点"模式给 UI/Stun 等场景：
- `InputFocusMode.Gameplay`：仅 GamePlay 图启用（默认）
- `InputFocusMode.UI`：仅 UI 图启用，清空 Gameplay 缓存
- `InputFocusMode.Mixed`：两图共开（战斗 HUD）

特殊状态：
- `DisableAllInput()` —— 全关 + 清缓存（眩晕、过场）
- `DisableGameplayExceptPartySwitch()` —— 阵亡：保留 Party*，关其它

---

## 4. 槽位 ↔ Action ↔ IntentKind 三角对照

InputReader 的物理回调 → Slot 表是单向；之后两张可逆映射在多处出现，必须对齐：

| InputAction 名 | SkillSlotType | GameplayIntentKind | ActionCategory (默认) | 优先级 |
|---|---|---|---|---|
| `Attack_01-03` | Skill_Primary_01 | LightAttack | Offense | 20 |
| (隐式 Combo 段) | Skill_Primary_02 | ComboAttack | Offense | 20 |
| (隐式 Charge 段) | Skill_Primary_03 | ChargeAttack | Offense | 20 |
| `SkillSlotSecondary_04` | Secondary_04 | HeavyAttack | Offense | 20 |
| `SlotUltimate_05` | Ultimate_05 | CastUltimate | Offense | 20 |
| `SlotAbility_06` | Ability_06 | CastAbility1 | Offense | 20 |
| `SlotAbility_07` | Ability_07 | CastAbility7 | **Movement** | 30 |
| `SlotAbility_08` | Ability_08 | CastAbility8 | **Movement** | 30 |
| `SlotAbility_09..17` | Ability_09..17 | Ability_09..17 | Offense | 20 |
| `Jump` | (无) | Jump | Movement | 30 |

**Primary 三态来源（非 InputAction 名）**：
左键单点 / 连点 / 长按由 `PrimaryAttackPressTracker` 在 `PlayerController.Update` 中以时间为轴细分：
- Tap (<= 0.18s) → `PlayerIntentCatalog.LightAttack(hold)`，可能升级为 `ComboAttack`（点击窗 0.28s 内连点）
- Hold ≥ ChargeConfig.tapThreshold → `ChargeAttack`（实际由 Skill `CastType.Charge` 接管，PressTracker 只负责松手时把 `PrimaryHoldDurationSeconds` 灌回意图）

---

## 5. 双层闸门与三段提交（关键正确性边界）

Ver 4.3.5 在仲裁层引入**延迟规划三段式**，避免「TryPrepareIntent 已耗资源、TransitionResolver 又拒绝」造成的回滚混乱：

```
BeginDeferredSkillPlanning  ──→  FinalizeDeferredSkillPlanning  ──→  AcknowledgeCommittedSkillConsumed
       (5.1 末)                       (5.3 之前)                         (5.3 之后)
        |                                |                                   |
        ↓ 失败                           ↓ 失败                              ↓ (Pop)
 CancelDeferredSkillPlanning   CancelDeferredSkillPlanning      —
                                  RevertCommittedSkillPlanningAfterFailedConsume
```

「双层闸门」：
- **全局闸门** = `TransitionResolver`（Ability/State 标签、过期）
- **局部闸门** = 状态自身：
  - `Locomotion` / `Airborne` 用 `ActionCategory` 整段掩码
  - `Action` 用 `ActionInterruptResolver.CanInterrupt(action, normalizedTime, intent, incomingAction)`
    - 在 ActionWindow 时间切片内查 `InterruptibleByCategories` + `MinIncomingPriority` + `AllowSelfInterrupt`
    - 硬打断：`incomingAction.InterruptPriority > action.InterruptStability`（跳窗口）

---

## 6. Cast 五态与 Charge 蓄力管线（与 Ver 4.5 动画三层耦合点）

`SkillCastHandlerFactory` 按 `SkillDataSO.castType` 装配处理器：

| CastType | Handler | OnInputPressed | OnUpdate | OnInputReleased | ShouldTrigger 语义 |
|----------|---------|----------------|----------|------------------|--------------------|
| Instant | InstantCastHandler | 无 | 无 | 无 | 恒 true |
| CastTime | CastTimeCastHandler | reset elapsed | 累计 | 早松 → cancelled | 读条已完成 |
| Channel | ChannelCastHandler | reset | 累计 | 早松 → releasedEarly | 仍在引导内 |
| Charge | **ChargeCastHandler** | IsHolding=true, ChargeStartTime=now | HoldSeconds+=dt; 越过 maxHoldTime → ForceRelease/Cancel | 决出 Tap/Charge/FullCharge | 蓄力中 false，松手后 true |
| HoldRelease | HoldReleaseCastHandler | Runtime.IsActive=true | 无 | _released=true | 按住中 true |

`ChargeCastHandler` 公开属性供 `PlayerActionState` 劫持表现：
- `HoldSeconds`、`ChargeProgress01`、`HasReachedThreshold`、`HasReachedFull`、`WasTap`、`WasCancelled`
- `DesiredAnimSpeed`：到达 chargeFullTime 后压到 `ChargeConfig.holdAnimSpeedAtFull`（0.05 ≈ 抖循环）
- 与 `MotionProfile.AnimSpeedMode` 通过 `IAnimSpeedControl.SetSpeed` 同链路写入，但 ChargeHandler 的请求**优先于** profileFactor。

松手回流路径：
```
PlayerController.Update → PrimaryAttackPressTracker.Tick
    → Player.TryNotifySkillCastInputReleasedForSlot(slot)
    → ctx.CastHandler.OnInputReleased(ctx)
    → ChargeCastHandler 决出结果：
        WasTap=true  → SkillChargeCommit 在 ResolveTapFallback 时已把 stage/action 切到 tapFallbackSlot
        WasCancelled → runtime.StartCancelCooldown(stats, cancelCdMode, cancelCdValue) + 退资源
        正常释放    → 收尾 ActionWindow 派发 HitFrame，伤害进 DamagePipeline
```

`SkillChargeCommit.TryApplyChargeOverride` 在 5.1 末，根据 `holdSeconds` 命中 `chargeLevels[]` 中 `minHoldTime ≤ hold` 的最大档，覆盖 `stage / action / damageMultiplier`；未命中则按 `damageMultiplierByProgress(progress01)` 走连续曲线。

---

## 7. 数据来源唯一性（SSOT）与禁止项

| 关注点 | 唯一数据源 |
|--------|------------|
| 键位绑定 | `.inputactions` 资产 + RebindManager（持久化 PlayerPrefs） |
| 槽位 ↔ Action 名 | `InputReader.TryGetSkillSlotActionNames` |
| 槽位 ↔ IntentKind | `IntentRouter.TryMapSlotToKind` + `SkillSystem.TryMapIntentToSlotDefault`（双向必须一致） |
| 槽位 ↔ Skill | `SkillLoadoutSO.bindings`（Resolve(slot)） |
| 槽位 ↔ Category/Priority | `ActionInterruptResolver.MapSkillSlotToCategory` |
| Intent 工厂 | `PlayerIntentCatalog.ForSlot / Jump / LightAttack / …` |
| 输入缓冲秒数 | `PlayerIntentCatalog`（每个 Kind 独立配置 bufferSeconds） |

**禁止**：
- 代码内 `new InputAction(...)` 临时创建绑定（破坏 Rebind）
- 在状态机内查表反推槽位（一律用 `intent.SkillSlot` 或 `TryResolveIntentSlot`）
- 主管线走 EventBus（事件只用于 UI / 模式切换等旁路）

---

## 8. 帧序合同（Execution Order Contract）

| Order | 组件 | 必须发生在前 |
|------|------|--------------|
| -100 | `ActionCameraController` (yaw 更新) | — |
| -50 | `PlayerController` (输入采样 + Intent 入队) | InputReader OnEnable 完成 |
| -20 | `PlayerStateManager.OnPreLogicUpdate` (仲裁/消费) | 当帧所有 Enqueue 完成 |
| -5 | `CinemachineBrain` | yaw 已更新 |
| 0 | 默认 Update | — |
| LateUpdate | Anim 同步、HUD 拉取 SkillRuntime 数据 | StateManager.Update 已 finish |
| FixedUpdate | KCC 步进（由 PlayerKCCMotor 内部驱动，与 Action 帧同步） | — |

`maxIntentConsumptionsPerFrame = 1`：每帧最多消费 1 条意图，防止「打断的打断」环路；过期意图由 `FlushExpired` 在 Tick 前清掉。

---

## 9. 故障定位速查

| 现象 | 第一嫌疑链路 | 排查 |
|------|--------------|------|
| 按键无反应 | 2→3→4 | Inspector `DebugInterruptFlow` 看是否入队；InputFocus 是否被 UI 抢占 |
| 入队但不释放 | 5.1 | `[IntentArb] BLOCK SkillSystem.CanCast/CD` (CD/资源/Stun/GCD) |
| CanCast 通过却拒切招 | 5.2 | `[IntentArb] BLOCK by TransitionResolver` + rejectReason |
| 切招后立刻被退回 | 5.3 | `[IntentArb] BLOCK by State gate` （多半 ActionWindow 不匹配） |
| 蓄力松手不响应 | 7.Charge | PrimaryAttackPressTracker.Tick 是否被打断；CastHandler 是否 InstantCastHandler（castType 配错） |
| 动画速率怪 | 7.Motion | v4.5 三层：`AnimSpeedMode` + Charge.DesiredAnimSpeed 谁后写 |
| 滑步 | MotionProfile | 切换 `AnimSpeedMode.StrideMatch`，调 `ReferenceSpeed` |
| 上下楼飘 | KCC | StepDown / Stair Band 阈值；Action 期禁 EdgeSlip |

---

## 10. 与历史版本兼容性摘要

| 字段 / 类型 | 状态 | 说明 |
|------------|------|------|
| `MotionProfileSO.MatchAnimationSpeed` | `[Obsolete]` | 由 `AnimSpeedMode` (Constant/Curve/StrideMatch) 替代 |
| `SkillDataSO.chargeThreshold` | `[Obsolete]` | `OnValidate` 自动迁移到 `charge.tapThreshold` |
| `GameplayIntentKind.UnusedLegacy6` | 保留占位 | 旧蓄力枚举值序列化对齐用，禁止入队 |
| InputAction 旧名 `SlotAbility1/Sprint/Dodge/SlotUltimate` | 保留 | InputReader 中显式转发到 06/07/08/05 |
| `PlayerActionState.ChargeMicroPhase` | **应清理** | 已由 `SkillCastHandlers.ChargeCastHandler` + `SkillChargeCommit` 接管 |

---

## 附录 A：核心类型清单（按层）

- **设备层**：`PlayerInputSystem` (auto-gen)、`PlayerInputSystem.IGamePlayActions`
- **翻译层**：`InputReader`、`InputFocusMode`、`RebindManager`
- **采样层**：`PlayerController`、`PrimaryAttackPressTracker`、`SecondaryInteractPressTracker`、`PlayerIntentCatalog`
- **意图层**：`GameplayIntent`、`GameplayIntentKind`、`GameplayIntentBuffer`、`SkillSlotType`
- **仲裁层**：`PlayerStateManager`、`TransitionResolver`、`ActionInterruptResolver`、`FrameContext`、`GameplayTagContainer`（5 轨：State/Status/Ability/Mechanic/Faction）
- **装配层**：`SkillSystem`、`SkillSegmentResolver`、`SkillChargeCommit`、`SkillCastHandlerFactory`、`SkillContext`、`SkillRuntime`、`SkillLoadoutSO`、`SkillDataSO`、`SkillStageSO`、`ChargeConfig`、`ChargeLevel`
- **路由层**：`IntentRouter`
- **执行层**：`PlayerActionState`、`TaskExecutor`、`AbilityTask`（Apply* / Spawn* / Wait* / Motion*）
- **运动层**：`MotionExecutor`、`MotionProfileSO`、`AnimSpeedMode`、`MotionGravityBehavior`、`IMotorAdapter`、`PlayerKCCMotor`、`KinematicMotorSolver`
- **表现层**：`PlayerAnimController`（Playables 3 通道）、`IAnimSpeedControl`
- **战损层**：`CombatContext`、`DamagePipeline`、`BaseDamageStage` → `DefenseReductionStage` → `CritStage` → `FinalClampStage` → `DamageTextEmitStage`

## 附录 B：术语缩写

- **SSOT**：Single Source of Truth，数据来源唯一性
- **GCD**：Global Cooldown，全局公共冷却（`SkillGcd`）
- **KCC**：Kinematic Character Controller，运动学角色控制器
- **CRTP**：Curiously Recurring Template Pattern（`EntityState<T>` / `EntityStateManager<T>`）
- **0-GC**：零垃圾回收分配（IntentBuffer 环形）

---

*文档版本：Ver 4.3.5（输入 → 技能响应主路由）；基线提交：当前工作树。*
*与 Ver 4.5 动画速率管线协同：见 `MotionExecutor.cs` Tick 内三层组合公式。*
