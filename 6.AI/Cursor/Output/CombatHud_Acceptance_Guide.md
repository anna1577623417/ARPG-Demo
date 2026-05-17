# ARPG v4.3 · 战斗 HUD / 属性 / 技能 — 验收操作指南

本文面向 Unity 编辑器搭建（SO、挂载、层级），按 **易到难** 排列，便于逐条验收。

---

## 0. 冷却遮挡（Cooldown Mask）语义 — 必须先对齐美术预制体

**规则：`cooldownMask`（场景中常见节点名 `Bloker_CD`）的 Image.fillAmount 表示「遮罩覆盖比例」。**

| fillAmount | 含义 |
|------------|------|
| **1** | 刚进入冷却：遮挡最大（满遮罩） |
| **0** | 冷却结束：无遮挡 |

代码路径：`SkillSlotView.SetCooldownProgress(float progress01)`  
内部：`cooldownMask.fillAmount = 1f - progress01`，其中 `progress01` 从 **0（刚起手 CD）→ 1（CD 走完）**。  

请在预制体上把 **Radial Fill / 顺时针方向** 调成「遮挡随数值减小而揭开」，若美术反向只需反转 Image 的 Fill Origin/Clockwise（不要在代码里猜）。

---

## 1. 属性系统 — 如何搭建与绑定到角色

### 1.1 已有链路（无需再造轮子）

- **`EntityStatsSO` / `PlayerStatsSO`**：`Create Asset → GameMain/Stats/…`，填写 `baseStats`（MaxHealth、Walk、Run、AttackPower…）。
- **`Entity` / `Player`**：Inspector 里 **`stats Blueprint`** 字段拖入上述 SO。
- **`Awake` 时**：`Entity` 读取蓝图 → 写入 **`StatSet`** → **`ResourcePool`** 注册 HP（Max 随 Stat 变化）。
- **玩家体力**：`Player` 在 `Awake`/`Init` 里额外 `RegisterSlot(Stamina)`（见 `Player.cs`），建议在 **`PlayerStatsSO`** 内维护 `MaxStamina` 与列表里的 `StatType.MaxStamina`。

### 1.2 验收步骤（属性）

1. 新建 **`PlayerStats`** 资产，勾选 **`usePlayerBaseStatsPreset`**（可选）或手动编辑列表。
2. 场景中 **`Player`** → **`stats Blueprint`** → 拖入该资产。
3. Play：用调试或 HUD 看 HP/Stamina 是否与表里 Max 一致；受伤/消耗后 **`OnCurrentChanged`** 驱动血条（见第 3 节）。

---

## 2. 技能系统 — 技能组、三连击、意图映射

### 2.1 「一组三连普攻」推荐做法（Primary 槽）

引擎支持 **`SkillDataSO.comboChain`**（连招链）：根技能绑在 **`SkillSlotType.Primary`**，`comboChain` 放 **3 段**子 **`SkillDataSO`**（每段通常含自己的 `stages[0].action`）。

**运行时**：每次成功出手推进 **`SkillRuntime.ComboIndex`**（见 `SkillSystem` / `SkillSegmentResolver.ResolveSegment`）。  
若超时未继续按，`runtime.IsComboExpired()` 会重置连招（见 `SkillSystem.TryPrepareIntentForSkills`）。

**备选（迁移期）**：未装配 Loadout 或允许回退时，`WeaponMovesetSO.LightAttacks[]` 三连由 **`ResolveLightAttackForCombo`** 驱动（旧 Moveset 路径）。

### 2.2 技能装配资产：`SkillLoadoutSO`

1. **`Create → Skill/Skill Loadout`**。
2. **`bindings`**：逐项添加 **`slot` + `skill`**。
   - 例如：`Primary` → 你的「剑士普攻根技能」（含 `comboChain` 三段）。
   - **`hudKeyLabel`**（新增）：**纯 HUD 展示**，填 `LMB`、`RMB`、`Shift`、`Space`、`Q`、`R` 等，与下图标对齐。
3. **`Player`** → **`skill Loadout`** → 拖入该 Loadout。
4. **`allow Weapon Moveset Fallback…`**：全技能化验收阶段建议 **关闭**，以便缺绑定时立刻暴露配置错误。

### 2.3 输入意图 → 技能槽（当前引擎硬编码映射）

**意图种类**在 `GameplayIntentKind`；**技能槽**在 `SkillSlotType`。  
`SkillSystem.TryMapIntentToSlot` **当前**映射为：

| 意图 `GameplayIntentKind` | 技能槽 `SkillSlotType` |
|---------------------------|-------------------------|
| LightAttack | **Primary** |
| HeavyAttack | **Secondary** |
| Dodge | **Dodge** |
| SwordDash | **Ability2** |

因此：

- **左键普攻 / 右键重击 / 空格翻滚（以项目默认 Input 为准）** → 与上表一致时，Loadout 里应对应绑定 **Primary / Secondary / Dodge**。
- **`SkillSlotType.Ability1`、`Ultimate`、`Jump`** 等 **尚无通用意图枚举分支**；若要把 **Q/R** 做成独立技能槽，需要后续加 **`GameplayIntentKind` + InputReader 脉冲 + `TryMapIntentToSlot`**（代码级扩展）。  
  **验收期变通**：可把常用技能先绑到已有四映射之一（例如需要「冲刺」技能走 **`SwordDash` → Ability2**），并在 **`hudKeyLabel`** 写展示文字。

**物理键位**一律在 **`.inputactions`**（及 RebindManager）里改；**`hudKeyLabel` 不会改键**，只改 HUD 文案。

---

## 3. 资源条 + 技能栏 UI — 层级与挂载（对照 Hierarchy）

### 3.1 资源组（`ResourceGroup` / `1_HP_Bar` …）

1. 叶子物体挂 **`ResourceBarView`**（Fill / Buffer / Text）。  
2. 父级（可选）挂 **`ResourceGroupController`** 统一管理多条资源条。  
3. 同级挂 **`PlayerHUDPresenter`**：  
   **`resourceBindings`** 里添加条目：`Type = HP` / `Stamina`，`View` 引用对应条，`MaxStatLink = MaxHealth` / `MaxStamina`，`TextFormat = "{cur} / {max}"`。

### 3.2 技能槽组（`SkillSlotGroup`）

**两种模式**（`SkillBarPresenter.layoutMode`）：

| 模式 | 用法 |
|------|------|
| **InspectorSlots** | 场景中已有多个 `Skil_Slot_Item`，在 **`slotBindings`** 里逐个指定 **SkillSlotType + SkillSlotView**（与旧流程兼容）。 |
| **InstantiateFromPlayerLoadout** | **`slots Root`** = `SkillSlotGroup` 下的容器；**`slot Prefab`** = 完整一条槽预制体（含 **SkillSlotView**，建议同物体 **SkillCooldownTicker**）。运行时按 **`Player.SkillLoadout.bindings`**（`skill != null`）**生成对应数量**，并写入 **`hudKeyLabel`**。 |

**预制体 `Skil_Slot_Item` 必挂：**

- **`SkillSlotView`**：`Icon`、`Bloker_CD` → 拖到 **`cooldownMask`**；`Input_Key_Text` → **`keyHintText`**（可选）。
- **`SkillCooldownTicker`**（可在首次 Bind 时自动添加）：负责高频 CD 与可用性轮询。

### 3.3 顶层 HUD

**`PlayerStatusHud`**（或等价 HUD）：

- **`resourcePresenter`** → `PlayerHUDPresenter`
- **`skillBarPresenter`** → `SkillBarPresenter`
- **`BuffStripPresenter`**（若使用 Buff 条）

**Props**：业务侧推送 **`PlayerStatusHudProps`**，其中 **`Player`** 非空时走 Presenter 绑定。

---

## 4. 推荐验收顺序（由易到难）

1. **属性**：Player + `PlayerStatsSO` → Play 看数值与 Max 是否一致。  
2. **资源条**：受伤/耗耐 → 血条/耐条变化 + Buffer 延迟（仅 Drain）。  
3. **技能数据**：仅 Primary + `comboChain` 三连 → 轻攻击意图能打出三段（观察动画/伤害）。  
4. **Loadout + 动态技能栏**：`SkillBarPresenter` = **InstantiateFromPlayerLoadout**，填 **`hudKeyLabel`**，确认槽位数与图标与 Loadout 一致。  
5. **Buff 条**（若启用）：给角色上 Buff → `BuffStripPresenter` 出图标。  
6. **主题**：`ResourceBarView.useTheme` + `UIRoot` → 切换主题看染色。

---

## 5. 与本指南同步的代码增量（feat）

**feat(ui-docs): 冷却蒙版语义、Loadout HUD 键位、动态技能槽与验收指南 / combat HUD mask semantics, loadout key labels, dynamic skill slots, acceptance guide**

- `SkillSlotView`：冷却语义注释、`keyHintText` / `SetKeyHint`  
- `SkillLoadoutSO.SlotBinding.hudKeyLabel` + Inspector Drawer  
- `SkillBarPresenter.LayoutMode` + 按 Loadout 实例化槽位  

---

## 6. 常见问题

**Q：HUD 上写了 Q，但按 Q 不出技能？**  
A：`hudKeyLabel` 只是文案。真实按键由 **Input Actions** 决定；且 Q 未必已映射到新的 `GameplayIntentKind`（见 §2.3）。

**Q：三连总是回到第一段？**  
A：检查 **`SkillDataSO.comboResetTime`**、是否在超时后又按；或仍在走 **WeaponMoveset** 回退路径。

**Q：动态模式生成 0 个槽？**  
A：确认 **`SkillLoadout.bindings`** 里 **`skill` 非空**，且 **`slotsRoot` / `slotPrefab`** 已赋值。
