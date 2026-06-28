
---

# 《ARPG 战斗与伤害系统》落地计划书

> 产出时间：2026-05-20 关联资料：`【TODO】战斗与伤害系统.md` 范围声明：**忽略网络同步**（Net.* 全部留空）；聚焦"单机权威"的战斗 Authoring + Runtime 闭环。

## 0. 元信息

|项|内容|
|---|---|
|计划名|Combat Core — HitBox→Damage 管线跑通 + 怪物/Boss 可编辑 + 语义标签|
|切片名|Slice-Combat-Core-MVP|
|总 Landing 数|**7**（L1–L7）|
|最早 Play Mode 验证|**L1**（玩家普攻经真实 HitBox 命中 TestDummy → 扣血）|
|关联 Rule|07-single-track-migration / 01-zero-gc / SkillRoute v4.3.6 经验|
|建议 git tag|`refactor/combat-core-v1`|

## 1. 现状盘点（已具备 vs 缺口）

这一步是诚实边界的前提——你已经把**数据层几乎铺满了**，真正缺的是**运行时那一根接线**。

### 已具备（数据 + 孤立逻辑）

|层|资产/类型|状态|
|---|---|---|
|标签|`GameplayTagContainer`（State/Status/Ability/Mechanic/Faction 五轨 + Event 经总线）|✅|
|标签位|`StateTag.HitboxActive_Window / Invulnerable / RootMotion_Window` + Phase + 6×AllowInterrupt|✅|
|标签位|`StatusTag.Invincible/SuperArmor/Stagger/Stun/Root/Burn…`、`MechanicTag.ImmuneCC/ArmorTypeHeavy/WeakPointHead`|✅（仅枚举，无消费）|
|动作|`ActionDataSO` + `ActionWindow`（归一化时间窗）+ `RuntimeEvents`（HitFrame/SFX/VFX）+ `EvaluatePhaseTags`|✅|
|动作|`ActionWindowTimelineEvents.AppendEventsOnWindowEnter`（边沿进入采集器）|✅ **但无人调用**|
|命中|`HitShapeSO`（Box/Sphere/Capsule/Cone，`Overlap` NonAlloc）|✅|
|命中|`SkillStageDefinition.HitShape / TargetFilter / DamageSheet`（per-stage override）|✅|
|伤害|`DamagePipeline`（Base→Defense→Crit→Clamp 四段）+ `CombatContext` + `HitContext` + `DamageResult` + `IDamageStage`|✅|
|受体|`IDamageable`（`TakeDamage`/`ReceiveDamage`）、`Entity`（HP/Stat/Buff）、`TestDummy`（全实现）|✅|
|分发|`EffectSystem.ApplyInstantDamage`（ctx→pipeline→ReceiveDamage→`NotifyHit`）|✅ **仅 Effect 路径，近战 HitFrame 未走**|
|属性|`MonsterStatsSO/EntityStatsSO/StatType/ResourcePool/BuffStack`|✅|

### 缺口（要让"管线跑通"必须补的）

1. **运行时缺一根接线（核心）**：`PlayerActionState.OnLogicUpdate` 调了 `EvaluatePhaseTags`，但**从不调 `AppendEventsOnWindowEnter`、从不读 `Stage.HitShape`、从不 `Overlap`**。近战 HitFrame → 扣血整条断裂。
2. **HitFrame 事件只携带 `string Payload`**，形状却在 `SkillStageDefinition.HitShape` 上——"命中帧用哪个形状/伤害表"的归属未定义。
3. **无 `DamageRequest` 载体**（资料 §15）：当前 `CombatContext` 每个调用点手搓，Source/Target/Tags/Reaction/PoiseDamage 无统一载体。
4. **无单次挥砍去重**（一次 HitBox 激活内同目标只结算一次）。
5. **无敌帧不被消费**：`Invulnerable`/`Invincible` 受击侧无人检查 → 无敌不挡伤害。
6. **无韧性数值/Poise 段**：`StatusTag.Stagger` 只是个 flag，没有 `PoiseDamage` 进管线、没有 Poise 资源。
7. **无受击反馈（Reaction）**：Light/Heavy/Launch/Knockback 缺失。
8. **无 Enemy/Boss 实体复用 Route/Action 管线**：`MonsterStatsSO` 只是属性，没有"和玩家同一套技能管线"的敌人；无 Boss Phase。
9. **新语义标签层**：资料强烈主张能力式标签（`Movement.Displace / Defense.Evasive / Attack.Melee / Control.*`），当前是枚举式且有空缺。
10. **无 HitBox Scene 预览 Gizmo**（编辑期可视化判定盒）。

## 2. 诚实边界

### 本计划做什么（P0）

近战 HitBox 命中链路、DamageRequest 载体、无敌帧消费、单挥去重、语义标签层、Poise/受击反馈最小集、敌人/Boss 复用同一技能管线、Boss Phase、HitBox Scene Gizmo。

### 本计划明确不做

- ❌ **不做完整 GameplayEffect/GAS**（Buff 已有 `BuffStack`，沿用，不重写）。
- ❌ **不做 AI 决策**（感知/黑板/Utility/行为树留下一 Slice）；Boss 本期只做"Phase + 机制触发 + 用现成 Route 出招"的脚本驱动，不接智能决策。
- ❌ **不做 Combat Director / Encounter / 仇恨表**。
- ❌ **不做网络同步**（按用户要求）。
- ❌ **不做元素反应**（标签留位，不结算）。
- ❌ **不做 ActionData Timeline 全功能编辑器**——只补 HitBox 的 Scene Gizmo，时间轴沿用现有 `Windows` 列表 Inspector。
- ❌ **不重写 DamagePipeline 四段**——只在链上插 Poise 段与 iFrame 拦截，不动既有结算口径。

## 3. 垂直切片与 Landing 顺序

> 排序原则：**最短路径打通一次"输入→命中→扣血"的真闭环**，再逐层加严谨度（去重/无敌/韧性/反馈），最后扩到敌人/Boss 与编辑器。

```
L1  DamageRequest + HitExecutor 接线   ← 第一次 Play Mode：玩家普攻真打到 Dummy
L2  无敌帧消费 + 单挥去重 + 目标过滤
L3  语义标签层（能力标签）+ ActionData 语义字段
L4  Poise/Stagger 数值 + Reaction 受击反馈最小集
L5  Enemy 实体复用 Route/Action 管线（怪物可编辑）
L6  Boss Phase + 机制触发（Boss 可编辑）
L7  HitBox Scene Gizmo + 命中调试 HUD
```

### 依赖简图

```
L1 接线 ──┬─→ L2 去重/无敌 ──→ L4 韧性/反馈
          └─→ L3 语义标签 ─────→ L5 敌人 ──→ L6 Boss
L1 ──────────────────────────────→ L7 Gizmo（与 L2+ 并行）
```

唯一硬序：**L1 必须最先**（其余全部依赖那根接线存在）。L3 语义标签可与 L2 并行。L7 可在 L1 后任意时刻并行。

---

## 4. 各 Landing 详述（方法 + 验收）

### L1 — DamageRequest 载体 + HitExecutor 接线（核心）

**方法**

1. 新增 `struct DamageRequest`（资料 §15 的真实落地，作为 `CombatContext` 的上游来源）：
    
    ```
    struct DamageRequest {    IEntity Source; IDamageable Target;    float BaseDamage; float AttackScale;     // 来自 DamageSheet    float PoiseDamage;                        // L4 用，先占位    DamageType DamageType; HitReactionType Reaction;    ulong AttackerStateTags; Vector3 HitPoint; Vector3 HitNormal;    int HitId;                                // 单挥去重用，L2 消费}
    ```
    
2. 新增 `HitExecutor`（运行时，挂在攻击发起侧或由 `PlayerActionState` 持有）：
    - 在 `OnLogicUpdate` 中**补调** `ActionWindowTimelineEvents.AppendEventsOnWindowEnter(action.Windows, m_prevNormalizedTime, nt, buffer)`；
    - 对 buffer 中 `Kind==HitFrame` 的事件：取 **当前 Stage 的 `HitShape`**（`player.SkillEntries.ActiveRoute.Stage.Definition.HitShape`）+ `DamageSheet`（Stage override 否则 Route）；
    - `hitShape.Overlap(origin, rot, s_results, layerMask, …)` → 对每个命中的 `IDamageable`：组 `DamageRequest` → `CombatContext` → `DamagePipeline.Compute` → `target.ReceiveDamage(result, ctx)` → `player.SkillEntries.NotifyHit(rule)`。
    - origin/rot 取角色 hand bone 或 transform；`s_results` 静态复用数组（0-GC）。
3. **明确 HitFrame 与 HitShape 的归属裁决**：`HitFrame` 事件不再需要 string 形状名——形状权威是 `Stage.HitShape`；`Payload` 留作"多 HitBox 编号"（可空）。把这条写进 `ActionWindowEvent` 注释。

**核心验收**

- ✅ V1.1：玩家普攻，`Stage.HitShape` 框到 `TestDummy` → Console 出 `[Dummy] damage=…` → HP 下降。
- ✅ V1.2：`DamageSheet.baseDamage=0`（非伤害招）→ 不触发 `ReceiveDamage`。
- ✅ V1.3：HitFrame 窗口 `[0.2,0.35]` 外不结算（边沿采集正确，前后摇不打人）。
- ✅ V1.4：单帧无 GC alloc（Profiler 验 `s_results` 无新分配）。

### L2 — 无敌帧消费 + 单挥去重 + 目标过滤

**方法**

1. **无敌拦截**：`HitExecutor` 命中后、`ReceiveDamage` 前检查 `target.Tags.HasAny(State, Invulnerable) || target.Tags.HasAny(Status, Invincible)` → 命中但 `damage=0` 且发 `Event.Combat.DodgeSuccess`（已存在 `CombatEventTag.CombatDodgeSuccess`）。无敌帧来源就是 `ActionWindow` 勾 `Invulnerable` 位（已支持写入）。
2. **单挥去重**：每次进入 HitboxActive 窗口分配一个 `HitId`（自增）；`HitExecutor` 持 `HashSet<int>`（按受体 InstanceID）记录本 HitId 已结算的目标，重复则跳过；窗口退出清空。
3. **目标过滤**：用 `Stage.TargetFilter` + `FactionTag` 排除友军/自己（攻击者 `TeamId`）。

**核心验收**

- ✅ V2.1：翻滚 `[0.1,0.5]` 配 `Invulnerable`，期间被 Dummy 反击 → 0 伤害 + DodgeSuccess 事件。
- ✅ V2.2：一次挥砍贯穿 3 帧，同一 Dummy **只扣一次**血。
- ✅ V2.3：玩家攻击不打到同队友/自己。

### L3 — 语义标签层（能力标签）

**方法**（落地资料 §十四「能力式标签」，但务实增量，不推倒枚举）

1. **新增** `enum CombatSemanticTag : ulong`（能力声明轨，挂到 `MechanicTag` 隔壁的新独立轨或并入 Ability 轨——建议新增第 6 持久轨 `Semantic`，避免污染 `Ability` 闸门语义）。位包括：
    
    ```
    Movement.Displace / Movement.Forced / Movement.TeleportAttack.Melee / Attack.Ranged / Attack.AOEDefense.Evasive / Defense.BlockControl.Unstoppable / Control.LaunchSelf / Control.Knockup
    ```
    
2. **ActionDataSO 加字段** `CombatSemanticTag SemanticTags`：动作声明"我是什么性质"，由 `HitExecutor`/打断系统按位查，**取代 `if(isRolling)` 式判断**（资料 §十八）。
3. 打断/反应改用 `HasTag(Defense.Evasive)` / `HasTag(Movement.Displace)` 查询语义，而非动作名。

**核心验收**

- ✅ V3.1：翻滚动作只声明 `Movement.Displace | Defense.Evasive`，无敌逻辑不再依赖动作名仍生效。
- ✅ V3.2：新增一个"瞬移闪"动作只声明 `Movement.Teleport | Defense.Evasive`，**零代码改动**即获得"可触发闪避判定 + 可打断低优先级"。

### L4 — Poise/Stagger 数值 + Reaction 受击反馈

**方法**

1. `StatType` 加 `Poise`、`PoiseRegen`；`ResourcePool` 注册 Poise 槽。
2. `DamagePipeline` 后插 `PoiseStage`：`PoiseDamage` 扣 Poise，归零 → 加 `StatusTag.Stagger` + 发硬直反应。
3. `enum HitReactionType { None, HitLight, HitHeavy, Launch, Knockback, Stagger }`，受体 `ReceiveDamage` 末尾按 `request.Reaction` + Poise 状态播反馈（动画 trigger / 击退 / HitStop）。先做最小：HitLight + Stagger + HitStop。
4. **HitStop**：命中瞬间双方 `Time.timeScale` 或局部时间缩放短暂停顿（动作游戏手感核心，资料 §十八 Milestone 必含）。

**核心验收**

- ✅ V4.1：连续轻击累积 PoiseDamage → Dummy 达阈值进 Stagger（播硬直）。
- ✅ V4.2：重击直接 `Launch`，Dummy 被挑空（接 Y 轴 Motion，与 XYZ 蓝图协同）。
- ✅ V4.3：每次命中有可感知 HitStop（≈0.05s）。

### L5 — Enemy 实体复用 Route/Action 管线（怪物可编辑）

**方法**（资料 §十二「CombatEntity」、§七「玩家和 AI 用同一套」）

1. 新增 `EnemyCharacter : Entity`，**持有与 Player 同构的** `SkillEntryService` + Action 状态机；出招走 `Intent → Route → ActionTimeline → HitExecutor`，**不是** `PlayAnimation()`。
2. 怪物配置全在 SO：`MonsterStatsSO`（属性）+ `SkillEntryDefinition`/`Route`（招式）+ 一个 `EnemyBrainSO`（最小：距离/CD 触发哪个 Slot，**非智能**，纯脚本表）。
3. 怪物的 HitBox/无敌/伤害与玩家**同一套 `HitExecutor`**，零分叉。

**核心验收**

- ✅ V5.1：新建一个怪物 prefab，只填 SO（属性 + 1 个攻击 Route）→ 它能用 HitBox 打到玩家并扣血。
- ✅ V5.2：怪物挨打同样走 Poise/无敌/Reaction（与玩家对称）。
- ✅ V5.3：改 Route 资产即改怪物出招，**无需改代码**。

### L6 — Boss Phase + 机制触发（Boss 可编辑）

**方法**（资料 §九「Phase/Mechanic」、§十「机制驱动」）

1. `BossDefinitionSO`：`Phase[]`（HP 阈值切换）+ 每 Phase 的 `Route` 池 + `MechanicTrigger[]`（如 "HP<50% 触发 AOE Route"）。
2. `BossController : EnemyCharacter`：按 Phase + 距离 + CD 预算选 Route 出招（脚本驱动）；Phase 切换发事件（HUD/特效）。
3. Boss 弱点：复用 `MechanicTag.WeakPointHead` + `HitShape`（命中头部判定盒额外倍率）。

**核心验收**

- ✅ V6.1：Boss HP 跨阈值 → 切 P2，招式池变化（可见行为差异）。
- ✅ V6.2：机制触发器在条件满足时放指定 Route（如召唤/AOE）。
- ✅ V6.3：策划改 `BossDefinitionSO` 的 Phase 阈值/招式池即生效，**无需改代码**。

### L7 — HitBox Scene Gizmo + 命中调试 HUD

**方法**（资料 §五–§十「Scene 可视化判定盒」）

1. `HitShapeSO` 加 `DrawGizmo(origin, rot)`；`SkillStageDefinition` 自定义 Editor 在选中时于 SceneView 画 Stage.HitShape。
2. 运行时调试：`HitExecutor` 在命中帧用 `Debug.DrawLine`/临时 Gizmo 画实际 Overlap 盒（开关：`Player.DebugSkillRoute` 复用）。
3. `CombatDebugHUD`（已存在）补：当前 Stage / HitId / 本帧命中数 / 最近 DamageResult。

**核心验收**

- ✅ V7.1：选中 Stage，SceneView 显示其判定盒位置/朝向/尺寸。
- ✅ V7.2：Play Mode 命中帧能看到判定盒实时绘制，与扣血时机一致（所见即所判）。

---

## 5. 设施 / 处理器表

|设施|类型|归属目录（建议）|Landing|
|---|---|---|---|
|`DamageRequest`|struct|`3_Gameplay/Combat/Damage/`|L1|
|`HitExecutor`|runtime class|`3_Gameplay/Combat/Hit/`|L1|
|`PlayerActionState` 补调 AppendEvents+HitExecutor|改造|既有|L1|
|`HitId` 去重 + 无敌拦截|HitExecutor 内|同上|L2|
|`CombatSemanticTag` + `Semantic` 第六轨|enum + Container|`2_Framework/GameplayTags/`|L3|
|`ActionDataSO.SemanticTags`|字段|既有|L3|
|`StatType.Poise` + `PoiseStage` + `HitReactionType`|enum + IDamageStage|`3_Gameplay/Combat/Damage/`|L4|
|`HitStop` 服务|静态/单例|`3_Gameplay/Combat/Feel/`|L4|
|`EnemyCharacter` + `EnemyBrainSO`|class + SO|`3_Gameplay/Characters/Enemy/`|L5|
|`BossDefinitionSO` + `BossController`|SO + class|同上|L6|
|`HitShapeSO.DrawGizmo` + Stage Editor|Editor|`Editor/Inspectors/`|L7|

## 6. 握手表（WIRE / OPEN）

|握手点|L1|L2|L3|L4|L5|L6|L7|
|---|---|---|---|---|---|---|---|
|H1 HitFrame → Stage.HitShape.Overlap → ReceiveDamage|WIRE|WIRE|WIRE|WIRE|WIRE|WIRE|WIRE|
|H2 受击侧 Invulnerable/Invincible 拦截|OPEN|WIRE|WIRE|WIRE|WIRE|WIRE|WIRE|
|H3 单挥 HitId 去重|OPEN|WIRE|WIRE|WIRE|WIRE|WIRE|WIRE|
|H4 语义标签查询替代动作名 if|OPEN|OPEN|WIRE|WIRE|WIRE|WIRE|WIRE|
|H5 Poise→Stagger + Reaction + HitStop|OPEN|OPEN|OPEN|WIRE|WIRE|WIRE|WIRE|
|H6 Enemy 走 Intent→Route→HitExecutor|OPEN|OPEN|OPEN|OPEN|WIRE|WIRE|WIRE|
|H7 Boss Phase/机制驱动出招|OPEN|OPEN|OPEN|OPEN|OPEN|WIRE|WIRE|
|H8 HitBox SceneView 可视化|OPEN|OPEN|OPEN|OPEN|OPEN|OPEN|WIRE|

每个 WIRE 配一条 `SkillRouteDebug.Log`（新增 `CatHit` / `CatDamage` 类别），命中帧可观测。

## 7. 总验收 Milestone（资料 §十八「第一版战斗系统完成」）

一条完整技能链路全程跑通，即视为本切片完成：

```
输入 → 语义解析 → Skill Route → Action Timeline
→ HitFrame → Stage.HitShape.Overlap → DamageRequest
→ DamagePipeline（Base→Defense→Crit→Poise→Clamp）
→ 无敌/去重/过滤裁决 → ReceiveDamage → 属性结算
→ Reaction（HitLight/Stagger/Launch）+ HitStop + 相机震动
→ 技能结束 / CD 启动
```

对称地，**敌人与 Boss 复用同一条链路**（L5/L6），不存在第二套近战结算路径。

## 8. 风险与回滚

|风险|缓解|回滚|
|---|---|---|
|HitExecutor 接线改动 `PlayerActionState` 触发回归|L1 只加不删，旧 Phase 派发路径不动|git tag `combat-core-v1` 前|
|新增第六标签轨 `Semantic` 牵动 `GameplayTagContainer` 全量|用独立轨而非挤 Ability，HasAll/HasAny 加一个 case|单文件回滚|
|Poise 段插入改伤害口径|插在 Clamp 前且只读 PoiseDamage，不改 FinalDamage 公式|抽掉 PoiseStage|
|Enemy/Boss 复用管线暴露 Player 专属耦合|L5 前先把 `SkillEntryService` 对 Player 的硬引用抽到 `IEntity`|维持怪物用占位 AI|
|HitStop 全局 timeScale 影响 UI/其它系统|用局部时间缩放（unscaledDelta 隔离 UI）|关 HitStop 开关|

## 9. 与既有蓝图的协同/冲突点

- **与 XYZ Motion 蓝图协同**：L4 `Launch`/`Knockback` 反馈依赖 Y 轴 Motion；建议 L4 在 XYZ 主蓝图 L3（YAxisPolicy 接管）之后排，否则挑空只能用旧重力 hack。
- **与 SkillGroup 补充蓝图协同**：L1 的 `DamageSheet` 取值口径（Stage override 否则 Route）与 SkillGroup 的 CD/Cost 继承同源——Group 引入后，伤害仍取 Route/Stage 层，不取 Group 层（Group 只管 CD/Icon/Cost）。**无冲突**。
- **冲突点**：资料主张"所有判断改能力标签"，但本期 **L3 只迁移战斗侧判断**（无敌/打断/反应），不强迁 FSM/移动侧 `if`，避免一次性铺太大。FSM 侧迁移留下一 Slice。

## 10. 未决问题（需你裁决）

- **Q1**：HitBox 的 `origin/rotation` 取角色 `transform` 还是绑骨骼（hand/weapon bone）？绑骨更准但需 Animator 暴露骨骼引用——建议先 transform + 偏移，L7 再升级骨骼。
- **Q2**：语义标签 `Semantic` 用**新增第六持久轨**，还是复用 `Mechanic` 轨多开几位？（新增轨更干净但动 Container；复用省事但语义混。建议新增轨。）
- **Q3**：HitStop 用全局 `Time.timeScale` 还是每实体局部缩放？（联机虽不做，但局部缩放更可控，建议局部。）
- **Q4**：Boss 本期"脚本驱动出招"是否够用，还是要直接上最小行为树？（建议脚本表，AI 留 P1。）
- **Q5**：敌人是否需要与玩家**完全同一个** `SkillEntryService` 实例类型，还是抽 `ISkillExecutor` 接口共享？（建议抽接口，避免 Player 专属字段泄漏到怪物。）

---

**一句话总结**：你的数据层已经 90% 就位，**L1 那根"HitFrame→HitShape→DamagePipeline→ReceiveDamage"的接线是整个计划的命门**——它一通，普攻就能真打到 Dummy；其余 6 个 Landing 都是在这根线上加严谨度（无敌/去重/韧性/反馈）和扩广度（怪物/Boss/编辑器）。要不要我现在就着手 **L1 的代码落地**（DamageRequest + HitExecutor + PlayerActionState 接线）？