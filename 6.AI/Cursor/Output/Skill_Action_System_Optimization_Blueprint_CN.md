# 技能与动作系统 · 优化技术文档与 AI 落地蓝图

> 本文档面向：**意图层（Intent）与槽位语义（SkillSlot）对齐、资源提交时机、位移职责划分、打断抽象化、蓄力形态扩展、动作期移动约束**，并与当前工程代码路径对齐，便于 AI 与策划按阶段落地。  
> 文中不照搬引用材料，而是在现有架构上的**认知升级**：先归纳矛盾，再给出可演进的数据流与实施切片。

---

## 一、问题总结（工程矛盾清单）

### 1.1 意图（Intent）与输入语义错位

- **历史路径**：左键 Tap → `LightAttack`、Shift / Space → `SwordDash` / `Dodge` 等**动作名级**意图，便于早期无技能系统的测试。
- **现状矛盾**：技能系统以 **`SkillSlotType` + SkillLoadout** 为真源；物理键可换绑，**硬件输入 ≠ 稳定语义**。同一按键在不同角色上应表示「激活某槽位」，而非固定「轻击/翻滚」。
- **HUD 诉求**：槽位 UI 需 **Slot → 绑定显示字符串**、**角色装配 → 图标/CD**，与「意图叫 LightAttack」脱钩。

### 1.2 资源条与展示管线

- **现象**：血/蓝/体力的「当前/最大」依赖 **`ResourcePool` 注册 + `ResourceBarView` 绑定 `ResourceType` + `PlayerHUDPresenter` 的 Stat 链接**；若 Inspector 未配对或主题未推送，进游戏看不到数值。
- **`ResourceGroupController`**：属于 UI 聚合层，若不参与 Presenter 绑定链，容易造成「有条无数据」。

### 1.3 Dash 任务 vs Motion SO

- **`DashTaskSO`**：逻辑层位移（可扣费、可触发伤害窗口），适合「技能定义的冲刺距离/时长」。
- **`MotionProfileSO`**：表现层「舞台装置」——曲线与节奏；若二者各写一套绝对距离，易产生 **双源真相**。
- **目标关系**：逻辑距离应由 **Skill/Task 或注入上下文** 决定；Motion 侧宜 **归一化曲线 × 运行时幅度**，或明确 **Override / Default** 优先级。

### 1.4 资源消耗时机

- **当前倾向**：起手扣费（`TryBeginSkillCostsIfNeeded`）适合瞬发；**可取消技能**若起手即扣，与「取消不扣资源」冲突。
- **缺口**：缺少 **`CostCommitPolicy`**（OnCastStart / OnFirstHitFrame / OnSkillEnd / OnCommitWindow）与 **回滚/返还** 钩子。

### 1.5 同技能重复输入（自我打断）

- **现象**：统一落在 `PlayerActionState`，第二次同槽输入可能被当作「同状态不可切」而吞掉。
- **缺口**：需 **执行实例 ID / 阶段指针**，或配置 **AllowSelfRestart**，与「同意图映射」解耦。

### 1.6 打断与离散窗口

- **现状**：`ActionInterruptResolver` 将 **`GameplayIntentKind` → AllowInterruptBy*** 标签再与时间窗聚合（见 `ActionInterruptResolver.MapIntentToInterruptTag`）。  
- **矛盾**：角色未必有「翻滚/剑冲」；**语义绑死在 Intent 名字**上，扩展新槽位要改映射表。
- **演进方向**：打断判断应逐步迁移到 **类别掩码（Category）或槽位通道**，Intent 仅作兼容层。

### 1.7 蓄力形态多样化

- **现状**：松手才派发蓄力相关 Intent；与 **按住循环、满蓄自动释放、满蓄不放视为取消、分段结算、返还资源/CD** 等需求不完全匹配。
- **缺口**：蓄力 **逻辑状态机**（计时、封顶、自动释放、取消策略）与 **动画阶段**（单 Clip 停帧 vs 多段衔接）需分层配置。

### 1.8 动作期移动约束

- **缺口**：「能否边走边打」应落在 **ActionDataSO / SkillStage** 或独立 **LocomotionConstraint**，由 Motor 读取，而非写死在 LocomotionState。

---

## 二、反思：架构演进的正确顺序

1. **先统一「槽位」与「输入回调」**：Input 只保证 **Slot 脉冲**；换绑只改 **Action→物理键**，不改槽位 ID。  
2. **再弱化 Intent 的业务含义**：保留 `GameplayIntentKind` 作迁移兼容，新增或主推 **`SlotActivation` / `GameplayIntent` 携带 Slot + HoldMeta**，由 `SkillSystem` 解析到 Loadout。  
3. **打断与消耗后置抽象**：在现有 `ActionWindow` 轨道上演进 **Category Mask**，避免一次性推翻标签系统。  
4. **位移单源真相**：Skill 输出 **TargetDistance / DurationScale** → Motion/Motor 只执行注入后的参数。  
5. **UI 与逻辑同源**：HUD 只认 **Slot + 当前角色引用**，不认「轻击」字符串。

---

## 三、目标架构：数据流总览

下列主链路为推荐 **目标态**（可与现有代码渐进融合）：

```
硬件输入 (任意设备)
  → InputReader：仅产生「槽位沿」事件 (Pressed/Released/HoldSec)
       （换绑 = 改 bindings，不改槽位枚举）

SkillSlotType（逻辑插座，稳定）
  → 查询 SkillLoadoutSO（每角色 / 每武器可不同）
  → 得到 SkillDataSO + Runtime（CD/Charge/Combo）

SkillSystem.Prepare（或等价入口）
  → 填充 SkillContext（Targeting、CastHandler、费用策略句柄）
  → 解析 ActionDataSO（时间轴 + Motion 注入参数）

PlayerActionState
  → TaskExecutor + WindowEvents + MotionExecutor
  → Motor / Buff / Damage

HUD
  → SkillBarPresenter：Slot 列表来自当前 Player + Loadout
  → 键位文案：InputReader / BindingDisplay(SkillSlotType)
```

**核心原则**：**输入信号 ≠ Intent 名字**；**Intent 可逐渐退化为「槽位激活的打包结构」**。

---

## 四、分主题设计方案（认知升级版）

### 4.1 Intent 重构：从「动作名词」到「槽位激活」

| 层级 | 职责 | 稳定性 |
|------|------|--------|
| 物理层 | 设备、绑定、Rebind 存档 | 随玩家变 |
| 槽位层 `SkillSlotType` | 路由 ID，与 Input 回调一一对应（代码层） | 稳定 |
| 装配层 `SkillLoadoutSO` | 槽位 → SkillData | 随角色/武器变 |
| 表现层 HUD | 仅展示 Slot 的绑定名与技能图标 | 只读查询 |

**落地策略（避免大爆炸）**：

- **短期**：保留 `PlayerIntentCatalog.ForSlot(slot)`，但文档与心智模型改为「槽位意图」；`GameplayIntentKind` 中业务色彩浓的项逐步标记为 **Legacy**。  
- **中期**：新增 **`struct SlotIntent { SkillSlotType Slot; float HoldPrimary; float HoldSecondary; uint Sequence; }`**，入队缓冲；旧枚举通过 Adapter 转 Slot。  
- **长期**：TransitionResolver 的过滤条件尽量依赖 **CapabilityTag + StateTag**，少依赖 `LightAttack` 字面。

### 4.2 HUD 与多角色

- **技能栏**：`SkillBarPresenter.Bind(Player)` 已有雏形；动态槽位数应用 **当前 Player 的 Loadout 有效条目**，预制体只提供 View。  
- **键位**：`SkillSlotType → GamePlay Map Action 名 → GetBindingDisplayString`（已具备查询入口方向）；换绑后 **`OnBindingsChanged` 全量刷新**。  
- **防串角色**：Presenter **仅绑定本地 Player 实例**；队伍切换时 **Unbind / Rebind**，禁止静态缓存「全局技能表」。

### 4.3 资源条（血 / 蓝 / 体）

**数据条件**：

- `Entity` 注册 **HP**；`Player` 注册 **Stamina、MP**（`StatType.MaxStamina / MaxMana` → `ResourcePool`）。  
- **条 UI**：`ResourceBarView` 指定 **`ResourceType`**；**最大值**若跟 Stat 走，在 `PlayerHUDPresenter.ResourceBinding` 里填 **`MaxStatLink`**（如 `MaxHealth`、`MaxStamina`、`MaxMana`）。  
- **验收**：进 Play 后 **Props 推 Player** 给 `PlayerStatusHud`，`PlayerHUDPresenter.Bind` 订阅 **`OnCurrentChanged` / `OnFinalValueChanged`**，首帧 `ForceSync`。

**ResourceGroupController**：仅负责布局/显隐，不参与数值；数值仍以 Presenter → View 为准。

### 4.4 Dash Task vs Motion：职责契约

| | DashTask（逻辑） | Motion（表现） |
|--|------------------|----------------|
| 回答的问题 | 这次冲刺 **多远、多久、是否受 Modifier 影响** | **曲线形状、脚步、相机** |
| 推荐关系 | 输出 **规范距离/时长** 注入 MotionExecutor | **归一化曲线 × 注入距离**；无注入则用 SO Default |

**禁止**：两处各写「4m」且不声明优先级 → 易分叉。  
**推荐**：在 **SkillContext / Motion 执行上下文** 增加 **`MotionOverride`**（距离、时长缩放、是否 RootMotionOnly）。

### 4.5 资源消耗：Commit 时机

建议在 **`SkillDataSO` 或独立 `SkillCostPolicy`** 增加：

- `CostCommitPoint`：`OnAbilityStart | OnHitCommit | OnAnimationEnd | ManualWindow`  
- 与 **`ActionWindowRuntimeEventKind`** 或自定义 **`CostWindow`** 对齐，在 **Commit 帧** 调用 `Drain`；若在此前 **Cancel**，走 **`RollbackPendingCost`**。

取消不扣费：仅在 **PendingCost** 阶段计时，Commit 时才入账。

### 4.6 同技能不打断同技能

- **判定维度**：**槽位 + 执行代数（Generation）或 InstanceId**。  
- 第二次按下：若配置 **不允许 SelfRestart** → 忽略；若 **允许 CancelThenRestart** → 先 **Abort 当前实例** 再开新实例。  
- 状态机仍是一个 `PlayerActionState`，但 **内部 Action 指针与意图缓冲** 可切换。

### 4.7 打断机制演进：从 Intent 映射到类别掩码

**现状**：`MapIntentToInterruptTag(intentKind)` — 适合迁移期。  
**目标**：`ActionWindow` 贡献 **`InterruptibleByCategoryMask`**（位移 / 普攻 / 重击 / 技能槽类…）；  
 incoming 侧携带 **`InterruptCategory`**（来自当前技能的元数据，而非 Intent 名）。

**过渡**：`GameplayIntentKind` → **查表** → `InterruptCategory`（一层适配），旧资产不改也能跑。

### 4.8 蓄力：逻辑与动画分层

**逻辑层（Skill）**：

- `MaxChargeTime`、`ReleaseBehavior`（AutoFire / HoldUntilRelease / CancelIfOverCap）、  
- `Tier[]`（时间阈值 → `ActionData` / `DamageMultiplier`）、  
- `PostChargePolicy`（返还 MP%、缩短 CD 等，走 Buff 或 `ModifyCooldownTask`）。

**表现层（Animation）**：

- **单动画**：归一化停帧 + 恢复播放；  
- **多动画**：Windup / Loop / Release 三段 Clip + CrossFade。

**与输入**：Input 层上报 **HoldDuration**；Skill 决定 **未满 / 满蓄 / 超时取消**，而非一律「松手才派发」。

### 4.9 动作期移动：数据驱动

建议在 **ActionDataSO** 或 SkillStage 扩展：

- `LocomotionPolicy`：`FullMove | ReducedSpeed(α) | StrafeOnly | RootMotionOnly | NoMove`  
- PlayerMotor 在 Action 活跃帧读取 **当前 Action 的策略**，与跑步输入合成。

---

## 五、AI 落地蓝图（分阶段、可验收）

### Phase A — 语义与 HUD 闭环（低风险）

- [ ] 文档化：**槽位 = 唯一路由 ID**；Intent 为兼容层。  
- [ ] 校验：`SkillBarPresenter` + `InputReader` 键位刷新路径；多角色切换 **Bind/Unbind**。  
- [ ] 资源条验收清单：`PlayerHUDPresenter` 三条绑定 **HP / Stamina / MP** 的 `ResourceType` + `MaxStatLink`。

**验收**：换绑后 HUD 键位变；换人后技能图标与槽位数正确。

### Phase B — Intent 中间表示（中风险）

- [ ] 引入 **`SlotIntent` 或扩展 `GameplayIntent` 携带 `SkillSlotType`**（可选字段）。  
- [ ] `PlayerController`：离散输入优先 **`ConsumeSkillSlotPressed(slot)`** → 构造槽位意图。  
- [ ] `SkillSystem`：优先按 **Slot** 解析技能；`GameplayIntentKind` 仅作 fallback。

**验收**：Shift/Space 在 Loadout 换成非翻滚技能后，行为随装配变，无需改 Input 脚本。

### Phase C — 消耗 Commit + 取消（中高风险）

- [ ] `SkillCostPolicy` + Pending/Commit；与 `PlayerActionState` 退出/取消路径挂钩。  
- [ ] 单元测试：取消 vs 完成 vs HitFrame Commit。

### Phase D — 打断类别化（高风险，改编辑器体验）

- [ ] 定义 **`InterruptCategory` 枚举 + 掩码**。  
- [ ] 扩展 `ActionWindow` 或 Slot Mask，迁移工具：旧 AllowInterruptByLight → Category。  
- [ ] `ActionInterruptResolver`：优先走 Category，Intent 映射作兼容。

### Phase E — 蓄力状态机扩展（高风险）

- [ ] Charge 子状态：计时器、AutoRelease、OverHoldCancel、Tier 选择。  
- [ ] 与 `SkillChargeCommit`、Primary 松手派发策略协调或替换。

### Phase F — Motion 注入与 Dash 统一（中风险）

- [ ] `MotionExecuteContext`：`OverrideDistance`、`DurationScale`。  
- [ ] `DashTask` / `MotionExecutor` 统一读取同一上下文。

---

## 六、与当前代码的锚点（便于 AI 检索）

| 主题 | 主要文件 |
|------|-----------|
| 槽位与输入 | `InputReader.cs`、`PlayerController.cs`、`SkillSlotType`、`PlayerIntentCatalog.cs` |
| 技能解析 | `SkillSystem.cs`、`SkillChargeCommit.cs` |
| 打断 | `ActionInterruptResolver.cs`、`ActionWindow` / `ActionWindowTagSlots` |
| 资源与 Stat | `Player.cs`（RegisterSlot）、`StatType`、`ResourcePool`、`PlayerHUDPresenter.cs` |
| HUD 技能栏 | `SkillBarPresenter.cs`、`SkillSlotView.cs` |
| Dash | `DashTask.cs`、`DashTaskSO.cs`、`MotionProfileSO`、`MotionExecutor` |

---

## 七、结语

当前系统的 **最大矛盾**是：**测试期「意图=具体动作」与正式期「意图=槽位激活」叠在同一套枚举上**。  
落地顺序应是：**槽位与绑定先稳定 → Loadout 为内容真源 → Intent 名称逐步退场 → 消耗与打断数据化**。  

按本文 **Phase A→F** 切片推进，可在不大规模推翻状态机的前提下，把 ARPG 技能管线推到可维护、可换绑、可多角色的工程水位。
