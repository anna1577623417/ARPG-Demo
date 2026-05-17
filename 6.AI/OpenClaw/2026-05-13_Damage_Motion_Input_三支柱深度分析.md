# Damage Pipeline × Motion System × Input System — 战斗手感三支柱深度分析

> **分析日期**：2026-05-13  
> **代码基线**：Ver 4.6+  
> **关联文档**：`2026-05-13_技能系统架构全景分析.md`（全景概览） / `2026-05-13_GameplayTags_Route_FSM_执行顺序_深度分析.md`（标签/路由/FSM）  
> **本文侧重**：伤害计算链路、程序化位移引擎、输入系统的完整数据流

---

## 目录

1. [Damage Pipeline — 伤害计算全链路](#1-damage-pipeline--伤害计算全链路)
2. [Motion System — 程序化位移引擎](#2-motion-system--程序化位移引擎)
3. [Input System — 从物理键到意图的完整转换链](#3-input-system--从物理键到意图的完整转换链)
4. [三系统交汇点 — 一帧内的协同时序](#4-三系统交汇点--一帧内的协同时序)

---

## 1. Damage Pipeline — 伤害计算全链路

### 1.1 架构概览

```
DamagePipeline.Compute(in CombatContext, in HitContext, stages?)
  └─ 默认 Stage 链 (4 段)：
      [0] BaseDamageStage       → 基础伤害 + 攻击力
      [1] DefenseReductionStage → 减去防御
      [2] CritStage             → 暴击乘算
      [3] FinalClampStage       → 结果钳位 (≥ 0)
  └─ 扩展 Stage (可选)：
      [4] DamageTextEmitStage   → 浮字表现层 (旁路，不改变 FinalDamage)
```

### 1.2 数据模型 — 全 readonly struct

整个链路用 **纯值类型** 传递——零堆分配、零副作用：

```cs
// 输入
public readonly struct HitContext {
    readonly float BaseDamage;        // 技能/武器的原始伤害
    readonly bool IsCritical;         // 是否暴击
    readonly float CriticalMultiplier; // 暴击倍率 (默认 1.5)
    readonly Vector3 HitPoint;        // 命中点 (供浮字定位)
}

public readonly struct CombatContext {
    readonly float AttackerAttackPower; // 攻击者攻击力
    readonly float DefenderDefense;     // 防御者防御力
    readonly float DefenderCurrentHP;   // 防御者当前血量
    readonly float DefenderMaxHP;       // 防御者最大血量
    readonly ulong AttackerTags;        // 攻击者标签
    readonly ulong DefenderTags;        // 防御者标签
}

// 输出
public readonly struct DamageResult {
    readonly float FinalDamage;    // 最终伤害值 (≥ 0)
    readonly bool IsCritical;      // 透传暴击标记
}
```

### 1.3 Stage 逐段分析

#### [0] BaseDamageStage — 基础伤害

```cs
float Apply(currentDamage, in ctx, in hit):
    base = hit.BaseDamage > 0f ? hit.BaseDamage : currentDamage;
    return max(0, base + ctx.AttackerAttackPower);
```

**设计意图**：伤害 = 技能基础伤害 + 攻击者攻击力。若 `hit.BaseDamage = 0`（比如 DOT/Buff 来源），则用上游传入的 `currentDamage` 继续。

#### [1] DefenseReductionStage — 防御减免

```cs
float Apply(currentDamage, in ctx, in hit):
    return max(0, currentDamage - ctx.DefenderDefense);
```

**设计意图**：当前伤害 - 防御者防御力（最简单的减法公式）。防御力来源：`Entity.m_statSet.Get(StatType.Defense)` + Buff 修正。

#### [2] CritStage — 暴击

```cs
float Apply(currentDamage, in ctx, in hit):
    if (!hit.IsCritical) return currentDamage;
    mul = hit.CriticalMultiplier > 0f ? hit.CriticalMultiplier : 1.5f;
    return max(0, currentDamage * mul);
```

**设计意图**：暴击 = 当前伤害 × 倍率。倍率来源：`SkillStageSO` / `SkillModifiers` / Buff 等。

#### [3] FinalClampStage — 最终钳位

```cs
float Apply(currentDamage, in ctx, in hit):
    return max(0, currentDamage);
```

**设计意图**：确保伤害永不为负（防御力不可能造成"治疗"）。

#### [4] DamageTextEmitStage — 浮字表现（旁路）

```cs
// 发布 DamageTextRequestedEvent → GlobalEventBus → DamageTextSystem
// 标记为 [旁路] — 不改变 finalDamage，不影响核心扣血逻辑
```

### 1.4 管线可插拔架构

```cs
// 全局默认 Stage 链
static readonly List<IDamageStage> s_defaultStages = new List<IDamageStage> { ... };

// 运行时替换（系统级注入，不需改 Compute 内部）
public static void ReplaceDefaultStages(IEnumerable<IDamageStage> stages);
public static void AddDefaultStage(IDamageStage stage);

// 每次调用也可传入自定义 stages（单个技能的临时 override）
public static DamageResult Compute(in CombatContext ctx, in HitContext hit,
    IReadOnlyList<IDamageStage> stages = null)
{
    var active = stages ?? s_defaultStages;
    for (i = 0; i < active.Count; i++)
        damage = active[i].Apply(damage, in ctx, in hit);
    return new DamageResult(damage, hit.IsCritical);
}
```

**策略模式优势**：
- 注入自定义 Stage （如元素反应、护盾穿透）不修改现有代码
- `IDamageStage` 接口仅一个方法签名，无框架依赖
- 每个 Stage 可独立单元测试

### 1.5 DamagePipeline 在技能管线中的调用点

```
PlayerActionState.OnUpdate:
  → ActionWindow 跨过 HitFrame 边界
    → TaskExecutor.BroadcastWindowSignal(ctx, HitFrame)
      → ApplyDamageTask.OnWindowSignal(ctx, HitFrame)
        → HitContext hit( base: skillStage.baseDamage × damageScale,
                          isCritical: RollCritRate(stats),
                          criticalMultiplier: critMul,
                          hitPoint: target.Position )
        → CombatContext ctx( attackerAttackPower, defenderDefense, currentHP, maxHP,
                             attackerTags, defenderTags )
        → DamageResult result = DamagePipeline.Compute(in ctx, in hit)
        → target.TakeDamage(result.FinalDamage, source)
          → Entity.TakeDamage(amount, source)
            → Drain HP → PublishHealthChanged → Publish EntityDiedEvent (if dead)
        → GlobalEventBus.Publish(new DamageTextRequestedEvent(result.FinalDamage, result.IsCritical, hitPoint))
          → DamageTextSystem.Spawn()
```

---

## 2. Motion System — 程序化位移引擎

### 2.1 架构概览

```
MotionExecutor (纯 C# 运行时代理)
├── 输入: MotionProfileSO + ActionDataSO.Duration + direction + startPos + baseAnimSpeed
├── 输出: SetDesiredVelocity(velocity) → IMotorAdapter → PlayerKCCMotor
├── 动画输出: SetSpeed(finalSpeed) → IAnimSpeedControl → PlayerAnimController
└── 状态维护: _startPos, _lastPos, _elapsed, _smoothedAnimSpeed
```

### 2.2 MotionProfileSO — 数据资产

```
MotionProfileSO : ScriptableObject
├── [Displacement]
│   ├── DisplacementCurve: AnimationCurve      // g(t), g(0)=0, g(1)=1 — 归一化位移
│   ├── BaseDistance: float                    // 总前向位移 (米)
│   └── PeakSpeedMultiplier: float             // 峰值速度乘数
├── [Lateral]
│   ├── LateralCurve: AnimationCurve?          // 侧向位移曲线
│   └── LateralDistance: float                 // 总侧向位移 (米)
├── [Animation Speed]
│   ├── AnimSpeedMode: Constant | Curve | StrideMatch
│   ├── SpeedOverTime: AnimationCurve?         // Curve 模式下的速率曲线
│   └── ReferenceSpeed: float                  // StrideMatch 的参考步伐速度
├── [Airborne Physics]
│   └── GravityBehavior: DefaultPhysics | Suspended
├── [Stat Scaling]
│   └── ScaleType: None | AttackSpeed | MovementSpeed | CastSpeed
├── [Warp]
│   ├── WarpCurve: AnimationCurve?             // 攻击吸附曲线
│   └── MaxWarpDistance: float
└── [Burst]
    ├── BurstDurationSeconds: float            // 爆发时长 (编辑用)
    ├── UsePlanarVelocityShape: bool
    ├── PlanarVelocityMultiplier: AnimationCurve
    └── PlanarPeakSpeed: float
```

### 2.3 位移计算公式

```
每帧计算:
  t = clamp01(_elapsed / _baseDuration)

  displacementRatio = DisplacementCurve(t)           // ∈ [0, 1]
  forwardDistance = BaseDistance × motionScale × PeakSpeedMultiplier
  forwardOffset = direction × forwardDistance × displacementRatio

  lateralRatio = LateralCurve(t)
  lateralOffset = lateralDir × LateralDistance × motionScale × lateralRatio

  warpOffset = direction × SampleWarp(t)

  targetPos = startPos + forwardOffset + lateralOffset + warpOffset
  delta = targetPos - lastPos

  // DefaultPhysics: 清零 Y 分量 (垂直交给重力 KCC)
  if (GravityBehavior == DefaultPhysics) delta.y = 0;

  desiredVelocity = delta / deltaTime
  → motor.SetDesiredVelocity(desiredVelocity)
```

### 2.4 三层动画速率组合公式 (v4.5)

这是全项目最精巧的数学组合——三层乘积让美术、策划、程序各自主导：

```
finalClipSpeed = ActionData.AnimSpeed     // ← 策划填的基础倍率
               × profileFactor           // ← MotionProfile 决定的动态因子
               × Charge.DesiredAnimSpeed  // ← 蓄力系统覆写 (蓄满压到 0.05)


profileFactor 由 AnimSpeedMode 决定:

  Constant   → factor = 1.0
    // 动画速率 = ActionData.AnimSpeed × 1.0
    // 适合: 站桩普攻、静止施法

  Curve      → factor = SpeedOverTime.Evaluate(t)
    // 动画速率 = ActionData.AnimSpeed × 曲线(t)
    // 适合: 戏剧化大招——慢起→爆发→定格

  StrideMatch → factor = clamp(actualSpeed / ReferenceSpeed, 0.7, 1.3)
    // 动画速率 = ActionData.AnimSpeed × 平滑后比值
    // 适合: 跑步/翻滚/突进——脚不打滑
    // 平滑: Mathf.Lerp(prevSmoothed/baseAnimSpeed, raw, 0.15)
```

**为什么 finalOverride 不是第三因子而是覆写？**

```cs
// Charge.DesiredAnimSpeed 在蓄满且按住时压到 holdAnimSpeedAtFull (e.g. 0.05)
if (_playback.HasAnimatorSpeedOverride)
    finalSpeed = _playback.AnimatorSpeedOverride;  // 直接覆盖，不走乘积

// 逻辑: 蓄力的"冻结"语义需要无视 BaseDistance / profileFactor 的含意，
//       用 MotionPlaybackContext 桥独立覆写，不参与乘积链。
```

### 2.5 LoopWindow — 蓄力循环采样

当 `ChargeHoldAnchorBehavior = LoopWindow` 时，MotionExecutor 在指定子区间循环：

```
[t = 0.6]  →  [t = 0.6]  →  [t = 0.6]  →  循环 ...
│              │              │
│  _elapsed 越过 LoopWindowEnd 时:
│  ├─ _elapsed = LoopWindowStart + remainder    // 卷回起点
│  ├─ _startPos += ComputeWindowDisplacement    // 补偿位移偏移
│  └─ _lastPos += ComputeWindowDisplacement     // 防止下帧产生逆向速度

这种设计保证了:
  - 位移在循环边界不跳变 (startPos 同步偏移)
  - 动画采样在同一区间反复 (归一化时间卷回)
  - 物理积分不受循环影响 (KCC 只读 desiredVelocity)
```

### 2.6 GravityBehavior — 重力与动作的协作

```
DefaultPhysics: 动作期间继续下落 (default)
  → MotionExecutor 清零 delta.y → 垂直方向完全交给 KCC 重力积分
  → 适合: 剑冲等地面/微腾空技能

Suspended: 动作期间挂起重力
  → Player.SuspendGravity() by ActionState.OnEnter
  → 空中连招期间垂直速度 = 0, y 不变
  → 退出时自动 ReleaseGravity()
  → 适合: 鬼泣式空中连段、滞空蓄力
```

### 2.7 MotionPlaybackContext — 外部覆写桥

```cs
public struct MotionPlaybackContext {
    public bool FreezeNormalizedAdvance;      // 冻结归一化时间推进
    public bool HasLoopWindow;                // 是否启用子区间循环
    public float LoopWindowStart;             // 循环起始 (归一化)
    public float LoopWindowEnd;               // 循环终点 (归一化)
    public bool HasAnimatorSpeedOverride;     // 是否有动画速率覆写
    public float AnimatorSpeedOverride;       // 覆写值 (Charge DesiredAnimSpeed)
}
```

**调用方**: ChargeRouteRuntimeBridge 在 PlayerActionState.OnUpdate 中通过 `_motionExecutor.SetPlaybackContext(...)` 注入蓄力状态。

---

## 3. Input System — 从物理键到意图的完整转换链

### 3.1 数据流总览

```
[物理层]     Unity Input System (.inputactions)
              ├─ Attack_01-03  (LMB)
              ├─ SkillSlotSecondary_04  (RMB)
              ├─ SlotAbility_06  (Q)
              ├─ SlotUltimate_05  (R)
              ├─ SlotAbility_07..17  (Shift/Space/1-9)
              ├─ Jump  (F)
              ├─ Move / Look / Interact / Pause / SwitchCamera
              └─ PartyNext / PartyPrev / PartySlot0-3

[翻译层]     InputReader (ScriptableObject)
              ├─ 连续量属性: MoveInput / LookInput / IsAttackHeld / IsInteractHeld
              ├─ 离散脉冲双写表:
              │   ├─ _slotPressedPulses[SkillSlotType] → 槽位脉冲 (Consume 后清零)
              │   ├─ _slotHeld[SkillSlotType] → 按住状态
              │   └─ _slotHeldStartTime[SkillSlotType] → 按住起始时间
              ├─ 焦点模式: Gameplay / UI / Mixed
              └─ 特殊禁用: DisableAllInput / DisableGameplayExceptPartySwitch

[采样层]     PlayerController.Update() (exec order -50)
              ├─ ConsumeDiscreteIntents → TryDispatchSlot × 17 个槽位
              ├─ PrimaryAttackPressTracker.Tick → 细分 Tap/Combo/Charge
              ├─ SecondaryInteractPressTracker.Tick → RMB Hold 追踪
              ├─ ResolveWorldDirection → cam-relative 移动向量
              └─ player.SetMovementIntent(worldDirection, wantsRun)
```

### 3.2 核心设计准则

| 准则 | 实现 |
|------|------|
| **只翻译，不决策** | InputReader 把物理信号转为结构字段（脉冲/连续/按住），绝不在回调里变化意图 |
| **双写兼容** | 旧 `_ability06PressedPulse` 字段与新的 `_slotPressedPulses[Ability_06]` 并存写入 |
| **焦点分层** | Gameplay/UI/Mixed 三模式，UI 焦点下清空 Gameplay 缓存防止误触 |
| **换绑无损** | RebindManager 改 `.inputactions` 绑定，不改槽位枚举——HUD 显示自动跟随 |
| **零 GC** | 所有输入消费通过值类型结构体 `GameplayIntent` 传递，无事件派发 |

### 3.3 PrimaryAttackPressTracker — LMB 三态细分

```
PrimaryAttackPressTracker (在 PlayerController.Update 中 Tick)

状态机:
  IDLE → PRESSED (LMB 按下)
  PRESSED → TAP (松开时 hold < tapThreshold=0.18s)
  PRESSED → COMBO_TAP (松开时 hold < tapThreshold, 且距上次松开 < comboWindow=0.28s)
  PRESSED → HOLD (hold >= tapThreshold, 开始蓄力)

输出:
  TAP → player.EnqueueGameplayIntent(PlayerIntentCatalog.LightAttack(holdDuration))
  COMBO_TAP → player.EnqueueGameplayIntent(PlayerIntentCatalog.ComboAttack(holdDuration))
  HOLD → 不立即入队！等待松手:
    ├─ 松手 → GameplayIntentKind.LightAttack (+ PrimaryHoldDurationSeconds)
    │         // SkillSystem 侧由 ChargeCommit 按 hold 时长决定覆盖
    └─ Charge tap threshold 内松手 → 视为 Tap
```

### 3.4 SecondaryInteractPressTracker — RMB 追踪

```
SecondaryInteractPressTracker
  追踪 RMB 按住时长 → 松手时:
    ├─ hold < tapThreshold (0.18s) → 轻点 → 暂未使用
    └─ hold >= tapThreshold → 写入 intent.SecondaryHoldDurationSeconds
```

### 3.5 TryDispatchSlot — 槽位通用派发

```cs
// 17 个槽位的统一派发入口
void TryDispatchSlot(SkillSlotType slot):
    ├─ !PlayerIntentCatalog.HasFactoryFor(slot) → return (不支持的槽位静默跳过)
    ├─ Route模式? inputReader.ConsumeSkillEntryPressed(entry, out hold)
    │   : inputReader.ConsumeSkillSlotPressed(slot, out hold)
    ├─ PlayerIntentCatalog.ForSkillEntry / ForSlot → intent
    └─ player.EnqueueGameplayIntent(intent)
```

**每次 Update 全部 17 槽位都被轮询一次（Check-and-Consume 模式）**——脉冲只存活一帧。

### 3.6 ConsumeSkillSlotPressed — 脉冲消费锁

```cs
// InputReader 内部
bool ConsumeSkillSlotPressed(SkillSlotType slot, out float holdSeconds):
    if (!_slotPressedPulses[slot]) return false;   // 无脉冲
    holdSeconds = Time.time - _slotHeldStartTime[slot];
    _slotPressedPulses[slot] = false;              // 清零脉冲 (消费一次)
    return true;
```

**设计保证**：每帧每个槽位最多产生一个意图。脉冲在 `TryDispatchSlot` 中被消费后清零，同帧内无人能再次消费。

### 3.7 InputReader 特殊状态 API

```
DisableAllInput():
  ├─ 清空全部脉冲/按住缓存
  ├─ 设置 _inputFocusMode = Mixed  // 至少保留 UI 层
  └─ 场景: 眩晕、过场、死亡

DisableGameplayExceptPartySwitch():
  ├─ 仅保留 PartyNext/PartyPrev/PartySlotN 的脉冲处理
  ├─ 清空其余所有 Gameplay 缓存
  └─ 场景: 阵亡后切换队员

SetFocus(InputFocusMode.UI):
  ├─ _focus = UI
  ├─ 清空 Gameplay 脉冲 (防止误触)
  └─ 场景: 打开菜单/背包
```

---

## 4. 三系统交汇点 — 一帧内的协同时序

### 4.1 伤害处理在一帧内的发生点

```
帧内时序:
  -50  PlayerController.Update → 意图入队
  -20  PlayerStateManager.OnPreLogicUpdate → 仲裁
   0  PlayerActionState.LogicUpdate → 归一化时间推进
       └─ ActionWindow 跨过 HitFrame 边界
          ├─ ApplyDamageTask.OnWindowSignal
          ├─ DamagePipeline.Compute
          ├─ target.TakeDamage
          │   └─ Entity.TakeDamage
          │       ├─ Drain HP
          │       ├─ PublishEntityHealthChanged (Local)
          │       └─ PublishEntityDiedEvent (if dead)
          └─ DamageTextRequestedEvent → GlobalEventBus  // 旁路
 LATE  LateUpdate
       ├─ HUD HP Bar 更新 (监听了 EntityHealthChanged)
       └─ DamageTextSystem → 生成/更新浮字

 FIXED FixedUpdate
       └─ KCC 物理步进 (与上述时间线同步)
```

### 4.2 Motion 在技能执行中的位置

```
PlayerActionState.OnEnter:
  ├─ MotionExecutor.Begin(profile, duration, direction, pos, animSpeed)
  └─ 若 GravityBehavior=Suspended → Player.SuspendGravity()

PlayerActionState.OnUpdate (每帧):
  ├─ MotionExecutor.Tick(dt, timeScale, position)
  │   ├─ 采样曲线 → desiredVelocity
  │   ├─ 计算三层动画速率 → SetSpeed
  │   └─ motor.SetDesiredVelocity(worldVelocity)
  │       └─ PlayerKCCMotor → 内部缓存 → FixedUpdate 物理步进
  └─ MotionExecutor.SyncPostMotorPosition(kcc.GetActualPosition())

PlayerActionState.OnExit:
  ├─ MotionExecutor.End()
  │   ├─ SetSpeed(1f) → 复位
  │   └─ SetDesiredVelocity(Vector3.zero)
  └─ 若 Suspended → Player.ReleaseGravity()
```

### 4.3 输入→Motion→Damage 的完整因果链

以"按下 Q 释放一个前冲刺技能"为例：

```
1. Input: Q → TryDispatchSlot(Ability_06) → Intent 入队
2. Arbiter: SkillSystem 解析 → Skill_06_Q 的 Charge Route?
            → 否 → 取 Normal RouteUnit.skillData
            → CanCast(CD/资源/Stun/GCD) → OK
3. Assembly: ResolveSegment → Stage[0]
            action = Stage[0].action → ActionDataSO(含 MotionProfile)
            tasks = Stage[0].tasks → [DashTask, ApplyDamageTask]
4. ActionState.OnEnter:
            MotionExecutor.Begin(profile, action.Duration, player.Forward, player.Pos, action.AnimSpeed)
            TaskExecutor.StartAll(tasks)
5. ActionState.OnUpdate (每帧):
            t=0.0..1.0 推进
            MotionExecutor.Tick → Velocity = BlastCurve(t) × BaseDistance / duration → KCC
            t=0.35: ActionWindow[HitFrame] 跨过
              → ApplyDamageTask.OnWindowSignal
              → DamagePipeline.Compute (BaseDamage + AttackPower - Defense) × Crit? → damage
              → target.TakeDamage(damage)
            t=1.0: 动作结束
6. ActionState.OnExit:
            MotionExecutor.End()
            CD 结算: StartCooldown (按 CooldownPolicy)
            Combo/Stage 推进
```

---

## 附录：DamagePipeline 扩展示例

如果要新增一个"元素反应"伤害 Stage：

```cs
public sealed class ElementalReactionStage : IDamageStage
{
    public float Apply(float currentDamage, in CombatContext ctx, in HitContext hit)
    {
        // 只有攻击者携带 Fire 标签且防御者携带 Wet 标签时触发 1.5×
        if ((ctx.AttackerTags & (ulong)StatusTag.Burning) != 0
            && (ctx.DefenderTags & (ulong)StatusTag.Wet) != 0)
        {
            return currentDamage * 1.5f;
        }
        return currentDamage;
    }
}

// 注入
DamagePipeline.AddDefaultStage(new ElementalReactionStage());
// 现在所有伤害计算都会经过元素反应 Stage
```

**关键设计**: TaskGraph 和 DamagePipeline 都通过接口实现运行时增删改，使整个战斗系统具有极强的扩展性。

---

*文档版本: 2026-05-13 (Damage × Motion × Input 深度分析)*  
*关联: `2026-05-13_技能系统架构全景分析.md` / `2026-05-13_GameplayTags_Route_FSM_执行顺序_深度分析.md`*
