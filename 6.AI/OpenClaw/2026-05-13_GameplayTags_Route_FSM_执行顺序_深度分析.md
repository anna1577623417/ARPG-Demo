# GameplayTags 5 轨 × Route 系统 × FSM 状态消费 × 帧序合同 — 子系统深度分析

> **分析日期**：2026-05-13  
> **代码基线**：Ver 4.6+  
> **关联文档**：`2026-05-13_技能系统架构全景分析.md`（全景概览，先读那篇）  
> **本文侧重**：四个横向子系统的独立深度分析——标签系统、Route 管线、FSM 意图消费、帧序保证

---

## 目录

1. [GameplayTags 5 轨标签系统](#1-gameplaytags-5-轨标签系统)
2. [Route 系统深度解析 (Ver 4.6+)](#2-route-系统深度解析-ver-46)
3. [FSM 架构与意图消费模式](#3-fsm-架构与意图消费模式)
4. [执行顺序合同 (Execution Order Contract)](#4-执行顺序合同-execution-order-contract)
5. [附录：PlayerActionState 完整生命周期](#附录-playeractionstate-完整生命周期)

---

## 1. GameplayTags 5 轨标签系统

### 1.1 设计动机

本项目的标签系统是最核心的**零 GC 仲裁基础设施**。所有技能资格判断、打断合法性、状态过滤都通过位掩码完成，无字符串比较、无字典查找。

### 1.2 五轨容器

```
GameplayTagContainer (struct → 零堆分配)
├── State:     GameplayTagMask   → StateTag (ulong)         // "正在做什么"
├── Status:    GameplayTagMask   → StatusTag (ulong)        // "被施加了什么"
├── Ability:   GameplayTagMask   → EntityCapabilityTag (ulong)  // "能不能做"
├── Mechanic:  GameplayTagMask   → MechanicTag (ulong)      // "机制限制"
└── Faction:   GameplayTagMask   → FactionTag (ulong)       // "阵营属性"
```

**关键设计约束**：

- `Event` 轨不入容器——战斗事件由 `CombatEventTag` + EventBus 瞬时携带，不持久化
- 调用方必须通过 `ref` 持有容器（如 `Player.Tags` 的 ref 属性），避免对 struct 值拷贝后改掩码
- 所有查询通过 `TagCategory` 枚举分发 → switch 内联展开 → O(1) 位运算

### 1.3 State 轨 — 行为状态 (StateTag: ulong)

State 轨应答「这个实体**当前正在做什么**？」——由 FSM 状态写入，由仲裁器只读查询。

```
StateTag 位分区 (64 位):
┌──────────────────┬──────────────────┬──────────────────┬──────────────────┬──────────────────┐
│  bit 0–15        │  bit 16–31       │  bit 32–39       │  bit 40–45       │  bit 46–63       │
│  Physical        │  Phase           │  Legacy / Meta   │  AllowInterrupt* │  Reserved        │
│  物理/空间姿态   │  动作阶段        │  遗留能力/元数据 │  打断许可语义   │  Dead / 预留     │
└──────────────────┴──────────────────┴──────────────────┴──────────────────┴──────────────────┘
```

**物理姿态 (0–15)**：

| 位 | 标签 | 写入者 | 仲裁影响 |
|----|------|--------|---------|
| 0 | `Grounded` | LocomotionState.OnEnter | Jump 意图要求此位 (RequiredAll) |
| 1 | `Airborne` | AirborneState.OnEnter | 着地检测 → 回 Locomotion |
| 2 | `Climbing` | (预留) | — |
| 3 | `Swimming` | (预留) | — |

**动作阶段 (16–31)**：

| 位 | 标签 | 写入者 | 仲裁影响 |
|----|------|--------|---------|
| 16 | `PhaseStartup` | ActionState + `ActionDataSO.EvaluatePhaseTags()` | — |
| 17 | `PhaseActive` | 同上 | — |
| 18 | `PhaseRecovery` | 同上 | — |
| 19 | `Stunned` | Buff/外部 | SkillRuntime.CanCast 检查此位 → 拒绝施法 |
| 20 | `HitboxActive_Window` | ActionWindow 时间切片 `EvaluatePhaseTags` | — |
| 21 | `RootMotion_Window` | ActionWindow 时间切片 | — |

**打断许可 (40–45)** — 由 `ActionWindow.WindowSlotMask` 写入，`ActionInterruptResolver` 读取：

```
AllowInterruptByDodge      = 1 << 40   // 翻滚可打断此窗口
AllowInterruptBySwordDash  = 1 << 41   // 剑冲可打断此窗口
AllowInterruptByLight      = 1 << 42   // 轻击可打断此窗口
AllowInterruptByHeavy      = 1 << 43   // 重击可打断此窗口
AllowInterruptByCharged    = 1 << 44   // 蓄力可打断此窗口
AllowInterruptByJump       = 1 << 45   // 跳跃可打断此窗口
```

**重要**: 遗留 `Can*` 标签 (bit 32–38) 已标记 `[Obsolete]`，正迁移到 Ability 轨的 `EntityCapabilityTag`。

### 1.4 Ability 轨 — 实体能做什么 (EntityCapabilityTag: ulong)

```
EntityCapabilityTag (ulong, 写入 GameplayTagContainer.Ability):
  CanJump             = 1 << 0
  CanDodge            = 1 << 1
  CanLightAttack      = 1 << 2
  CanHeavyAttack      = 1 << 3
  CanCancelToLocomotion = 1 << 4
  CanSwordDash        = 1 << 5
  CanCastAbility1     = 1 << 6    // Ability_06 槽
  CanCastUltimate     = 1 << 7    // Ultimate_05 槽
```

**写入者**：Player 每帧在 `BuildFrameContext` 前按姿态、冷却、资源重建：

```
地面 + 非攻击状态 → CanLightAttack | CanHeavyAttack | CanJump | CanDodge | CanSwordDash | …
空中 + 上升       → 可能只保留 CanSwordDash
眩晕              → 清空全部
```

**消费者**：`TransitionResolver.CanOfferIntent` 中的 Ability 禁止位/必需位检查。

### 1.5 GameplayTagMask — 位掩码操作

```cs
public struct GameplayTagMask {
    ulong _bits;
    public void Add(ulong bits)      { _bits |= bits; }
    public void Remove(ulong bits)   { _bits &= ~bits; }
    public bool HasAll(ulong bits)   { return (_bits & bits) == bits; }
    public bool HasAny(ulong bits)   { return (_bits & bits) != 0; }
}
```

**关键 API（TagCategory 分发）**：

```
container.HasAny(TagCategory.Status, (ulong)StatusTag.Stun)
  → SkillRuntime.CanCast 以此检查眩晕禁止

container.HasAll(TagCategory.Ability, (ulong)EntityCapabilityTag.CanJump)
  → TransitionResolver 以此验证必需能力

container.Add(TagCategory.State, (ulong)StateTag.Airborne)
  → AirborneState.OnEnter 写入
```

### 1.6 标签与仲裁接口的完整交互

```
PlayerStateManager.OnPreLogicUpdate:
  ├─ Entity.TickSkillRuntimes(dt)            // CD/Combo/二段窗 推进
  │   └─ runtime.CanCast → 检查 tags.HasAny(Status, Stun)  // 眩晕否决！
  ├─ IntentBuffer.FlushExpired(now)
  ├─ TryPeek → BuildFrameContext
  │   └─ FrameContext.CurrentTags = Player.Tags.State       // State 轨快照
  │   └─ FrameContext.CurrentAbilityTags = Player.Tags.Ability  // Ability 轨快照
  ├─ SkillSystem.TryPrepareIntentForSkills                     // 技能资格
  ├─ TransitionResolver.CanOfferIntent(in ctx, in intent)      // 标签闸门
  │   ├─ (currentTags & forbiddenTags) != 0          → 拒绝
  │   ├─ (abilityTags & forbiddenAbilityTags) != 0   → 拒绝
  │   ├─ (currentTags & requiredAllTags) != requiredAllTags → 拒绝
  │   └─ (abilityTags & requiredAllAbilityTags) != requiredAllAbilityTags → 拒绝
  └─ Current.TryConsumeGameplayIntent                          // 状态本地闸门
```

---

## 2. Route 系统深度解析 (Ver 4.6+)

### 2.1 设计动机

传统技能系统：**按键名 → 意图名 → 技能**。Route 系统改为：**入口槽 → RouteUnit[条件匹配] → SkillDataSO**。

同一物理按键在不同状态（Normal / Combo / Charge）下可路由到不同技能资产。

### 2.2 数据结构

```
SkillEntryRow                   ← 一个物理入口（如 LMB）
├── entrySlot: SkillEntrySlot   ← Skill_01_LM
├── hudKeyLabel: "LMB"
└── units: SkillRouteUnit[]     ← 该入口下的所有路由变体
    ├── [0] Normal  → SkillDataSO (普攻第一段)
    ├── [1] Combo   → SkillDataSO (普攻第二段)
    └── [2] Charge  → SkillDataSO (蓄力重击)

SkillRouteUnit                  ← 单条路由变体
├── routeId: string             ← 标识符
├── icon: Sprite
├── showOnHud: bool             ← 是否显示在 UI 技能栏
├── skillData: SkillDataSO      ← 目标技能资产
├── presentationKind: Normal | Combo | Charge
└── conditions: SkillRouteCondition[]?
    └── { type: Normal/Combo/Charge, comboIndex?, minHoldTime?, operator? }
```

### 2.3 RouteResolver — 匹配算法

```cs
RouteResolver.Resolve(row, in InputContext, skillRuntime, out reason):
  // 优先顺序: Charge > Combo > Normal

  // Step 1: 查找 Charge 单元
  遍历 units:
    若 unit.presentationKind == Charge || unit.skillData.castType == Charge:
      若 ctx.SkillHoldSeconds >= unit.skillData.charge.tapThreshold:
        → reason = Charge, return unit

  // Step 2: 查找 Combo 单元
  若 skillRuntime 连招窗未过期:
    遍历 units:
      若 unit.presentationKind == Combo:
        → reason = Combo, return unit

  // Step 3: 查找 Normal 单元
  遍历 units:
    若 unit.presentationKind == Normal || default:
      → reason = Normal, return unit

  // Step 4: 回退
  return units[0]  // reason = FallbackPrimary
```

**RouteResolveReason** (决策追踪，供调试/HUD)：

```
Normal = 0          → 默认普攻段
Combo = 1           → 连招窗内命中 Combo 路由
Charge = 2          → 按住达阈值命中 Charge 路由
FallbackPrimary = 3 → 回退到首个 unit
```

### 2.4 Route 运行时 — 三层并存的 CD 状态

Ver 4.6+ 阶段，Route Runtime 与旧 `SkillRuntime` 字典**并存**（未接管 CD 状态机）：

```
Player 内部状态:
├── m_skillRuntimes: Dictionary<SkillSlotType, SkillRuntime>  ← 旧 CD 权威源 (Phase 5)
├── m_routeService: SkillRouteService                          ← 新 HUD 句柄宿主 (Phase 6)
│   └── m_hudHandles: List<IRouteRuntimeHandle>               ← HUD 绑定列表
└── m_routeService.Rebuild(loadout, owner)                    ← Loadout 切换时重建
```

**Route 运行时族谱**：

```
SkillRouteRuntime (abstract)
├── NormalRouteRuntime          ← 占位 (Phase1 空壳)
│   ├── Unit: SkillRouteUnit
│   └── EntrySlot: SkillEntrySlot
├── ChargeRouteRuntime          ← 蓄力计时/CD
│   └── Bridge → ChargeRouteRuntimeBridge  (PlayerActionState 劫持)
└── MultiStageRouteRuntime      ← 多段推进
```

### 2.5 ChargeRouteRuntimeBridge — 蓄力完整状态桥

`ChargeRouteRuntimeBridge` 是蓄力从"Skill 侧配置"到"Action 侧执行"的桥梁：

```
ChargeRouteRuntimeBridge (在 PlayerActionState 内实例化)
├── 持有 ChargeCastHandler 引用
├── 管理 HoldLoopWindow 循环采样状态
├── 推迟 Animator 速率覆写 → MotionPlaybackContext.AnimatorSpeedOverride
└── 驱动 ChargeHoldEnter/Exit ActionWindow 事件
```

### 2.6 Route 与 HUD 的绑定链

```
SkillLoadoutSO.entries[]
  → SkillEntryRow.units[] (showOnHud=true 的)
    → SkillRouteService.HudHandles (IReadOnlyList<IRouteRuntimeHandle>)
      → SkillBarRoutePresenter (UI 层)
        → SkillSlotView / SkillCooldownTicker / SkillChargeTicker
```

HUD 不再认 "LightAttack" 字符串，只认 `Slot + RouteUnit`——彻底解耦输入命名与显示。

---

## 3. FSM 架构与意图消费模式

### 3.1 框架层 FSM — 纯 C# 通用状态机

```
StateMachine<TOwner>
├── _states: List<State<TOwner>>          // 顺序列表
├── _stateMap: Dictionary<Type, State<TOwner>>  // 类型 → 实例 (O(1) 查找)
├── Current / Previous: State<TOwner>
├── Initialize(owner, states)             // 注入宿主 + 状态列表
├── Start() → states[0].Enter(owner)
├── Change(index / <TState>() / State)    // 三入口汇聚到核心 Change(State)
│   ├─ Current?.Exit(owner)
│   ├─ Previous = Current
│   ├─ Current = to
│   └─ Current.Enter(owner)
├── ForceChange(State)                    // 同状态也 Exit/Enter (Action 打断重入)
├── LogicUpdate(dt) / PhysicsUpdate(fixedDt)
└── IsCurrentOfType<T>() / GetState<T>() / ContainsState<T>()
```

**设计亮点**：
- **纯 C#**：不依赖 `UnityEngine`，可迁移到服务端
- **deltaTime 外部传入**：不读 `Time.deltaTime`，状态机的时钟由调用方决定
- **ForceChange**：同状态打断时也能触发 OnExit/OnEnter（如 ActionState 内换招）
- **漏斗式 Change**：3 个入口（索引/类型/实例）→ 1 个核心实现

### 3.2 Player 四支柱 FSM

```
[0] PlayerLocomotionState (默认)
     ↓ Change<PlayerActionState>
[2] PlayerActionState (万能动作)
     ↓ Change<PlayerAirborneState> (跳跃)
[1] PlayerAirborneState
     ↓ 着地 → Change<PlayerLocomotionState>
[3] PlayerDeadState (终态)
```

### 3.3 三类意图消费模式对比

#### 模式 A：连续状态（Locomotion）— 整段类别掩码

```cs
// PlayerLocomotionState.TryConsumeGameplayIntent
var incomingCategory = ActionInterruptResolver.ResolveIncomingCategory(in intent, incomingAction);
if ((m_allowedCategories & incomingCategory) == 0) return false;
return IntentRouter.Route(player, in intent, forceActionReentry: false);
```

**特点**：无归一化时间概念，只要类别匹配即切到 ActionState。

#### 模式 B：分段连续状态（Airborne）— 按物理相位切掩码

```cs
// PlayerAirborneState.TryConsumeGameplayIntent
var phase = player.VerticalSpeed > 0f ? "Ascending" : "Descending";
// 上升: 默认不允许 (与旧版一致)
// 下降: 默认四类全开 (空中可出招)
var allowed = GetCurrentPhaseAllowedCategories(player);
if ((allowed & incomingCategory) == 0) return false;
return IntentRouter.Route(player, in intent, forceActionReentry: false);
```

**特点**：同一状态内按 VerticalSpeed 动态切换允许的类别。

#### 模式 C：动作状态（Action）— 归一化时间窗口

```cs
// PlayerActionState.TryConsumeGameplayIntent
var normalized = ResolveCurrentActionNormalized(player);
var incomingAction = IntentRouter.PeekActionDataForRouting(player, in intent);
if (!ActionInterruptResolver.CanInterrupt(m_action, normalized, in intent, incomingAction))
    return false;
return IntentRouter.Route(player, in intent, forceActionReentry: true);
// ↑ Action 内打断始终 ForceChange (重走 OnEnter/OnExit)
```

**特点**：在 ActionWindow 时间窗内做类别/优先级/自打断检测；硬打断直接跳窗口。

### 3.4 PlayerStateManager 仲裁主循环

```
OnPreLogicUpdate (exec order -20):
  FOR i = 0..maxIntentConsumptionsPerFrame (默认 1):
    ├─ TryPeek 队首
    ├─ BuildFrameContext(dt) → 快照 State/Ability/Stamina 等
    ├─ [5.1] SkillSystem.TryPrepareIntentForSkills
    │   └─ 失败 → break (不消费，等待下帧)
    ├─ IntentBuffer.ReplaceFront (写回 intent.Action)
    ├─ [5.2] TransitionResolver.CanOfferIntent
    │   └─ 失败 → CancelDeferredSkillPlanning; break
    ├─ FinalizeDeferredSkillPlanning
    ├─ [5.3] Current.TryConsumeGameplayIntent
    │   └─ 失败 → RevertCommittedSkillPlanningAfterFailedConsume; break
    └─ AcknowledgeCommittedSkillConsumed; Pop
```

**每帧最多消费 1 条意图**——防止「A 打断 B、C 打断 A」的无限打断链。

---

## 4. 执行顺序合同 (Execution Order Contract)

### 4.1 完整帧序表

帧序是技能系统正确性的**硬合同**——任何违反都会导致仲裁前意图未入队、或位移先于 Action。

```
┌─────────────────────────────────────────────────────────────────┐
│                    一帧内的执行顺序                              │
├──────┬──────────────────────────────────────────────────────────┤
│ 阶   │ 组件 (ExecutionOrder)                                    │
├──────┼──────────────────────────────────────────────────────────┤
│ -120 │ SystemRoot.Awake                                         │
│      │   ◆ 注册 ServiceRegistry (GameModeManager + IGameModeMovementContext) │
│      │   ◆ PushMovementContextToScenePlayers (注入移动上下文)    │
├──────┼──────────────────────────────────────────────────────────┤
│ -100 │ ActionCameraController (yaw 更新)                        │
│      │   ◆ 确保后续移动采样用当帧相机朝向                        │
├──────┼──────────────────────────────────────────────────────────┤
│  -50 │ PlayerController.Update                                  │
│      │   ◆ ConsumeDiscreteIntents → 全部槽位脉冲入队             │
│      │   ◆ Tick PressTrackers (LightAttack/Combo/Charge 分流)   │
│      │   ◆ ResolveWorldDirection + ResolveRunIntent             │
│      │   ◆ player.SetMovementIntent                             │
├──────┼──────────────────────────────────────────────────────────┤
│  -20 │ PlayerStateManager.OnPreLogicUpdate                      │
│      │   ◆ TickSkillRuntimes (CD/Combo/二段窗)                  │
│      │   ◆ FlushExpired                                         │
│      │   ◆ 仲裁主循环 (5.1 → 5.2 → 5.3)                        │
├──────┼──────────────────────────────────────────────────────────┤
│   -5 │ CinemachineBrain (相机矩阵已更新)                        │
├──────┼──────────────────────────────────────────────────────────┤
│    0 │ 默认 Update                                              │
│      │   ◆ Entity.Update → LateUpdate 前逻辑                    │
│      │   ◆ PlayerKCCMotor 内部步进 (由 Action 驱动)             │
├──────┼──────────────────────────────────────────────────────────┤
│  LATE │ LateUpdate                                              │
│      │   ◆ Entity.LateUpdate → 计算 Velocity/Position 差量      │
│      │   ◆ BuffStack.Tick(dt)                                   │
│      │   ◆ Anim 同步 (PlayerAnimController)                     │
│      │   ◆ HUD 拉取 SkillRuntime 数据 (CD/Charge/Hold 进度)     │
│      │   ◆ DamageTextSystem 刷新浮字池                          │
├──────┼──────────────────────────────────────────────────────────┤
│ FIXED │ FixedUpdate                                             │
│      │   ◆ PlayerKCCMotor.Step (物理步进: CapsuleSweep +        │
│      │     Collide&Slide + 9-pass 法向 sanitize)                │
│      │   ◆ 内部 MotorSolveContext 上下文判断 (Locomotion /      │
│      │     Airborne / Action)                                   │
└──────┴──────────────────────────────────────────────────────────┘
```

### 4.2 帧序合同的核心保证

| 保证 | 实现机制 | 违反后果 |
|------|---------|---------|
| 仲裁前意图全部入队 | `PlayerController.Update` (-50) 先于 `StateManager.OnPreLogicUpdate` (-20) | 意图延迟一帧才被消费 → 手感变"粘" |
| 相机朝向先于移动采样 | `CameraController` (-100) 先于 `PlayerController` (-50) 的 `ResolveWorldDirection` | 角色朝向偏移 → "镜头一转人就歪" |
| CD/Combo 在仲裁前推进 | `TickSkillRuntimes` 在仲裁循环前调用 | 刚转好的 CD 仍被拒绝 → 卡技能 |
| HUD 在逻辑帧后读取 | LateUpdate 在 Update 之后 | CD 进度条显示滞后一帧 |
| 移动上下文在 Awake 注入 | `SystemRoot` (-120) 早于任何 Controller | Controller.NPE 或 fallback Camera.main |

### 4.3 maxIntentConsumptionsPerFrame = 1 的设计理由

```
为什么是 1？
  ├─ 防止「A 打断 B、C 打断 A」的无限打断链
  ├─ 避免同一帧内多个技能 AI 仲裁互相覆盖
  └─ 简化 SkillContext 的 Borrow/Return 资源管理 (无需池化)
```

### 4.4 GameBootstrapper 初始化链

```
GameBootstrapper.Awake (MonoSingleton):
  ├─ CollectModules():
  │   ├─ modules 字段显式列表 (优先)
  │   └─ autoFindSceneModules? → FindObjectsByType<MonoBehaviour>(IncludeInactive)
  │       筛选 IGameModule 实例, 去重
  └─ 遍历调用 module.Init()
```

**风险**: `autoFindSceneModules = true` 时 `FindObjectsByType` 遍历所有场景 MonoBehaviour → O(n) 且初始化顺序不确定。

---

## 附录：PlayerActionState 完整生命周期

### OnEnter

```
PlayerActionState.OnEnter(player):
├─ 读取 player.PendingAction (由 IntentRouter 挂载)
├─ 读取 player.ActiveSkillContext (由 FinalizeDeferredSkillPlanning 提交)
├─ 读取 motionProfile (从 ActionDataSO.MotionProfile)
├─ 标签标记:
│   ├─ Tags.State.Add(Grounded | PhaseStartup | PhaseActive)
│   ├─ Tags.Status.Add(…)
│   └─ Tags.Ability.Add(CancellableFromAction 等)
├─ MotionExecutor.Begin(motionProfile, duration, burstFaceDir, startPos, baseAnimSpeed)
│   ├─ 内部构建 MotionPlaybackContext
│   │   ├─ startPos / duration / burstDir / animSpeed
│   │   └─ HasLoopWindow → ChargeHoldAnchorBehavior=LoopWindow 时启用
│   └─ 注册 IMotorAdapter → PlayerMotorAdapter
├─ TaskExecutor.StartAll(stage.tasks, ctx)
├─ 资源扣除: Skills.TryBeginSkillCostsIfNeeded(ctx)  // 起手扣费
│   └─ 遍历 costs[] → pool.Drain(resourceType, amount)
├─ 起手窗口派发: OnWindow(HitFrame 等 actionStart 事件)
├─ Charge 桥就绪: m_chargeBridge.Begin(castHandler, motionProfile, …)
└─ 离散瞬移: TeleportTriggers 中 t≈0 的触发
```

### OnUpdate

```
PlayerActionState.LogicUpdate(player):
├─ 归一化时间推进:
│   normalizedTime = m_normBaselineOffset + (TimeSinceEntered - m_normBaselineOffset)/resolvedDuration
├─ ctx.CastHandler.OnUpdate(ctx, dt)  // 推进 CastHandler 状态机
├─ SkillCastGating.ShouldDispatchTasksAndWindows(handler, sheet)
│   └─ false (Charge 蓄力中 / CastTime 读条中) → 跳过
├─ ActionWindow 派发:
│   ├─ EvaluatePhaseTags(normalizedTime, ref mask)
│   │   └─ 遍历 Windows[] → 命中时间切片 → 叠加 WindowSlotMask 位
│   ├─ 跨过窗口边界 → 触发 RuntimeEvent (HitFrame / Sound / VFX / ChargeHoldEnter/Exit)
│   ├─ TaskExecutor.BroadcastWindowSignal(ctx, kind)
│   └─ m_chargeBridge.OnWindow(kind)  // ChargeBridge 处理锚点事件
├─ MotionExecutor.Tick(dt, timeScale, position)
│   ├─ 采样 DisplacementCurve(normalizedTime) → 平面速度
│   ├─ 采样 LateralCurve(normalizedTime) → 侧向速度
│   ├─ 三层动画速率: baseAnimSpeed × profileFactor × Charge.desiredAnimSpeed
│   │   写入 MotionPlaybackContext.AnimatorSpeedOverride
│   └─ → IMotorAdapter.ApplyVelocity(worldVelocity)
├─ TeleportTriggers 离散瞬移 (单帧)
├─ PlayerKCCMotor → 物理积分
└─ 是否结束?
    ├─ normalizedTime >= 1f && (CastHandler.IsComplete || !shouldWaitForCast)
    ├─ CastHandler.IsComplete 提前退出
    └─ 结束 → End()
```

### OnExit

```
PlayerActionState.OnExit(player):
├─ MotionExecutor.End()
│   ├─ animSpeed 复位 1
│   ├─ 注销 IMotorAdapter
│   └─ 释放重力挂起 (若 Suspended)
├─ 标签清理:
│   ├─ Tags.State.Remove(PhaseStartup | PhaseActive | PhaseRecovery)
│   └─ Tags.State.Remove(ActionWindow 叠加的打断许可位)
├─ CD 结算 (按 CooldownPolicy):
│   ├─ OnFirstCast / OnLastCast → runtime.StartCooldown(stats)
│   ├─ OnSkillEnd → (由外部调用)
│   └─ SuppressLastCastCooldownThisExit → 中间段跳过
├─ Stage 推进:
│   ├─ HasNextStage? → runtime.AdvanceStage() + OpenSecondStageWindow()
│   └─ 否则 → ResetStage()
├─ Combo 推进:
│   ├─ 攻击类技能 (IsAttackComboCleanExit) → runtime.AdvanceCombo()
│   ├─ 过期 → ResetCombo()
│   └─ 非攻击类 → ResetCombo()
├─ TaskExecutor.ExitAll(ctx)
├─ Charge 桥清理: m_chargeBridge.End()
├─ 技能上下文回收: player.ReturnSkillContext()
├─ 回 Locomotion (默认) / 或保持 Airborne (若动作在空发)
└─ 事件发布:
    ├─ PublishEvent(PlayerAttackEndedEvent) (若 IsAttacking)
    ├─ PublishEvent(ActionCompletedEvent)
    └─ PublishEvent(PlayerActionEndedLocalEvent)
```

### 硬打断 vs 正常退出的差异

| 方面 | 正常退出 (normalizedTime >= 1) | 硬打断 (ActionInterruptResolver 通过) |
|------|-------------------------------|--------------------------------------|
| FSM 调用 | Change → Exit → Enter | ForceChange → Exit → Enter (同状态) |
| CD 结算 | 按 CooldownPolicy 正常结算 | OnFirstCast 时已结算 + OnExit 不再结算中间段 |
| Stage 推进 | AdvanceCombo + OpenSecondStageWindow | ResetCombo (中断连招链) |
| 资源退款 | 无 | Charge 取消时按 refundResourceOnCancel 退款 |
| 事件发布 | ActionCompletedEvent | ActionCancelledEvent |

---

*文档版本: 2026-05-13 (子系统深度分析)*  
*关联: `2026-05-13_技能系统架构全景分析.md`*
