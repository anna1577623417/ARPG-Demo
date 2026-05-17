> 最后更新：2026-05-17 23:15
> 产出时间：2026-05-17 22:30

# SkillRoute 最小单位重构 — 经验教训与 AI 落位效能总结

> 基于本对话上下文（语义层 112.1、Combo 连段、CD/资源结算、Normal 抢占、窗口锚点等）的严肃反思。  
> 目标：下一次「又好又快」地升级系统语义，而不是堆更多补丁。

---

## 一、结论（先讲结果）

| 维度 | 事实 |
|------|------|
| **架构方向** | SkillRoute 作为技能最小单位、Entry 仅键位、Semantic 单层翻译 — **方向正确** |
| **交付结果** | 蓝图功能**未全部跑通**；Charge 单链路、Combo CD/资源、连段窗口等经历多轮「拆东墙补西墙」 |
| **主要损耗** | 不是「缺代码」，而是 **双轨拖延 + 阶段过宽 + 兜底修 bug + 附属面（Editor/HUD）未纳入切片** |

**核心教训（2026-05-17 修订）**：大重构的 Done 是 **蓝图 → N 次合理落位 → 尽快可验证 → 逐步修握手与小 bug**。单次落位完成时：**设施已搭好**（类型/单轨骨架在），允许 **尚未通电**（调用/事件/资产未接），但必须 **可观测**（Log 能定位断点）。**切片闭环** 才要求 Play Mode 最小验收通过。禁止用双轨或 fallback 假装通电。

---

## 二、你的五条判断 — 对话证据与深化

### 1. 双轨保守式推进 — 严重拖垮进度与结构

**现象（本对话）**

- 曾出现 `UseRouteRuntime`、Resolve 时读旧 hold、Normal 兜底挡连段等 **两套真路径**。
- Legacy 语义残留在：Inspector 下拉（RouteStart / FirstStageEnd）、过时 Tooltip、Editor 字段与运行时枚举不一致。
- 新链路长期 **没有一条 LM 槽位可从头玩到尾**（Tap → Combo ABC → 容器 CD → 填充 Normal），却在加 Semantic、Transition 边、First/Last SubRoute 等多层概念。

**机理**

- 双轨让 AI 与人类都在猜「当前权威路径是哪条」，测试无法收敛。
- 「保守」变成 **永远不删旧轨**，旧测试、旧 Editor、旧 HUD 继续引用旧心智模型。

**应改为**

- **单轨 + git tag**（项目已有 `07-single-track-migration.mdc`，本迭代执行不彻底）。
- 旧类型 **只出现在 Editor 迁移工具**，不进 `PlayerStateManager` 主帧序。
- 每个 Phase 的 Done 定义：**旧调用点数为 0**，而不是「新文件已创建」。

---

### 2. 重构连带面爆炸 — SO / Editor / HUD 未纳入同一施工面

**现象**

- Route 改完后：`ComboRouteDefinitionEditor`、Transition 边、`RouteWidget` CD 环、`SkillBarRoutePresenter` 等 **并行失效或半失效**。
- SO 上 `cooldownPolicy` 枚举与运行时结算点（`OnExit` / `BeginSession` / `LastSegmentEnd`）**长期不一致**。
- 人类在 Inspector 选「On First Stage End」，运行时却在另一条分支结算 — **配置不可信**。

**应改为**

- 重构蓝图必须带 **「配置面 → 运行时锚点」对照表**（一张表，不是长文）：

| Inspector 选项 | 唯一运行时锚点 | 谁负责调用 |
|----------------|----------------|------------|
| On Last SubRoute End | `ComboRouteRuntime.ApplyLastSubRouteSettlement` | `EndComboSession` |
| Combo Window | `LastSegmentEndTime` 起算 | `NotifySubRouteSegmentEnded` |

- **改枚举 / 改结算 / 改 Editor 文案** 必须同一 PR 完成，禁止「运行时先改、Editor 下周再改」。

---

### 3. 阶段过长、系统过多 — 注意力丢失

**现象（本对话任务链）**

112.1 语义层 C–F → Combo 边 Transition → CD First/Last SubRoute → 连段 AAABC → Normal 抢占 → 窗口从段结束计时 → 变量重复声明 …  
**多条线并行**，没有一条「LM 默认配置可完整打一套」的稳定验收。

**未闭环能力（对话末仍可能存在）**

- Charge 单链路蓄力 / 解冻
- 蓝图里其它 Entry（Q MultiStage、Directional 等）
- EditMode 测试与运行时日志的 **契约不一致**

**应改为**

- **WIP 上限**：同一迭代最多 **1 个主切片 + 1 个附属切片**（例：主 = LM Combo；附 = Combo Inspector）。
- 切片验收脚本（Play Mode 60 秒）写在 Phase 表头，AI 不得标记 Done 除非用户或日志验收通过。

**推荐切片顺序（Skill 域）**

1. LM：`Tap → Combo ABC → 容器 CD`（无 Charge、无 Normal 填充）
2. + Entry Normal 仅容器 CD 期
3. + Charge
4. + Q MultiStage / Directional
5. HUD / 全 Entry 资产批处理

---

### 4. 兜底式修 BUG — 拆东墙补西墙

**本对话典型兜底（已发生，部分已改回系统式）**

| 兜底写法 | 问题 | 系统式替代 |
|----------|------|------------|
| 「有 Combo 就禁止一切 Normal fallback」 | CD 期无法填充普攻 | **CD 感知分流**：Session 内只走 chain；容器 CD 只走 Entry.Normal |
| `EndComboSession` on Tap idx=0 during session | 误杀 Session 后落 Normal | **删除**；用序号单调 + 段结束锚点 |
| `COERCE comboIdx` 但不改 gap 锚点 | 仍被 edge max 0.5s 杀 Session | **窗口从段结束计时** + Session 内不判 edge max |
| `TryApplyCooldownPolicy` 在 FirstSubRoute 兼容 OnRouteStart | 第一段完就 CD | **结算点专用 API**，BeginSession 只扣资源 |
| `_justResolvedComboLastChild` 在 Enter 时算 | 一段完就 EndCombo | **Exit 时按 ComboIndex 与 ChainLength 判定** |

**识别兜底的信号（AI 自检）**

- 新增 `if (hasX) return null` 阻止落另一条 Route，但 **未说明两条 Route 的优先级契约**。
- `REJECT … no Y fallback` 类日志 — 多半是补丁。
- 同一 bug 修两次且改在不同文件（Resolver + Service）— 缺 **单一真相层**。

**系统式修复模板**

1. 写清 **时间与序号契约**（一页纸）。
2. 只在一个层写状态（例：Session 段位只在 `ComboRouteRuntime` + `NotifyRouteEntered` 提交）。
3. 其它层 **只读** 状态，不「纠正」状态（避免 COERCE 与 END 打架）。

---

### 5. 工程卫生 — 命名、Tooltip、未初始化变量

**本对话**

- `CS0136 sessionActive` 重复声明 — 作用域叠层未清理。
- `CS0122` / `ResetResourceConsumeFlags` 保护级别 — 子类合理访问未提升。
- Tooltip 内嵌 `"` 导致 Unity 序列化或文案混乱 — 应用 **【】** 标注（你的规范应写入 Rule）。

**应改为（AI 写 Unity 代码时强制）**

- 改函数签名时 **全仓编译级搜索** 调用方（含测试）。
- 新增 `out` / 重载时检查 **外层是否已有同名局部变量**。
- `[Tooltip]` 禁止嵌套英文双引号；用 `【】` 或「」；多行用 `\n`。
- 子类要调用的基类钩子 → 直接 `protected`，不要先 `private` 再被打脸改。

---

## 三、本迭代「结构性」失误（超越单条 bug）

### 3.1 缺少「结算与窗口」的单一真相

连段失败的主因不是「没写 Combo」，而是：

- **窗口起点**：松手 / CommitAdvance / 段结束 — 三者在不同阶段被混用。
- **CD 起点**：SubRoute OnExit / 容器 OnExit / FirstSubRoute — 多次误触发。
- **序号**：Resolver 私有 `ComboIndex` vs Service `ComboIndex` vs Session — 分裂导致 AAABC、Tap(1) 无 Session。

**今后强制**：每个「可配置时机」枚举值，在代码里 **只有一个 public 入口函数** 会触发它。

### 3.2 语义层与战斗层边界被反复穿透

112.1 设计是 Semantic 不分技能；但多次在 Service 用 hold 推断、用 Normal 兜底 Combo，等于 **第二层语义**。

**今后**：`SkillEntryService.TryResolveForIntent` 只读 `intent.Semantic / ComboIndex`；窗口与 gap 只读 `ComboRouteRuntime.LastSegmentEndTime`。

### 3.3 验收标准错配

- 文档 Phase 多 ≠ 可玩。
- 日志「看起来能解析」≠ Session 正确推进。

**最小可玩验收（建议写入每个 Skill PR）**

```
[ ] LM 三连段 ABC，容器 CD 1.8s 仅 C 后触发
[ ] CD 中 Tap 仅 Entry.Normal（若配置了）
[ ] CD 结束新开 ABC，SESSION START 出现
[ ] 段中抢按：DROP，不 END Session
[ ] 无 SESSION END … gap > max 0.5s（在段结束后 0.5s 内按可接 B）
```

---

## 四、对 AI 落位效能的具体建议（又好又快）

### 4.1 开工协议（15 分钟，必须落盘或贴进 Issue）

1. **切片名** + **Landing k/N** + 明确 **本落位不做什么**。
2. **设施表**：本切片结束前必须存在的类型/主循环点。
3. **握手表**：本落位 WIRE 行 + OPEN 行 + 每行 **验收 Log**。
4. **最早 Play Mode 验证点**（第几次 Landing 必须能进场景看 Log）。
5. **单轨声明**：旧 API 删除列表（文件名级）。
6. **Slice 闭环** 才用的验收 Log（3～5 条）。
7. **git tag 名**（回滚点）。

### 4.2 每个 PR 只允许一类变更

| PR 类型 | 允许 | 禁止 |
|---------|------|------|
| 契约 | 枚举、结算点、窗口锚点 | 顺手改 HUD |
| 接线 | PlayerStateManager、Service | 新 Phase 文档 |
| 资产/Editor | Inspector、迁移脚本 | 改 Runtime 逻辑 |
| 修 bug | 根因层单点 | 多加一条 fallback |

### 4.3 AI 输出顺序（减少返工）

1. **时间与状态契约**（表格式）  
2. **改哪些文件、删哪些文件**  
3. **实现**  
4. **验收日志对照** — 不要先写 500 行实现再补契约。

### 4.4 禁止模式清单（命中即停笔改方案）

- `if (legacy) / UseRouteRuntime / allowFallback`
- `REJECT … no Normal fallback` 且无 CD/session 分流表
- 在 Resolver **写** ComboIndex，在 Service **又写** ComboIndex（只能 Service 提交）
- 在 Enter 判定「是否最后一段」
- 用「禁止落 Normal」代替「Normal 与 Combo 的优先级契约」

---

## 五、仍未完成项（诚实边界）

以下在对话末 **不能声称已闭环**，应列入下一切片：

- Charge 全链路（Press 即 Charge、Release 解冻、与 ActionState 边沿一致）
- 蓝图全部 Entry / Route 类型的 EditMode 矩阵
- HUD 与容器 CD / Session 窗的一致性展示
- `RouteResolver` 与 `SkillEntryService` 是否完全单轨（历史注释仍提双路径）
- 全量资产 `comboTransitions` 长度与链节点数校验自动化

---

## 六、面试口述版（30 秒）

「我们把技能最小单位收成 SkillRoute + Entry 键位 + Semantic 输入层，战斗帧只保留一条解析路径。这次迭代说明：大重构必须先打通垂直切片，结算点和窗口起点要有唯一 API；双轨和兜底 fallback 会指数级增加 debug 成本。最后用段结束时间驱动连段窗、容器 CD 只在 Last SubRoute 收口，才从根本上消掉 AAABC 和一段进 CD。」

---

## 七、关联规则（已升级）

- `.cursor/rules/07-single-track-migration.mdc` — 反面教材 + 指向落位/通电模型
- `.cursor/rules/09-refactor-delivery-protocol.mdc` — **Landing / Slice 两层 Done**、设施+握手表、通电 Log、通路修复顺序、蓝图→N 次落位
- `.cursor/rules/06-skill-route-single-track.mdc` — Skill 域时间与结算约定
