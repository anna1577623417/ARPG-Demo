> 最后更新：2026-05-16 19:10

# Ver4.6 单轨 SkillRoute 验收与搭建指南

> **文档角色**：Play 验收 + 场景搭建的**操作手册**（会随单轨施工更新）。  
> **权威蓝图**：`6.AI/Cursor/BluePrint/Ver4.3.6_Skill_Route_Runtime_AI_Blueprint_CN.md`  
> **施工令 / 日志**：`BluePrint/105.3 单轨落地【施工令】.md`、`Output/105.4 单轨施工日志.md`  
> **⚠ 历史说明**：下文 §1–§6 中仍出现 `SkillRouteUnit` / `SkillRouteDefinitionSO` / `SkillRuntime` 的段落，为 **Ver4.6.0 前驱脚手架** 遗留表述；**单轨运行时以 §0 为准**，勿按旧类型名接线。

---

## 0. 单轨架构（当前权威）

**最小技能单位 = `SkillRouteDefinition`（及子类）+ `SkillStageDefinition`；入口 = `SkillEntrySlot`；运行时总线 = `SkillEntryService`。**

```
设备
  → InputReader（Slot 双写 + InputModifierBuffer）
  → InputInteractionResolver / PlayerController 脉冲
  → GameplayIntent.Skill_Entry_NN（仅物理键位 + Hold + MoveBuffered）
  → PlayerStateManager（TransitionResolver 闸门）
  → SkillEntryService.TryResolveForIntent
       RouteResolver：Charge > Combo > Directional > Derivative > MultiStage > Normal
  → SkillRouteRuntime 子类（Normal / Combo / Charge / MultiStage / Derivative）
  → SkillStageRuntime + SkillTransition + ConditionEvaluator
  → Player.ArmPendingAction(Stage.Action) → PlayerActionState
  → MotionExecutor（MotionPlaybackContext：LoopWindow / 蓄力压速）
  → Player.RequestActionPresentation → PlayerAnimController
```

| 概念 | 类型 | 职责 |
|------|------|------|
| **入口** | `SkillEntrySlot` + `SkillEntryDefinition` | 仅物理键位；聚合多条 Route |
| **装配** | `SkillEntryLoadoutSO.Bindings[]` | Slot → EntryDefinition |
| **最小技能单位** | **`SkillRouteDefinition`**（抽象） | Icon / CD / Cost / Stages[] |
| **阶段** | `SkillStageDefinition` | 唯一握手 `ActionDataSO` |
| **运行时总线** | **`SkillEntryService`** | Resolve / ActiveRoute / HudHandles / CD Tick |
| **HUD** | `IRouteRuntimeHandle` → `RouteWidget` | `SkillEntryBarPresenter`（或 `SkillBarRoutePresenter` 分组版） |
| **Legacy** | `SkillDataSO` / `SkillRouteDefinitionSO` | **仅 Editor 迁移**；战斗帧不读 |

---

## 0.1 接线完整性快照（2026-05-16 单轨修复后）

| 链路环节 | 状态 | 说明 |
|----------|------|------|
| InputReader → Intent | ✅ 已接 | `SkillEntryIntentFactory` + `ConsumeSkillEntryPressed` |
| Intent 仲裁 → Resolve | ✅ 已接 | `PlayerStateManager.OnPreLogicUpdate` |
| Resolve → Action 播放 | ✅ 已接 | `ResolveStartStage` + `PlayerActionState` + `RequestActionPresentation` |
| Route Tick / Transition | ✅ 已接 | `SkillEntryService.TickActive`；MultiStage 同次 `SwapToStageAction` |
| Charge 双模式 | ✅ 已接 | `ChargeRouteRuntime` + `MotionPlaybackContext` |
| Combo 段位 | ✅ 已接 | `ComboRouteRuntime` + `_comboBySlot` |
| 方向技 Shift+WASD | ✅ 已接 | `InputModifierBuffer` → Intent → `DirectionalRouteSet` |
| LM→RM 派生 | ⚠ 半接 | `ArmDerivativeUnlock` + RM `DerivativeRoutes`；需资产与 Play 验证 |
| 多段盲僧 Q / 凯隐 Q | ⚠ 半接 | Runtime + `Tools/SkillRoute/Generate Paradigm Demos`；**命中回写未接 Task** |
| 拉克丝 E 锚点引爆 | ⚠ 半接 | `NotifySkillAnchorReady` + `MechanicTag.SkillAnchorReady`；**投掷物 Task 需接线** |
| HUD 动态 Icon/CD | ✅ 已接 | `SkillEntryService.HudHandles` → `SkillEntryBarPresenter` |
| 伤害 → OnHit 条件 | ❌ 未接 | `SkillEntryService.NotifyHit` **无战斗帧调用方** → 盲僧 Q1→Q2 条件门 Play 可能失效 |
| EditMode 测试 | ⚠ 混杂 | `SkillEntryRouteResolveTests` / `MultiStageRouteRuntimeTests` 可用；`SkillRuntimeTests` 等为旧套件 |
| 配方 Compiler（W10） | ❌ 未做 | 手填资产或 Paradigm 生成器 |

### 能否开始验收？

| 层级 | 结论 |
|------|------|
| **P0 通路验收** | **可以开始** — 前提：Player 绑定 **`SkillEntryLoadoutSO`**（非旧 `SkillLoadoutSO`），Entry 内 Normal/Combo/Charge Route 与 Stage.Action 已填；HUD 绑 `SkillEntryBarPresenter`。 |
| **P1 多 Route CD 隔离** | **可以开始** — `RouteRuntimeHandle.CdProgress01` 已接 Widget。 |
| **P2 Charge / Motion** | **可以开始** — 需 Charge Route 资产与 MotionProfile。 |
| **P4 范式技能（盲僧/拉克丝/凯隐）** | **暂不能作为 DoD** — 需：`NotifyHit` 接线、Lux 锚点 Task、Paradigm Loadout 绑定与 Play 矩阵。 |
| **签收（§7 全绿）** | **尚不能** — 旧文档 §7 清单需按 §0.1 重写；至少补 Hit 回写 + 资产 + 测试绿。 |

**建议验收顺序**：P0（LM Normal/Combo/Charge）→ P1 HUD/CD → P2 Charge → 再开 P4 范式专项。

---

## 0.2 场景搭建（单轨，必填）

| 组件 | 字段 | 期望 |
|------|------|------|
| **Player** | `skillEntryLoadout` | `SkillEntryLoadoutSO`（`Bindings[].entry` → `SkillEntryDefinition`） |
| **PlayerHudBootstrap** | `skillBarRoutePresenter` | **`SkillEntryBarPresenter`**（`widgetRoot` + `RouteWidget` prefab） |
| **Paradigm（可选）** | 菜单 | `Tools/SkillRoute/Generate Paradigm Demos (Ver4.6)` → 绑到 Player Loadout |

> 旧文 §2 中的 `Skill Loadout` / `SkillBarRoutePresenter` 若仍指 `SkillLoadoutSO`，请改为上表。

---

## 0.3 历史章节索引（下文 §1 起）

以下章节保留作迁移/对照参考；出现 **`SkillRouteUnit` / `SkillRouteDefinitionSO` / `SkillRouteService` / `SkillRuntime`** 时，请映射到 §0 单轨类型，或改用 **Tools/SkillRoute/** 与 **Paradigm** 生成器。

---

## 0.4 一句话架构（历史稿 · SkillRouteUnit 时代，仅供参考）

<details>
<summary>展开：Ver4.6.0 前驱脚手架表述（非当前运行时）</summary>

**输入只表达 Entry；RouteResolver 在 `SkillRouteUnit` 列表中选路……**（已废弃，见 §0）

</details>
---

## 1. 验收前门禁（未通过勿进 Play）

### 1.1 资产迁移（必做）

**创建 `SkillRouteDefinitionSO` 的三种方式（推荐顺序）：**

| 方式 | 路径 | 说明 |
|------|------|------|
| **① 迁移（推荐）** | **Tools → Skill → Migrate All SkillData → Route Definitions** | 从现有 `SkillData_*.asset` 批量生成，字段已填好 |
| **② 右键菜单** | Project 右键 → **Create → Skill → Route Definition** | 空白 Route，需手填 stages / features |
| **③ 顶部菜单** | **Tools → Skill → Create Route Definition (Blank)** | 在选中文件夹下创建空白 `SkillRoute_*.asset` |

> **看不到「Route Definition」？** 见 §5「菜单里没有 Route Definition」。

| 步骤 | 菜单 / 操作 | 通过标准 |
|------|-------------|----------|
| M1 | **Tools → Skill → Migrate All SkillData → Route Definitions** | Console 无报错；`4_Data/.../Routes/Generated`（或技能同目录）出现 `*_Route.asset` |
| M2 | Project 选中 **Player_SkillLoadout** → **Tools → Skill → Migrate Selected Loadout Entries** | 保存后 `entries[].units[].definition` **全部非空**；同一 Loadout **至多一条 `LM` 行**（该流程末尾会自动合并键位重复行） |
| M2b | （可选）**Tools → Skill → Merge Loadout Entry Rows (Key-Only Slots)** | 手工把旧资产里多条左键 / Reserved 行并入一条 `LM`，且不丢 `units` |
| M3 | 打开 Loadout → **LM** 行 → Units ≥ 3 | Normal / Combo / Charge 各有一条 unit，且 `showOnHud = true` |
| M4 | 每个 `SkillRouteDefinitionSO` Inspector | `features` 与预期一致（Charge 开 `enableCharge` 等）；`stages[]` 非空 |

### 1.2 代码 / 工程门禁

| 检查 | 命令 / 操作 | 通过标准 |
|------|-------------|----------|
| C1 | Unity 编译 | Console **0 error** |
| C2 | Test Runner → EditMode | `SkillRouteTransitionTests`、`SkillRuntimeTests` **全绿** |
| C3 | 选中 Player → **Tools → Skill → Route Runtime → Validate Loadout Entries** | 无 error 日志 |
| C4 | 全工程检索（可选） | 运行时 `.cs` 无 `new SkillRuntime(SkillDataSO` / `ResolvedSkillSheet` / `unit.skillData` |

**运行时 `SkillDataSO` 允许出现的位置（仅编辑器/迁移）：**

- `SkillLoadoutSO.bindings[]`（`[Obsolete]`，合成 entries 用）
- `Editor/*Migrator*`、`SkillDataSOEditor`
- `#if UNITY_EDITOR` 块内 `MakeRouteUnitFromLegacy`

### 1.3 Git 快照（强烈建议）

迁移前打 tag，便于对比与回滚：

```bash
git tag -a pre-skill-route-definition -m "迁移前：SkillDataSO 为施法源"
# 完成 M1–M4 并验收通过后：
git tag -a post-skill-route-definition -m "迁移后：SkillRouteUnit.definition + Route 级 CD"
git log --oneline --decorate -5
```

| Tag | 含义 | 回滚 |
|-----|------|------|
| `pre-skill-route-definition` | 旧 SkillData 管线（历史提交） | `git checkout pre-skill-route-definition -- <paths>` |
| `post-skill-route-definition` | Ver4.6.1 最终形态 | 当前主线 |

---

## 2. 场景搭建清单

### 2.1 Player（必填）

| 字段 | 期望 |
|------|------|
| **Input Reader** | 已绑定项目 `InputReader` |
| **Skill Loadout** | 如 `Player_SkillLoadout` / `Sword_01_SkillLoadout` |
| **Allow Weapon Moveset Fallback…** | 验收期 **关闭**（缺配置立即暴露） |
| **Weapon Moveset** | 闪避四向等 Moveset（与 Skill 并行） |
| **Debug Interrupt Flow**（可选） | 验收时可开；Perf 时关 |

> **已删除**：`Use Route Runtime` Inspector 开关；Route 施法管线在代码层**恒开**。

### 2.2 HUD — Route 动态栏（必做）

| 组件 | 配置 |
|------|------|
| `PlayerHudBootstrap` | `Skill Bar Route Presenter` **已赋值**（有值则优先于旧 Slot 栏） |
| `SkillBarRoutePresenter` | `Widgets Root` + `Route Widget Prefab`（含 `RouteWidget`） |
| | `Group Widgets By Entry` = ✓（LM 下 Normal/Combo/Charge 并排） |
| `RouteHudDebugOverlay`（可选） | 组件 **Enabled**；Play 时左上角显示 Entry/Route/CD |

**Bind 时机**：`ActivePlayerChanged` → `Bind(player)` → `Routes.Rebuild(loadout)` → 实例化 `showOnHud=true` 的 unit。

### 2.3 Loadout 结构验收

```
SkillLoadoutSO
└── entries[]
    └── SkillEntryRow (entrySlot = LM, …)
        └── units[]  ← SkillRouteUnit（最小路由单位）
            ├── definition → SkillRouteDefinitionSO
            ├── presentationKindOverride / showOnHud / hudKeyLabel
            └── routeIdOverride（可选）
```

**LM 三 Route 自动生成**：保存 Loadout 触发 `OnValidate` → `TryEnsurePrimaryLmTripleRoutes`（仍须 M1/M2 填充 `definition`）。

---

## 3. Play 模式 — 终极验收矩阵

> 建议打印 **Tools → Skill → Route Runtime → Print Smoke Checklist**，逐项打勾。  
> 每步对照 **RouteHudDebugOverlay** 或 **Log Route Debug Snapshot**。

### 3.1 P0 — 通路（必须通过）

| ID | 操作 | 期望 | Overlay / 日志 |
|----|------|------|----------------|
| P0-1 | 进 Play | HUD 出现 ≥1 组 RouteWidget；LM 理想为 **3 并排** | — |
| P0-2 | 轻点 LM | Normal 出手；`Current Entry: LM` | `Current Route: Normal` |
| P0-3 | Combo 窗内连点 LM | Combo 段；Combo index / window 有值 | Route 切 Combo |
| P0-4 | 长按 LM（Charge 已绑） | 蓄力条变化；松手分档 | `Charge Ratio` 行 |
| P0-5 | 技能进 CD | **被击中的 Route** 对应 Widget 遮罩 | `CD: <routeId> rem=…` |
| P0-6 | RM / Q 等其它 Entry | 对应 Route 出手（若已绑 definition） | Entry 名正确 |

**输入语义**：主攻击仅 **`LM` + primaryHold**；Tap/Combo/Charge **不由**三个 Legacy 意图开关配置。

### 3.2 P1 — Route 与 CD 隔离

| ID | 操作 | 期望 |
|----|------|------|
| P1-1 | Normal 出手进 CD | **仅 Normal** Widget CD；Combo/Charge 不受影响 |
| P1-2 | Combo 段出手 | Combo definition 的 `baseCooldown` 生效 |
| P1-3 | Charge 松手释放 | Charge Route CD；Prepare 阶段不重复扣 CD（与配置一致） |
| P1-4 | `maxCharges > 1` 的技能 | 充能数递减 / 回充计时与 `SkillRouteRuntimeState` 一致 |

### 3.3 P2 — Charge / Motion

| ID | 操作 | 期望 |
|----|------|------|
| P2-1 | 蓄满 | LoopWindow / 动画减速或定格（看 Stage Action 配置） |
| P2-2 | 松手 | 分档伤害 / 换 Action |
| P2-3 | 轻点 &lt; `charge.tapThreshold` | Tap fallback（`enableTapFallback` 时） |
| P2-4 | `MotionPlaybackContext` | Overlay `Playback` 行在蓄力期有 freeze/loop 标记 |

### 3.4 P3 — HUD 与 Presenter

| ID | 操作 | 期望 |
|----|------|------|
| P3-1 | `showOnHud=false` 的 unit | **不**生成 Widget |
| P3-2 | 仅 Charge 按住 | Charge 块蓄力条亮，其它块无蓄力 UI |
| P3-3 | 换 Loadout 后重进 Play | Widget 数量与 entries 一致（Rebuild） |
| P3-4 | `iconOverride` | HUD 显示覆盖图而非 definition.icon |

### 3.5 P4 — 多段 / Transition 范例（可选）

| ID | 内容 | 操作 |
|----|------|------|
| P4-1 | Lee Sin Q | `Tools/Skill/Create Transition Examples` → 绑 **Ability_06** → Q1 命中 → 3s 窗 → Q2 距离门控 |
| P4-2 | Lux E | 锚点落地 → 手动 E 或超时引爆 |

### 3.6 P5 — 闪避 / Chord（Phase 6）

| ID | 操作 | 期望 |
|----|------|------|
| P5-1 | W/A/S/D + Space | 四向闪避（`WeaponMovesetSO` Dodge* 已填） |
| P5-2 | Chord 修饰键 | `InputModifierBuffer` + Route 条件（若已配） |

---

## 4. 自动化与工具

### 4.1 Edit Mode 测试

**Window → General → Test Runner → EditMode**

| 套件 | 覆盖 |
|------|------|
| `SkillRouteTransitionTests` | Route 上下文、Transition |
| `SkillRuntimeTests` | Route 级 CD / 充能、`TaskExecutor` |

### 4.2 编辑器菜单（保留项）

| 菜单 | 作用 |
|------|------|
| **Tools → Skill → Migrate All SkillData → Route Definitions** | 从 Legacy SkillData 批量生成 Route |
| **Tools → Skill → Migrate Selected Loadout Entries** | 填充选中 Loadout 的 `unit.definition` |
| **Tools → Skill → Route Trial Kit Generator…** | 打开试用套件窗口（可编辑前缀/后缀） |
| **Tools → Skill → Generate Trial Route Kit V1** | 一键按 V1 规范生成 8 条 Route |
| **Tools → KCC → Mesh Combine Tool** | KCC 网格合并 |

**V1 试用 Route 命名（后缀默认 `_SkillRoute`）**：

`LM_Normal_01` · `LM_Charge_02` · `LM_Combo_03` · `RM_Normal_04` · `Q_Normal_05` · `R_Normal_06` · `SHIFT_Normal_07` · `SPACE_Normal_08`

生成目录默认：`4_Data/1.Skills/Routes/TrialKit/V1/`（Route 在根目录，Stage 在 `Stages/` 子目录）。

| Route | Stage 数量 | Stage 命名示例 |
|-------|------------|----------------|
| 除 Combo 外 | **1** | `LM_Normal_01_Stage01_SkillStage` |
| `LM_Combo_03` | **3** | `…_Stage01` / `…_Stage02` / `…_Stage03` |

窗口内可改 Route/Stage 后缀；「应用前缀/后缀」目前仅重命名 Route 资产。

### 4.3 Profiler（Perf 门禁，可选）

Play 连续普攻 + 蓄力 **60s**，关闭 `DebugInterruptFlow`：

- **GC Alloc** 战斗帧 ≈ 0（无每帧 `new` 字符串 / LINQ）
- UI：无数据变化时 RouteWidget 不每帧 Dirty

---

## 5. 故障排查

| 现象 | 根因 | 处理 |
|------|------|------|
| 技能栏空白 | 未绑 `SkillBarRoutePresenter` 或 Prefab/Root 空 | §2.2 |
| 只有 1 个图标 | 仅 1 个 unit 或 `showOnHud=false` | 检查 LM `units[]` 长度与标志 |
| 攻击变 Moveset 普攻 | Fallback 开启或 definition 空 | 关 Fallback；跑 §1.1 M1–M2 |
| 改 SO 不生效 | 改的是 Legacy `SkillDataSO` | 改 **`SkillRouteDefinitionSO`** + Loadout `definition` |
| Overlay Route 为 — | Resolver 未选中 unit | 检查 `presentationKind` 与 Charge/Combo 窗 |
| Console `SkillData` 相关告警 | 未迁移 | §1.1 |
| 文档提 Use Route Runtime | 历史文档 | **已删除**；以本文为准 |
| **Create → Skill 里没有 Route Definition** | 脚本未编译进工程 | ① Console 是否有 **红色编译错误**（有则先全修完）② 菜单是否仍显示 **Skill Data** 而非 **Skill Data (Legacy)**——若是旧菜单，说明 Unity 未加载 Ver4.6.1 脚本，点 **Assets → Refresh** 或重启 Editor ③ 直接用 **Tools → Skill → Migrate…** 或 **Create Route Definition (Blank)** |

---

## 6. 与旧系统对照（迁移人必读）

| 旧（Ver ≤4.5） | 新（Ver 4.6.1） |
|----------------|-----------------|
| `SkillSlotType` + `bindings[]` | `SkillEntrySlot` + `entries[]` |
| `SkillDataSO` 施法 | `SkillRouteUnit.definition` → `SkillRouteDefinitionSO` |
| `CastType` 单枚举 | `SkillRouteFeatures` 多特性 + `ResolvePrimaryCastMode()` |
| 技能级 CD 在 `SkillRuntime` | **Route 级** `SkillRouteRuntimeState` |
| `UseRouteRuntime` 开关 | **删除**；恒开 |
| `SkillSegmentResolver` / `SkillChargeCommit` | 内联 `SkillSystem` / `ChargePrepareHelper` + `ChargeRouteRuntime` |
| `SkillBarPresenter` 固定槽位 | `SkillBarRoutePresenter` 动态 `RouteWidget` |

**HUD 仅 UI 回退**：清空 `PlayerHudBootstrap.Skill Bar Route Presenter` 可回到旧 Slot 栏；**施法管线无法**用 Inspector 关断。

---

## 7. 签收标准（Definition of Done）

满足以下全部项即视为 **SkillRouteUnit 框架验收通过**：

- [ ] §1.1 M1–M4 完成，`definition` 全非空  
- [ ] §1.2 C1–C2 通过  
- [ ] §3.1 P0-1～P0-6 全部通过  
- [ ] §3.2 P1-1～P1-2 通过（多 Route 技能必测）  
- [ ] §3.3 P2-1～P2-2 通过（项目含 Charge 时）  
- [ ] §3.4 P3-1～P3-2 通过  
- [ ] Git tag `post-skill-route-definition` 已打（或等价 release 分支）  
- [ ] 已知问题记入 `HEARTBEAT.md` 或 issue（若有）

---

## 8. 面试口述（30 秒）

「输入只表达 Entry；`SkillRouteUnit` 是 Loadout 最小路由单位，指向 `SkillRouteDefinitionSO` 承载 Route 级 CD 与 Stages；RouteResolver 按 Charge&gt;Combo&gt;Normal 选路；Charge 用 `MotionPlaybackContext`；多段用 Transition + MarkSession；HUD 由 `SkillBarRoutePresenter` 按 `showOnHud` 动态生成 `RouteWidget`；无运行时双轨，迁移靠 Editor 菜单与 git tag 回滚。」

---

## 附录 A — 关键类型速查

| 类型 | 路径 |
|------|------|
| `SkillRouteUnit` | `4_Data/1.Skills/Routes/SkillRouteUnit.cs` |
| `SkillRouteDefinitionSO` | `4_Data/1.Skills/Routes/SkillRouteDefinitionSO.cs` |
| `SkillRouteFeatures` | `4_Data/1.Skills/Routes/SkillRouteFeatures.cs` |
| `RouteResolver` | `2_Framework/Skill/Routes/Resolver/RouteResolver.cs` |
| `SkillRouteService` | `2_Framework/Skill/Routes/Runtime/SkillRouteService.cs` |
| `SkillBarRoutePresenter` | `3_Gameplay/UI/Presenters/SkillBarRoutePresenter.cs` |

## 附录 B — 运行时 SkillDataSO 扫描结论（2026-05-15）

| 区域 | 是否仍引用 SkillDataSO | 说明 |
|------|------------------------|------|
| `3_Gameplay` / `2_Framework` 战斗帧 | **否** | 已切 `ActiveRoute` / `definition` |
| `SkillLoadoutSO` bindings | 仅迁移 / OnValidate | `entries` 为权威 |
| `Editor/*` | 是 | Migrator、Legacy Inspector |
| `SkillDataSO` 资产 | 保留 | `[Obsolete]`，作迁移源 |

*文档随 Ver4.6.1 最终形态维护；旧版 OpenClaw 分析文内 `skillData` 表述仅供参考，以本文为准。*
