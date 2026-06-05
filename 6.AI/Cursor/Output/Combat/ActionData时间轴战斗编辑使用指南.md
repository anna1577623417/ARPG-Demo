> 产出时间：2026-06-02 21:03

# ActionData 时间轴战斗编辑使用指南

> **适用版本**：139.2 Action Timeline Editor + 137.1 打断单轨 + 136.1 CombatFlow  
> **读者**：技能策划、战斗策划、TA  
> **目标**：在 **ActionData 时间轴** 上正确配置打断窗口、战斗状态标签、HitFrame；理解 Scene 预览里「圈圈 / 方框 / 黄线」的含义；并与 **CombatFlow**（技能流转）分工协作。

---

## 目录

1. [快速入口](#1-快速入口)
2. [先搞清「三层」——别在一条轨里混三件事](#2-先搞清三层别在一条轨里混三件事)
3. [编辑器布局与基本操作](#3-编辑器布局与基本操作)
4. [轨道一览（15 轨）](#4-轨道一览15-轨)
5. [打断窗口（Interrupt）——验收重点](#5-打断窗口interrupt验收重点)
6. [CombatFlow 与时间轴的关系](#6-combatflow-与时间轴的关系)
7. [战斗状态窗口：Hitbox / Hurtbox / 无敌 / Phase](#7-战斗状态窗口hitbox--hurtbox--无敌--phase)
8. [攻击盒 vs 受击盒 vs HitShape（两套体系）](#8-攻击盒-vs-受击盒-vs-hitshape两套体系)
9. [Runtime Event 与 HitFrame](#9-runtime-event-与-hitframe)
10. [Scene 预览：圈圈、方框、黄线是什么意思](#10-scene-预览圈圈方框黄线是什么意思)
11. [完整工作流示例](#11-完整工作流示例)
12. [Play Mode 验收与 Debug 开关](#12-play-mode-验收与-debug-开关)
13. [常见错误与排查](#13-常见错误与排查)
14. [附录：字段与运行时对照表](#14-附录字段与运行时对照表)

---

## 1. 快速入口

| 方式 | 操作 |
|------|------|
| 菜单 | `Tools → GameMain → Action Timeline Editor` |
| Inspector | 选中 `ActionDataSO` → **Timeline** 折叠 → **打开 Action 时间轴编辑器** |
| 自动绑定 | 时间轴窗口已打开时，在 Project 里点选另一个 `ActionDataSO` 会自动切换 |

**前置条件**

- 动作已绑定 `MainClip`（Pose 预览需要）
- 该 `ActionDataSO` 已被某个 `SkillStageDefinition.action` 引用（Play Mode 才会跑到）
- Scene 预览需要 **Gizmo Anchor**（见 §3.4）

---

## 2. 先搞清「三层」——别在一条轨里混三件事

策划常问：「圈圈是 Hitbox 吗？」「打断和 CombatFlow 是不是一回事？」  
答案：**不是一层，是三个独立契约**。

```text
┌─────────────────────────────────────────────────────────────────┐
│  Layer A · Action 时间轴（ActionDataSO）                          │
│  回答：在这一动作的 nt=0~1 上，Phase/Hitbox/打断窗口 何时生效？   │
│  数据：ActionWindow / TeleportTrigger / TimelineMarker          │
│  运行时：EvaluatePhaseTags → State 轨；ActionTimelineRuntime    │
└─────────────────────────────────────────────────────────────────┘
                              ↓ 与下面两层正交
┌─────────────────────────────────────────────────────────────────┐
│  Layer B · 打断仲裁（ActionInterruptResolver）                   │
│  回答：当前动作播到 nt 时，新意图能不能「硬切」掉它？              │
│  数据：ActionWindow.InterruptibleByCategories + 动作级 Priority   │
│  运行时：PlayerActionState.TryConsumeGameplayIntent             │
└─────────────────────────────────────────────────────────────────┘
                              ↓ 与下面一层正交
┌─────────────────────────────────────────────────────────────────┐
│  Layer C · 技能流转（CombatFlow + AbilityGate）                  │
│  回答：从 Idle / 上一 Route 结束后，能不能「起手」或「切边」到某技能？│
│  数据：Loadout.combatFlow、AbilityMap、Route.abilityGateRules     │
│  运行时：CombatGraphRuntime → SkillEntryService                   │
└─────────────────────────────────────────────────────────────────┘
```

| 你想配的东西 | 去哪里配 | **不要**去哪里配 |
|-------------|---------|-----------------|
| 后摇能闪避取消 | Action 时间轴 **Interrupt** 轨 | CombatFlow 边条件 |
| 空中不能放某 Route | Route **abilityGateRules** / AbilityMap | ActionWindow |
| 连段 A→B 自动衔接 | **CombatFlow** 边 | Interrupt 窗口 |
| 判定段开 Hitbox 标签 | **Hitbox ★** 轨 | SkillStage  alone |
| 真正 overlap 检测形状 | **SkillStage.HitShapeSO** | Scene 红方框（仅示意） |

**记忆口诀**

- **时间轴** = 这一刀「什么时候处于什么战斗状态」  
- **Interrupt** = 这一刀「什么时候允许被别的事打断」  
- **CombatFlow** = 技能图「允许从哪走到哪」

---

## 3. 编辑器布局与基本操作

### 3.1 窗口结构

```text
┌─ 工具栏：Action 引用 | 保存 | 刷新 | 预览 | 缩放 ─────────────────┐
├─ 动作摘要：Category / MainClip / Duration / 优先级 / 强韧 / 自打断 ─┤
├─ 预览条：预览 slider | Pose ☑ | Scene ☑ ────────────────────────────┤
├──────────────────────────────┬───────────────────────────────────────┤
│  左栏 · 时间轴（15 轨）       │  右栏 · 属性                          │
│  标尺 0.0 … 1.0              │  选中 Window / TP / Marker 的字段      │
│  黄线 = 当前预览时刻          │  快捷添加（预览时刻）                  │
├──────────────────────────────┴───────────────────────────────────────┤
│  状态栏：Clip 长度 | t= | Track | Gizmo Anchor ───────────────────────│
└──────────────────────────────────────────────────────────────────────┘
```

- **归一化时间 `t`**：整段动作 `0~1`，与 `ActionDataSO.Duration` 对应，**不是** Clip 原始秒数（Clip 对齐在 Time Authority 另配）。
- **吸附步长**：`0.01`（1% 动作长度）。
- **默认新建战斗片段长度**：`0.12`（12% 动作）。

### 3.2 鼠标操作（左栏时间轴）

| 操作 | 效果 |
|------|------|
| 点击标尺 / 拖拽黄线 |  scrub 预览时刻 `t` |
| 单击片段 | 选中；右侧显示属性 |
| 拖拽片段中间 | 平移整段 `[Start, End]` |
| 拖拽片段左右白边 | 改起点 / 终点 |
| **双击空白轨** | 在该轨、该时刻创建默认片段 |
| 点击轨名标签 | 切换「焦点轨」（快捷添加用） |

右栏迷你工具栏：

- **`+ Window`**：在 0~0.25 加空白 Window（需手动加标签）
- **`−`**：删除选中 Window
- **`+ TP`**：在预览时刻加瞬移点
- **`+ ◆`**：在预览时刻加标记（类型随焦点轨：FX/SFX/Camera 等）

### 3.3 预览开关

| 开关 | 作用 |
|------|------|
| **预览** slider | 驱动时间轴黄线 + Scene 同步 |
| **Pose** | `AnimationMode` 采样 `MainClip` 到 Gizmo Anchor 骨骼 |
| **Scene** | Scene 视图绘制 Handles（Hitbox 方框、圈圈、标记等） |

两者都开 = **所见即所得**：拖 slider 看_pose + Gizmo 是否对齐。

### 3.4 Gizmo Anchor（Scene 预览锚点）

状态栏右侧 **Object 字段**：

1. 拖入场景里 Player 的 `Transform`，或  
2. 留空 → 自动用 **当前 Selection** 或场景里第一个 `Player`

未指定 Anchor 时 Scene 左上角提示：**请指定 Gizmo Anchor 或选中 Player**。

---

## 4. 轨道一览（15 轨）

所有 **Window 轨** 底层都是同一条 `ActionDataSO.Windows` 列表；不同轨只是**按标签过滤显示**，避免一条轨堆所有语义。

### 4.1 战斗核心轨（加粗 ★）

| 轨名 | 颜色 | 创建预设 | 写入 StateTag |
|------|------|----------|---------------|
| **Hitbox ★** | 红 | Hitbox + PhaseActive | `HitboxActive_Window` |
| **Hurtbox ★** | 紫 | Hurtbox | `HurtboxActive_Window` |
| **Invincible ★** | 黄 | Invuln | `Invulnerable` |

### 4.2 Phase 轨

| 轨名 | 颜色 | StateTag |
|------|------|----------|
| Phase · Startup | 蓝 | `PhaseStartup` |
| Phase · Active | 绿 | `PhaseActive` |
| Phase · Recovery | 紫 | `PhaseRecovery` |

Phase 表示**战斗阶段语义**（前摇 / 判定 / 后摇），供条件系统、Debug、后续扩展读取；与 Interrupt 轨独立。

### 4.3 打断轨

| 轨名 | 颜色 | 关键字段 |
|------|------|----------|
| **Interrupt** | 橙 | `InterruptibleByCategories`（默认预设 Movement） |

**注意**：Interrupt 轨上的片段**不要求**勾选 Hitbox/Phase 标签；它只负责「谁能打断我」。

### 4.4 其它 Window 轨

| 轨名 | 用途 |
|------|------|
| Combo Input | `ComboInput_Window` — 连段输入缓冲窗 |
| Root Motion | `RootMotion_Window` — 根运动相关时间语义 |
| Runtime Events | 仅显示带 `RuntimeEvents` 列表非空的 Window |

### 4.5 非 Window 轨（◆）

| 轨名 | 数据列表 | 说明 |
|------|----------|------|
| Teleport ◆ | `TeleportTriggers` | 归一化时刻 + 平面距离(m) |
| FX ◆ / Audio ◆ | `TimelineMarkers` | SpawnVfx / PlaySfx |
| Camera | `TimelineMarkers` | Shake / Push / Lock |
| TimeScale | `TimelineMarkers` | HitStop / SlowMo / BulletTime |

---

## 5. 打断窗口（Interrupt）——验收重点

### 5.1 判定链（运行时）

`ActionInterruptResolver.CanInterrupt` 顺序：

```text
1. 硬优先级：incomingPriority > action.InterruptStability → 允许（无视窗口）
2. 否则：找当前 nt 覆盖的所有 Window
3. 跳过 InterruptibleByCategories == None 的 Window
4. 检查：
   - 来袭类别 ∈ 窗口允许的 Categories（Flags）
   - incomingPriority >= MinIncomingPriority
   - 自打断：isSelf 时需 window.AllowSelfInterrupt 或 action.AllowSelfInterrupt
5. 任一 Window 通过 → 允许打断
```

### 5.2 动作级字段（动作摘要折叠）

| 字段 | 含义 |
|------|------|
| **Category** | 本动作属于 Movement / Offense / Defensive / Utility（来袭判定时读 **incoming Action** 的 Category） |
| **优先级 (InterruptPriority)** | 本动作作为**来袭者**的优先级 |
| **强韧 (InterruptStability)** | 本动作作为**被断者**的霸体线；来袭优先级必须 **严格大于** 此值才能硬断 |
| **自打断 (AllowSelfInterrupt)** | 动作级允许同 Action 重入 |

### 5.3 窗口级字段（选中 Interrupt 片段 → 右栏「打断」）

| 字段 | 含义 |
|------|------|
| **允许类别** | `Movement` / `Offense` / `Defensive` / `Utility` 多选 Flags |
| **最低优先级** | 来袭动作的 `InterruptPriority` 必须 ≥ 此值 |
| **允许自打断** | 本窗口内是否允许同动作再次 Enter |

### 5.4 类别与默认优先级（无 Action 引用时）

| ActionCategory | 典型入口 | Resolver 默认来袭优先级 |
|----------------|----------|-------------------------|
| Movement | Shift / Space 映射槽 | 30 |
| Offense | LM / RM 等攻击槽 | 20 |
| Defensive | — | 40 |
| Utility | — | 10 |

### 5.5 配置示例

**普攻后摇可被闪避取消**

1. 在 **Phase · Recovery** 或 **Interrupt** 轨画 `[0.55, 0.85]` 片段（Recovery 仅语义，Interrupt 才管打断）。
2. 选中 **Interrupt** 片段（或同区间单独 Interrupt 窗）：
   - 允许类别：`Movement`
   - 最低优先级：`0` 或 `30`（视闪避 Action 的 Priority）
3. 闪避 `ActionDataSO`：`Category = Movement`，`InterruptPriority ≥ MinIncomingPriority`。
4. **CombatFlow / Route** 仍需能解析到闪避技能（Interrupt 只解决「能不能断」，不解决「闪避 Route 是否存在」）。

**霸体招式**

- 动作级 `InterruptStability = 50`  
- Recovery 段 **不** 开 Interrupt 窗，或只开 `Defensive` 且提高 `MinIncomingPriority`。

### 5.6 已废弃字段（勿用）

- `ActionWindow.incomingAbilityGate` — **137 终态已废弃**；地面/滞空等用 Route `abilityGateRules` 或 Loadout `AbilityMap`。
- `StateTag.AllowInterruptByDodge` 等 legacy 位 — 新资源用 **InterruptibleByCategories**，Inspector 会提示迁移。

---

## 6. CombatFlow 与时间轴的关系

**CombatFlow 不在 Action 时间轴里编辑**，但在「技能验收」里必须与 Interrupt 一起测。

### 6.1 数据挂载

```text
SkillEntryLoadoutSO
  ├─ combatFlow → CombatGraphAsset（节点 + 边）
  ├─ AbilityMap → 槽位语义 → AbilityGateRuleSO
  └─ entries[] → SkillRouteDefinition
```

### 6.2 运行时顺序（简化）

```text
输入 Intent
  → SkillEntryService.Resolve
      → Primary Route 解析
      → 失败则 CombatGraphRuntime.TryResolve（Flow 边）
  → AbilityGateService（Route 起手 / Flow 切边）
  → PlayerActionState Enter

动作进行中再次输入
  → ActionInterruptResolver（只看 ActionWindow + Priority）
  → 成功则 Exit 当前 Route → 重新 Route
```

### 6.3 策划分工表

| 需求 | Action 时间轴 | CombatFlow | Route / AbilityMap |
|------|--------------|------------|-------------------|
| Idle 按 LM 起普攻 | — | Idle → Attack 边 | Entry 绑定 Route |
| 普攻结束自动接下一招 | — | Attack → Attack2 边 + 条件 | MultiStage / Combo Route |
| 后摇按 Shift 闪避 | **Interrupt 窗** | 边可选（若从 Action 态回 Idle 再 Dodge 则不需要边） | Dodge Route + Gate |
| 空中禁止某技能 | — | 边条件 `Airborne` | Route.gate grounded-only |

**Editor 入口**：`CombatGraphAsset` 自定义 Inspector（`CombatGraphAssetEditor`）；生成 MVP：`CombatFlowMvpGenerator` 菜单工具。

---

## 7. 战斗状态窗口：Hitbox / Hurtbox / 无敌 / Phase

### 7.1 机制

运行时 `ActionDataSO.EvaluatePhaseTags(nt, ref mask)`：

- 遍历所有 `Windows`，若 `Start ≤ nt ≤ End`，将其 `WindowSlotMask` 映射的 StateTag **按位 OR** 进 mask。
- 多窗重叠 → **标签叠加**（不是互斥）。

### 7.2 各标签战斗含义

| StateTag | 策划语义 | 典型用途 |
|----------|----------|----------|
| `PhaseStartup` | 前摇 | 不可命中、可被打断 |
| `PhaseActive` | 判定段 | 与 Hitbox 窗配合 |
| `PhaseRecovery` | 后摇 | 可取消、可被打断 |
| `HitboxActive_Window` | 攻击方判定窗 | 配合 HitFrame 做伤害 |
| `HurtboxActive_Window` | 受击方暴露窗 | 标记可被命中（逻辑 Tag，非碰撞体） |
| `Invulnerable` | 无敌 | 闪避无敌帧、起跳无敌 |
| `ComboInput_Window` | 连段输入缓冲 | Combo Route 输入窗 |
| `RootMotion_Window` | 根运动窗 | 与 Motor 根运动策略配合 |

### 7.3 编辑方式

**方式 A — 双击轨 / 快捷添加**

- 右栏 **快捷添加 → Hitbox / Hurt / Invuln**（在**当前预览时刻**插入 0.12 长度片段）

**方式 B — 属性面板**

- 选中 Window → **战斗轨快捷开关**（Hitbox / Hurtbox / Invuln / Startup / Active / Recovery）
- 或 **战斗状态（Hitbox / Hurtbox / …）** 折叠里勾 Slot Mask

**方式 C — 拖时间**

- 先画长 Window，再拖边缘对齐动画关键帧（配合 Pose 预览）

---

## 8. 攻击盒 vs 受击盒 vs HitShape（两套体系）

这是最容易混淆的点：**时间轴 Tag ≠ 物理/overlap 形状**。

### 8.1 时间轴层（ActionWindow → StateTag）

| 概念 | 编辑器表现 | 运行时 |
|------|-----------|--------|
| **Hitbox 窗** | 时间轴红条；Scene **红色线框方盒**（约 1.2×1.8×1.4m，锚点前向 1.1m） | 写 `HitboxActive_Window` 到 State 轨 |
| **Hurtbox 窗** | 时间轴紫条；Scene **紫色线框球**（r≈0.55m 躯干） | 写 `HurtboxActive_Window` |

→ **Scene 方框/球是示意体积**，方便对齐动画，**不是**最终 HitDetection 碰撞体。

### 8.2 伤害层（SkillStage → HitShapeSO）

| 概念 | 配置位置 | 运行时 |
|------|----------|--------|
| **HitShape** | `SkillStageDefinition` → `HitShape`（Box/Sphere/Capsule/Cone SO） | HitFrame 触发时 overlap → `DamagePipeline` |
| **DamageSheet** | Route / Stage | 伤害数值、元素等 |
| **TargetFilter** | Stage | 过滤目标阵营/标签 |

**正确做法**

1. 时间轴：Hitbox 窗覆盖「可能造成伤害」的 nt 区间。  
2. 时间轴：在窗内 **Runtime Event → HitFrame**（或 Timeline Marker HitFrame）标精确出手帧。  
3. Skill Stage：绑 `HitShapeSO` 定义真实检测形状与偏移。  
4. Play Mode：看伤害 Log / 受击反馈，而非只看 Scene 红盒。

### 8.3 受击盒说明

当前工程 **没有独立 Hurtbox Collider 组件**；`HurtboxActive_Window` 是 **State 语义**（「此时角色受击逻辑开放」），Scene 紫球仅为预览。  
是否可被命中仍由目标侧 Status、Invulnerable、DamagePipeline 综合判定。

---

## 9. Runtime Event 与 HitFrame

### 9.1 两种 HitFrame 配置路径

| 路径 | 数据 | 适用 |
|------|------|------|
| **Window 内事件** | `ActionWindow.RuntimeEvents` → `Kind=HitFrame` | 与 Hitbox 窗同段、偏移 `NormalizedOffset` |
| **Timeline Marker** | `TimelineMarkers` → `Kind=HitFrame` | 独立于 Window 的瞬时标记 |

### 9.2 Window Event 时间计算

```text
eventTime = Lerp(windowStart, windowEnd, NormalizedOffset)
```

- `Offset=0` → 窗起点  
- `Offset=1` → 窗终点  
- 运行时 `ActionWindowTimelineEvents.AppendEventsOnCrossing` 在 nt 穿越 eventTime 时触发一次。

### 9.3 Runtime Events 轨

仅展示 **RuntimeEvents 列表非空** 的 Window（黄色条）。  
编辑：选中 Window → 右栏 **Runtime Event（窗内事件）** 展开列表。

### 9.4 与 Hitbox 窗的配合建议

```text
nt:  0.0 ── Startup ── 0.25 ── Active+Hitbox ── 0.45 ── Recovery ── 1.0
                              ↑ HitFrame @ offset 0.35~0.5
```

- Hitbox 窗：略宽于真实判定（缓冲）  
- HitFrame：对齐武器最快点的一帧  

---

## 10. Scene 预览：圈圈、方框、黄线是什么意思

Scene 预览由 `ActionDataTimelineSceneBridge` 绘制，**全部是 Handles 示意**，与 Unity Layer / Sorting 无关。

### 10.1 一览表

| 视觉 | 颜色/形状 | 含义 | 对应数据 |
|------|-----------|------|----------|
| **黄虚线 + 小球** | 黄 | 预览 playhead；线上 `t=0.xx` | 预览 slider |
| **脚下同心圆环（可多层）** | 半透明青 | **当前 t 下所有激活的 ActionWindow**；每多一个重叠 Window 多一圈（半径 +0.04m） | `Windows[]` 时间覆盖 |
| **红色线框方盒** | 红 | **HitboxActive_Window 激活** | Hitbox 轨片段 |
| **紫色线框球（三圆环）** | 紫 | **HurtboxActive_Window 激活** | Hurtbox 轨片段 |
| **黄色小球（头上）** | 黄 | **Invulnerable 激活** | Invincible 轨片段 |
| **青色/白色箭头线** | 青/白 | Teleport 触发；预览时刻接近时变白高亮 | `TeleportTriggers` |
| **彩色小球 + 虚线区间** | 各色 | Timeline Marker；白=当前激活；Zone 型显示持续时间 | `TimelineMarkers` |
| **头顶白字** | 白 | `ActionName t=0.xx` | — |

### 10.2 「圈圈」到底是几层？

**两种「层」要分开理解：**

#### A. Scene 里的青色同心圆 —— 「Window 覆盖层」

- **不是** Hitbox。  
- **不是** 三层 Phase。  
- 含义：**此刻有哪些 Window 片段覆盖当前 t** —— 有几个 Window 重叠，就画几个同心圆环。  
- 只有 Hitbox 窗激活时，**额外**再画红色方盒；Invuln 再画头上黄球。

```text
示例：t=0.4 同时处于 PhaseActive + Hitbox + Interrupt 三个 Window
  → 3 个青色同心圆 + 1 个红色方盒（无 Invuln 则无黄球）
```

#### B. 设计上的「三层 Phase」—— Startup / Active / Recovery

- 对应时间轴 **三条 Phase 轨**（蓝 / 绿 / 紫条）。  
- 与青色圆环**无直接一一对应**；Phase 只是 Window 上挂了 `PhaseStartup` 等标签后在对应轨显示。  
- 推荐：**Phase 三段时间尽量不重叠**；Hitbox 窗常与 **Phase · Active** 对齐。

#### C. 架构「三层契约」—— 见 §2

- 时间轴 Tag / Interrupt / CombatFlow —— **逻辑分层**，不是 Scene 里画三层圈。

### 10.3 Marker 颜色（Scene 小球）

| Kind | 颜色倾向 |
|------|----------|
| SpawnVfx | 粉 |
| PlaySfx | 浅蓝 |
| CameraShake / Push / Lock | 蓝系 |
| TimeScaleHitStop / SlowMo / BulletTime | 橙 / 黄 / 红 |

Zone 型 Marker（SlowMo、CameraPush 等）：虚线连接 `[t, t+Duration]`。

### 10.4 Pose 预览注意

- 依赖 `MainClip` + Gizmo Anchor 骨骼。  
- **HitShape 不会自动对齐武器模型**；方盒是固定近似尺寸。  
- 141.1 已知限制：极端缩放角色、无 Humanoid 绑定时 Pose 可能偏差 —— 以 nt 时间轴为准校准。

---

## 11. 完整工作流示例

### 11.1 单手剑三连 · 第一刀

**目标**：前摇不可打断 → 判定开 Hitbox + HitFrame → 后摇可闪避取消。

| 步骤 | 操作 |
|------|------|
| 1 | 打开 `Action_Sword_A` 时间轴；Duration / MainClip 已在 Time Authority 配好 |
| 2 | 动作摘要：`Category=Offense`，`InterruptPriority=20`，`InterruptStability=15` |
| 3 | `t≈0` 双击 **Phase · Startup** → 拖至 `0~0.22` |
| 4 | `t≈0.22` 快捷 **Hitbox** → 拖 `0.22~0.42`；确认带 PhaseActive |
| 5 | 选中 Hitbox Window → Runtime Event 加 **HitFrame**，`Offset=0.45`（约在窗中间偏前） |
| 6 | `t≈0.42` 画 **Phase · Recovery** `0.42~0.75` |
| 7 | 同区间画 **Interrupt** 窗：`Movement`，`MinIncomingPriority=0` |
| 8 | 指定 Gizmo Anchor；拖 slider 检查：Recovery 段无红盒、有青环+Interrupt 窗；Active 段有红盒 |
| 9 | Stage 绑 `HitShapeSO`（Box/Sphere）；Route 进 Loadout + CombatFlow |
| 10 | Play Mode：`DebugInterruptFlow` 在后摇按 Shift 看 `[Interrupt] allow=true` |

### 11.2 闪避无敌帧

| 步骤 | 操作 |
|------|------|
| 1 | Dodge Action：`Category=Movement`，`InterruptPriority=30` |
| 2 | `Invincible ★` 轨 `0.05~0.35` |
| 3 | Scene 预览：该区间头上出现 **黄色 Invuln 球** |
| 4 | Route `abilityGateRules`：Grounded；CombatFlow 从 Idle/Attack 边到 Dodge（若需要） |

### 11.3 蓄力 Hold 锚点（Charge）

- Window Event：`ChargeHoldEnter` / `ChargeHoldExit`  
- 需 Route 侧 `ChargeConfig.holdAnchorMode = ByActionWindow`  
- 与时间轴 nt 对齐蓄力循环姿势。

### 11.4 瞬移冲刺

- **Teleport ◆** 轨点击添加；属性 **时刻** + **距离 (m)**  
- Scene：青色箭头；预览时刻接近时箭头变白。

---

## 12. Play Mode 验收与 Debug 开关

### 12.1 Player Inspector Debug

| 开关 | 日志前缀 | 用途 |
|------|----------|------|
| `DebugInterruptFlow` | `[Interrupt]` | 打断 allow/deny、reason、cat、pri |
| `DebugSkillRoute` | `[SkillRoute]` / `[Flow]` / `[Resolve]` | Route 解析、CombatFlow 切边、CD |

### 12.2 推荐验收脚本

**打断窗口**

1. 进入 Play，放普攻至 Recovery。  
2. 按闪避键。  
3. 期望：`[Interrupt] allow=true ... reason=window+category`（或 hard-priority）。  
4. 在 Startup 段按闪避 → 期望 **无** allow（若未开 Interrupt 窗且优先级不够）。

**CombatFlow**

1. 仅 Idle 状态按攻击 → `[Flow]` 或 `[Resolve]` 解析到 Attack Route。  
2. Route 自然结束 → 回到 Idle 节点（视 Graph 配置）。

**HitFrame**

1. 观察伤害数字 / HitStop / 受击动画是否在 HitFrame 对应时刻触发。  
2. 与时间轴 nt 对比（`Duration * nt` 秒）。

### 12.3 Route HUD

`RouteHudDebugOverlay`（需 `DebugSkillRoute`）：显示 FSM、Active Route、Stage idx、nt —— **不画 Hitbox**，与时间轴 Scene 预览互补。

---

## 13. 常见错误与排查

| 现象 | 可能原因 | 处理 |
|------|----------|------|
| Scene 无 Gizmo | 未设 Anchor / 未开 Scene ☑ | 状态栏拖 Player Transform |
| 有青环无红盒 | 当前 t 无 Hitbox 窗 | 拖 slider 到 Hitbox 条范围 |
| 多个青环 | 多 Window 时间重叠 | 正常；检查是否误重叠 Phase |
| 后摇按闪避没反应 | 只画了 Recovery 未画 Interrupt | Interrupt 轨单独开 Movement |
| 后摇仍不能闪避 | CombatFlow/Route 未解析到 Dodge | 查 `[Resolve]`；Interrupt 通过只表示「可 Exit」 |
| Hitbox 红盒位置不对 | 预览盒固定近似 | 以 nt 为准；HitShape 在 Stage 调 |
| 有 Hitbox 无伤害 | 缺 HitFrame 或 HitShape | 加 Runtime HitFrame + Stage HitShapeSO |
| Interrupt 开了仍不能断 | `MinIncomingPriority` 过高 | 降低或提高闪避 Action Priority |
| 霸体被小招打断 | 来袭 Priority > Stability | 提高 Stability 或降低攻击 Priority |
| 用了 AbilityGate 在 Window | 字段已废弃 | 改 Route Gate / AbilityMap |

---

## 14. 附录：字段与运行时对照表

### 14.1 ActionDataSO 时间相关

| 字段 | 编辑器位置 | 运行时 |
|------|-----------|--------|
| `Duration` | Time Authority / 摘要 | `nt = elapsed / Duration` |
| `Windows[]` | 时间轴各轨 | `EvaluatePhaseTags` |
| `TeleportTriggers[]` | Teleport 轨 | `ActionTimelineRuntime` |
| `TimelineMarkers[]` | FX/Audio/Camera/TimeScale | `ActionTimelineRuntime` |
| `Category` / Priority / Stability | 动作摘要 | `ActionInterruptResolver` |

### 14.2 关键源码索引

| 模块 | 路径 |
|------|------|
| 时间轴编辑器 | `Editor/Authoring/ActionDataTimelineEditor*.cs` |
| Scene 预览 | `Editor/Authoring/ActionDataTimelineSceneBridge.cs` |
| Window 数据 | `4_Data/2.Actions/ActionWindow.cs` |
| 打断解析 | `3_Gameplay/Combat/ActionSystem/ActionInterruptResolver.cs` |
| 时间轴运行时 | `3_Gameplay/Combat/ActionSystem/ActionTimelineRuntime.cs` |
| CombatFlow | `2_Framework/Skill/Routes/Runtime/CombatGraphRuntime.cs` |
| HitShape | `4_Data/Combat/HitShape/*.cs` |

### 14.3 相关蓝图文档

| 文档 | 主题 |
|------|------|
| `6.AI/Cursor/BluePrint/139.2【蓝图】【优化】ActionData Window时间轴编辑.md` | 时间轴 V1→V2 路线图 |
| `6.AI/Cursor/BluePrint/137.1落地计划书_StateTag_打断窗口_技能流转.md` | Interrupt vs Flow 契约 |
| `6.AI/Cursor/BluePrint/136.1落地计划书_四向Group_Ability_CombatFlow装配.md` | Loadout CombatFlow 装配 |
| `6.AI/Cursor/Output/Editor/Unity编辑器工具设计要点_ActionTimeline实践总结.md` | 编辑器 UX 原则 |

---

## 面试口述总结

ActionData 时间轴用归一化 `0~1` 的 **ActionWindow** 切片描述 Phase、Hitbox/Hurtbox/无敌等 **State 时间语义**，用 **Interrupt 轨** 单独配置 `InterruptibleByCategories` 与优先级；**CombatFlow** 管技能图流转，二者不与 AbilityGate 混配。Scene 预览里 **青色同心圆 = 当前时刻激活的 Window 数量**，**红方盒/紫球/黄球 = Hitbox/Hurtbox/Invuln 标签示意**；真实命中由 **SkillStage.HitShapeSO + HitFrame 事件** 驱动。验收时开 `DebugInterruptFlow` 与 `DebugSkillRoute` 分别对照打断与 Route/Flow 日志。
