# 技能系统配置全指南（中英双语）

> 适用范围：当前工程 `SkillDataSO + SkillStageSO + SkillRuntime + SkillSystem + PlayerActionState + InputReader/Rebind/HUD` 这条链路。  
> Scope: This guide targets the current pipeline built on `SkillDataSO + SkillStageSO + SkillRuntime + SkillSystem + PlayerActionState + InputReader/Rebind/HUD`.

---

## 1) 系统总览 / System Overview

- **输入层 Input Layer**：`InputReader` 把物理键映射为 `SkillSlotType` 脉冲，`PlayerController` 消费后转为 `GameplayIntent`。
- **路由层 Routing Layer**：`SkillSystem.TryPrepareIntentForSkills` 把 `Intent -> Slot -> SkillRuntime/SkillData -> ActionData`。
- **执行层 Execution Layer**：`PlayerActionState` 进入 Action 后驱动 `TaskExecutor`、窗口事件、Cast 五态（Instant/CastTime/Channel/Charge/HoldRelease）。
- **表现层 Presentation Layer**：`SkillBarPresenter + SkillSlotView + SkillCooldownTicker` 显示图标/CD/按键；按键显示由 `InputReader.GetSkillSlotBindingDisplayString(slot)` 动态查询。

---

## 2) 关键资产关系 / Core Asset Relationship

- `SkillLoadoutSO`：定义“哪个 `SkillSlotType` 装哪个 `SkillDataSO`”。
- `SkillDataSO`：技能根模板（施法模型、CD、消耗、目标、连招链、成长）。
- `SkillStageSO`：某一段执行（Action + Task + 过渡 + 检测）。
- `ActionDataSO`：动作时间轴（窗口、瞬移触发、动画/运动资料）。
- `AbilityTaskSO`：可组合任务模块（伤害、效果、位移、投射物、召唤、等窗、改 CD 等）。

---

## 3) 全参数指南（中英双语）/ Full Parameter Reference (CN+EN)

## 3.1 `SkillDataSO`（核心技能模板）

- `skillName`：技能显示名。  
  EN: Display name of the skill.
- `skillId`：技能唯一逻辑 ID（建议唯一、稳定）。  
  EN: Stable unique logical ID for save/network/analytics.
- `icon`：HUD 图标。  
  EN: HUD icon sprite.

- `castType`：施法模型（`Instant/CastTime/Channel/Charge/HoldRelease`）。  
  EN: Cast protocol type controlling input and trigger behavior.
- `castTime`：读条时长（仅 `CastTime` 生效）。  
  EN: Cast bar duration for `CastTime`.
- `channelDuration`：引导总时长（仅 `Channel` 生效）。  
  EN: Total channel duration for `Channel`.
- `chargeThreshold`：蓄力阈值（按住超过后进入蓄力分支）。  
  EN: Hold threshold to qualify as charged.
- `chargeLevels`：蓄力分档数组（按 `minHoldTime` 选最高命中档）。  
  EN: Charge tiers selected by highest satisfied `minHoldTime`.

- `stages`：同一张技能内的阶段表（不切换技能资产）。  
  EN: In-sheet stage list without changing skill asset.

- `cdPolicy`：CD 结算策略（`OnFirstCast/OnLastCast/OnSkillEnd`）。  
  EN: Cooldown settlement policy.
- `baseCooldown`：基础冷却秒数。  
  EN: Base cooldown seconds.
- `maxCharges`：充能上限（>1 时走充能模型）。  
  EN: Charge count cap for multi-charge model.
- `rechargeTime`：每层充能恢复时间。  
  EN: Per-charge refill time.
- `ignoreGlobalCooldown`：是否忽略公共 GCD。  
  EN: If true, skip global cooldown gating.

- `costs`：资源消耗列表（运行时按 `costByLevel` 或 `baseAmount` 扣）。  
  EN: Resource costs consumed on cast enter.

- `targetType`：目标类型（Self/SingleTarget/Area/Direction/Projectile）。  
  EN: Target acquisition strategy.
- `maxRange`：最大目标范围（Area 等会参与采样/限制）。  
  EN: Max targeting range.

- `traits`：技能 trait 位标记。  
  EN: Base trait bit flags.
- `traitUnlocks`：等级解锁 trait。  
  EN: Trait unlocks by level.

- `comboChain`：连招链（每次连段切换到“另一张 SkillDataSO”）。  
  EN: Combo segments as separate skill sheets.
- `comboResetTime`：连招超时重置秒数。  
  EN: Combo timeout before reset.

- `maxLevel`：技能等级上限（目前主要用于曲线计算）。  
  EN: Skill max level used with scaling curves.
- `damageByLevel`：等级伤害曲线。  
  EN: Damage scaling curve by level.
- `cooldownByLevel`：等级冷却曲线。  
  EN: Cooldown scaling curve by level.
- `costByLevel`：等级消耗曲线（覆盖每条 cost 的 baseAmount 语义）。  
  EN: Cost scaling curve overriding base amount behavior.

---

## 3.2 `ChargeLevel`（蓄力分档）

- `minHoldTime`：达到该档所需最小按住时长。  
  EN: Minimum hold time to enter this tier.
- `action`：覆盖 Action（可空）。  
  EN: Optional action override for this tier.
- `stageOverride`：覆盖 Stage（可空）。  
  EN: Optional stage override for this tier.
- `damageMultiplier`：本档伤害倍率。  
  EN: Damage multiplier for this tier.
- `motionOverride`：预留（当前主流程未强制读取）。  
  EN: Reserved for motion override integration.

---

## 3.3 `SkillStageSO`（单阶段）

- `action`：动作时间轴数据（窗口、位移、瞬移触发）。  
  EN: Action timeline asset for animation/windows/motion.
- `tasks`：该阶段并行任务数组。  
  EN: Parallel task list executed in this stage.
- `transitionType`：阶段过渡类型（Auto/OnInput/OnHit/OnTimer）。  
  EN: Stage transition mode.
- `transitionWindow`：过渡窗口时长。  
  EN: Transition window duration.
- `requireConfirmInput`：是否需要确认输入。  
  EN: Whether explicit confirm input is required.
- `hitShape`：命中形状配置。  
  EN: Hit shape config reference.
- `targetFilter`：目标过滤器。  
  EN: Target filter config.

---

## 3.4 `SkillLoadoutSO`（槽位装配）

- `bindings[].slot`：槽位 ID（`SkillSlotType`）。  
  EN: Logical slot ID for routing.
- `bindings[].skill`：该槽位装配的技能数据。  
  EN: Equipped `SkillDataSO` for that slot.
- `bindings[].hudKeyLabel`：仅 UI 兜底文案，不改变真实绑定。  
  EN: UI fallback label only; does not change actual input binding.

---

## 3.5 `IntentSkillSlotMapSO`（意图到槽位覆盖）

- `entries[].intent`：`GameplayIntentKind`。  
  EN: Intent kind to remap.
- `entries[].slot`：目标 `SkillSlotType`。  
  EN: Destination slot type.

用途：覆盖默认 `TryMapIntentToSlot`。  
EN: Override default intent-to-slot routing.

---

## 3.6 `ActionDataSO`（动作层关键参数）

- `MainClip`：主动画片段。  
  EN: Primary animation clip.
- `CrossfadeTime`：动画过渡时间。  
  EN: Crossfade duration.
- `AnimSpeed`：播放速度倍率。  
  EN: Animation speed multiplier.
- `Duration`：逻辑时长（可与动画长度不同）。  
  EN: Logical duration (can differ from clip wall-clock).
- `MotionProfile`：程序化位移配置（空则仅表现层动作）。  
  EN: Programmatic motion profile; null means animation-only movement.
- `Windows`：窗口切片（HitFrame/可打断窗等）。  
  EN: Timeline windows generating runtime events/tags.
- `TeleportTriggers`：离散瞬移触发点（按归一化时间触发一次）。  
  EN: One-shot teleport triggers by normalized timeline.

---

## 3.7 `AbilityTaskSO` 参数总表 / Task Parameter Summary

- `ApplyDamageTaskSO`
  - `damageMultiplier`：伤害倍率。 / EN: Damage multiplier.
  - `radius`：范围半径。 / EN: Overlap radius.
  - `hitMask`：命中层。 / EN: Layer mask for hit test.

- `ApplyEffectTaskSO`
  - `buff`：施加 Buff。 / EN: Buff definition to apply.
  - `applyTo`：施加目标（Self/SingleTarget）。 / EN: Target side for effect.

- `WaitConfirmTaskSO`
  - `cancelOnTimeout`：超时是否失败。 / EN: Fail on timeout.
  - `timeout`：超时秒数。 / EN: Timeout seconds.

- `WaitWindowSignalTaskSO`
  - `waitFor`：等待窗口事件类型。 / EN: Runtime window event to wait for.

- `DashTaskSO`
  - `distance`：总位移距离。 / EN: Total dash distance.
  - `duration`：总时长。 / EN: Dash duration.
  - `strengthByNorm`：速度分布曲线。 / EN: Normalized strength curve.

- `MoveToTargetTaskSO`
  - `speed`：追击速度。 / EN: Chase speed.
  - `stopDistance`：停止距离。 / EN: Stop distance.
  - `maxChaseRange`：最大追击范围。 / EN: Max chase range.
  - `allowTracking`：是否持续追踪。 / EN: Continuous tracking toggle.

- `SpawnProjectileTaskSO`
  - `projectilePrefab`：投射物预制体。 / EN: Projectile prefab.
  - `speed`：飞行速度。 / EN: Projectile speed.
  - `lifetime`：生存时间。 / EN: Lifetime.
  - `spawnOffset`：生成偏移。 / EN: Spawn offset.
  - `collisionRadius`：碰撞半径。 / EN: Collision radius.

- `SpawnSummonTaskSO`
  - `summonPrefab`：召唤物预制体。 / EN: Summon prefab.
  - `localOffset`：局部偏移。 / EN: Local spawn offset.

- `ShowIndicatorTaskSO`
  - `indicatorPrefab`：指示器预制体（当前为占位）。 / EN: Indicator prefab (currently placeholder).

- `ModifyCooldownTaskSO`
  - `targetSlot`：目标槽位。 / EN: Target slot.
  - `op`：操作类型（Reset/ReduceFlat/ReducePercent/Set）。 / EN: Cooldown operation.
  - `value`：参数值。 / EN: Value for the operation.

---

## 4) 连招配置（实操）/ Combo Configuration (Practical)

## 4.1 推荐架构（强烈建议）/ Recommended Architecture

- 用 `comboChain` 管“段与段切换”，每段是独立 `SkillDataSO`。  
  EN: Use `comboChain` for segment switching with separate `SkillDataSO` assets.
- 用 `stages` 管“单段内部阶段”。  
  EN: Use `stages` for intra-segment phases only.
- 不建议一个根技能同时复杂叠加：长 `comboChain` + 多 `stages` + 二段窗。  
  EN: Avoid stacking long comboChain, multi-stage, and second-stage window on one root unless necessary.

## 4.2 配置步骤 / Setup Steps

1. 为每一段攻击创建 `SkillDataSO`（如 A1/A2/A3）。
2. 每段至少有 `stages[0]`，并绑定对应 `ActionDataSO`。
3. 创建“根技能 RootSkill”，把 `comboChain = [A1, A2, A3]`。
4. RootSkill 的 `comboResetTime` 设为可接受输入窗口（建议 0.8~1.5）。
5. 在 `SkillLoadoutSO` 中把对应槽位（如 `Skill_Primary_01`）绑定到 RootSkill。
6. 运行测试：连续短按应推进 A1->A2->A3；停顿超过 resetTime 应回到 A1。

## 4.3 关键行为（代码语义）/ Runtime Semantics

- `PlayerActionState` 在一次攻击“干净结束”后推进 `SkillRuntime.AdvanceCombo()`。
- `SkillRuntime.IsComboExpired()` 为真时，输入前会重置 `ComboIndex`。
- `comboChain` 是切换 SkillSheet，不是同一 Sheet 的 stage 递进。

---

## 5) 蓄力配置（实操）/ Charge Configuration (Practical)

## 5.1 输入前提 / Input Preconditions

- Primary（LMB）按住时长由 `PrimaryAttackPressTracker` 在松手时写入 `PrimaryHoldDurationSeconds`。
- Secondary（RMB/Interact）按住时长由 `SecondaryInteractPressTracker` 写入 `SecondaryHoldDurationSeconds`。
- `SkillSystem` 根据意图类型选择 primary/secondary hold 作为蓄力判据。

## 5.2 支持蓄力判定的槽位 / Slots Supported by Charge Commit

- `Skill_Primary_01` + `LightAttack`
- `Skill_Primary_03` + `ChargeAttack`
- `Secondary_04` + `HeavyAttack`
- `Ability_06` + `CastAbility1`
- `Ultimate_05` + `CastUltimate`

## 5.3 配置步骤 / Setup Steps

1. 在目标 `SkillDataSO` 设 `castType = Charge`。
2. 设 `chargeThreshold`（建议先从 0.18~0.35 秒试起）。
3. 配 `chargeLevels`（可选）：
   - 档位按 `minHoldTime` 升序。
   - 每档可设置 `stageOverride`/`action`/`damageMultiplier`。
4. 若不配 `chargeLevels`，系统会回退：优先尝试 `stages[1]` 作为长按段。
5. 在对应 `SkillLoadout` 槽位装配该技能。
6. Play 下短按/长按对比验证（动作、伤害、窗口行为是否符合）。

## 5.4 常见误区 / Common Pitfalls

- `castType=Charge` 但没有正确槽位与意图组合，可能永远走不到蓄力分支。  
  EN: Charge logic requires valid slot-intent pair.
- `chargeThreshold` 过高导致看似“蓄力失效”。  
  EN: Overly high threshold makes charged path unreachable.

---

## 6) HUD 键位动态显示与换绑刷新 / Dynamic Key Label + Rebind Refresh

- 技能槽 UI 的键位显示来源：`InputReader.GetSkillSlotBindingDisplayString(slot)`。  
  EN: Slot key labels are pulled from input binding display by slot.
- `SkillBarPresenter` 在绑定时主动刷新一次（初始化正确）。  
  EN: Presenter performs pull refresh on bind for correct initialization.
- `RebindManager.OnBindingsChanged` 触发时，`SkillBarPresenter` 被动刷新（换绑后即时更新）。  
  EN: HUD refreshes on binding change event after rebind/reset/load.
- `hudKeyLabel` 仅做兜底文案，不是输入真源。  
  EN: `hudKeyLabel` is fallback display only, not actual binding source.

---

## 7) 全链路测试方案 / Full Test Plan

## 7.1 基础冒烟（必须先过）/ Mandatory Smoke Tests

1. **Loadout 生效**：每个槽都能拿到正确图标。  
   EN: Slot icon matches equipped skill.
2. **键位初始化**：进入场景后 HUD 键位标签与当前绑定一致。  
   EN: Initial HUD key labels match active bindings.
3. **换绑刷新**：在设置中改键后，HUD 标签即时变更。  
   EN: HUD updates immediately after rebind.

## 7.2 连招测试矩阵 / Combo Test Matrix

1. 短按三次：应按段推进，最终循环或重置符合设计。  
2. 间隔超过 `comboResetTime` 后再按：必须回到第一段。  
3. Action 中断（被打断/切状态）后：检查连段是否仍符合你的规则。  
4. `cdPolicy=OnLastCast` 时：中间段不应提前结算最终 CD。

## 7.3 蓄力测试矩阵 / Charge Test Matrix

1. `hold < threshold`：应走普通段。
2. `hold == threshold` 附近：确认边界行为稳定。
3. `hold` 命中不同 `chargeLevels`：动作覆盖/伤害倍率正确。
4. 不配置 `chargeLevels` 时：验证是否正确回退 `stages[1]`。
5. 对 `Ability_06`/`Ultimate_05` 分别测试 primary/secondary hold 输入口径。

## 7.4 Cast 五态行为测试 / Cast Type Behavior Tests

- `Instant`：立即触发窗口与任务。
- `CastTime`：读条未完成松手应取消（`WasCancelled` 路径）。
- `Channel`：提前松手应中断（`ReleasedEarly` 路径）。
- `Charge`：按住-松手后才触发并按时长选档。
- `HoldRelease`：按住持续，松手完成。

## 7.5 Task 测试清单 / Task Validation Checklist

- `ApplyDamage`：HitFrame 到达时有伤害；半径与层级正确。
- `ApplyEffect`：Buff 目标正确（Self/SingleTarget）。
- `SpawnProjectile`：HitFrame 触发生成，速度/寿命符合配置。
- `SpawnSummon`：HitFrame 触发召唤，偏移正确。
- `MoveToTarget`：追踪、停距、超距失败逻辑正确。
- `ModifyCooldown`：目标槽 CD 按 op/value 改变。
- `WaitWindowSignal`：只在目标事件到来时完成。
- `WaitConfirm`：超时策略符合 `cancelOnTimeout`。

## 7.6 资源/CD/GCD 测试 / Resource-CD-GCD Tests

1. 资源不足时不可施放。  
2. `maxCharges > 1` 时正确扣层并按 `rechargeTime` 回充。  
3. `CooldownReduction` 生效上限符合代码（当前 clamp 到 40%）。  
4. `ignoreGlobalCooldown=true` 的技能不应被 GCD 阻塞。

## 7.7 自动化建议 / Automation Suggestions

- 参考现有 `Tests/Editor/SkillRuntimeTests.cs` 增加以下用例：
  - Combo timeout reset test.
  - Charge level selection boundary test.
  - `OnLastCast` cooldown suppression across multi-stage test.
  - `OnBindingsChanged` event propagation test (integration).

---

## 8) 快速排障 / Troubleshooting

- **问题：按键显示对了但技能不放**  
  - 检查 `Intent -> Slot` 映射（默认或 `IntentSkillSlotMapSO`）。
  - 检查该槽位是否真有 `SkillLoadout` 绑定。
  - 检查 `CanCast` 条件（CD/资源/眩晕/二段窗）。

- **问题：连招总断**  
  - `comboResetTime` 太短。
  - 你的 Action 退出不满足“clean exit”推进条件。
  - 被中断后逻辑期望与实际不一致。

- **问题：蓄力不生效**  
  - `castType` 不是 `Charge`。
  - `chargeThreshold` 太高。
  - 输入源没有把 hold 时长带入对应 intent。

- **问题：任务不触发**  
  - 多数任务绑定在 `HitFrame` 事件；检查 Action 窗口是否真的进入过该事件。
  - Cast gating（CastTime/Channel/HoldRelease）可能阻止窗口派发。

---

## 9) 推荐配置基线（便于先跑通）/ Recommended Baseline

- 普攻根技能：`castType=Instant`，`comboChain` 三段，`comboResetTime=1.2`。
- 蓄力技能：`castType=Charge`，`chargeThreshold=0.22`，至少两档 `chargeLevels`。
- 冷却策略：多段技能优先 `OnLastCast`，单段可 `OnFirstCast`。
- 测试环境：先只开 1 个可攻击目标，避免多目标干扰排查。
- HUD：优先动态键位显示，`hudKeyLabel` 仅当查询失败时兜底。

---

## 10) 交付检查清单（上线前）/ Pre-Ship Checklist

- [ ] 每个可用槽位都在 `SkillLoadout` 有有效绑定。  
- [ ] 每个技能至少有 1 个有效 Stage 且 `stage.action != null`。  
- [ ] 关键技能都做了短按/长按/中断测试。  
- [ ] 关键技能都做了资源不足与 CD 阻塞测试。  
- [ ] HUD 键位在初始、换绑、重置绑定后都正确。  
- [ ] Console 无持续 MissingSkill 警告。  

