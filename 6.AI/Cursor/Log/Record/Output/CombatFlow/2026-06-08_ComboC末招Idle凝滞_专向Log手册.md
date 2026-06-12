> 最后更新：2026-06-08 21:00
> 产出时间：2026-06-08 19:30

# Graph 末招 Combo C 无法回 Idle — 专向诊断 Log 手册

## 现象

- 动作：`General_Armature_Sword_Attack_Combo_C_ActionData`
- Route：`Route_Normal_Combo_C`
- Graph 节点：`ActionC_Action` → `End`（`OnSegmentComplete`，无 TargetRoute）
- 表现：末招结束后 **凝滞在 Action 姿态**，未回到 Locomotion Idle

## 开启

1. Player 上 **`Debug Skill Route`** = true  
2. Console 过滤（二选一或同时）：
   - **`[SkillRoute][Graph]`** — 游标推进 / 边命中 / ComboChain
   - **`[CombatGraph][Finisher]`** — 末段退出专向（≤12 条/repro）  
3. 复现：LM Graph 连段 A → B → **C**，打完末招松手  
4. 预期整段 Log **≤ 12 条**（`TRACE-BEGIN` … `TRACE-END`，事件-only）

## 过滤范围（降噪）

仅当以下任一命中才打 Log：

- Action 名含 `Combo_C`
- Route 名含 `Combo_C` / `Route_Normal_Combo_C`
- Graph 游标含 `ActionC` 或为 `End`

其它 Route/段 **不打**，避免全战斗刷屏。

---

## 事件类型 ↔ 握手站

| 标签 | 触发点 | 看什么 |
|---|---|---|
| `STAGE-DONE` | `NormalRouteRuntime` Stage.Completed | Stage 时长是否跑满；`routeActive` 是否随后变 false |
| `ROUTE-INACTIVE` | `SkillEntryService` Route 退出后 | 确认 Route 已死 + 当前 cursor |
| `SEGMENT-END` | 段末 Graph 分叉 | **最关键**：`branch=GRAPH-ONLY` vs `NATURAL-EXIT` |
| `NATURAL-EXIT` | `NotifyRouteNaturalExit` | `IDLE` vs `LATE-OPEN`（末段 Late 窗） |
| `EXIT-GATE` | `PlayerActionState` nt≥0.95 一次 | 退出闸门四元组是否满足 |
| `EXIT-FIRED` | 真正 `ExitToBaseline` 一次 | 闸门通过且已派发退出 |
| `BASELINE-EXIT` | `ExitToBaseline` 分支 | **`JumpLand` + `forceReenter=true`** 或 `Locomotion` |
| `STALL-SUSPECT` | route 已死 + nt≥0.99 + **gatePass=false** | 凝滞铁证（每招最多 1 条） |
| `TRACE-BEGIN` / `TRACE-END` | 末段 Route 进入 / 回到 Locomotion | 单次 repro 边界；`events≤12` |

---

## 猜想 ↔ 判定（修 bug 用）

| ID | 猜想 | Log 信号 | 修复方向 |
|---|---|---|---|
| **H1** | ActionC→End 走 `GRAPH-ONLY`，**未调 `NotifyRouteNaturalExit`** | `SEGMENT-END branch=GRAPH-ONLY cursor …→End`，**无** `NATURAL-EXIT` | `SkillEntryService`：graph-only 后补 `NotifyRouteNaturalExit` 或 End 等价 Idle |
| **H2** | `NotifyRouteNaturalExit` 开了 **Late Window** 锚在 ActionC | `NATURAL-EXIT LATE-OPEN anchor=ActionC_Action` | 末段无后续边时不应开 Late；或 Late 过期后 `TickLateWindow`→Idle |
| **H3** | Route 已死但 **Action nt 不到 1** | `ROUTE-INACTIVE` 有，`EXIT-GATE pass=false` 且 nt&lt;1 | Action 时长 / AnimSync / Stage.Duration 对齐 |
| **H4** | Route 仍 Active（Stage 未完成） | 无 `ROUTE-INACTIVE`；`STAGE-DONE routeActive=true` 一直重复 | Stage Tick / Transition 卡死 |
| **H5** | 闸门满足但未切状态 | `EXIT-GATE pass=true` 无 `EXIT-FIRED` | `ExitToBaseline` 分支被 JumpLand 等截胡 |
| **H6** | 闸门不满足：route 活着 | `EXIT-GATE routeEnded=false` + `STALL-SUSPECT` | Route 未 `TryEndRouteWhenLastStageComplete` |
| **H7** | Graph 游标 End 与 IdleNode 同 ID | `ROUTE-INACTIVE cursor==idleNode`（本资产 `IdleNodeId=End`） | 正常；解析锚点仍用 `Start` |
| **H8** | **空中起手** + 落地后 JumpLand 分支 **`Change` 同状态被忽略** | `EXIT-GATE pass=true` + `EXIT-FIRED` + **无** `BASELINE-EXIT`；或每帧重复 `[Action][Exit]` | `ForceChange<PlayerActionState>` + `m_exitDispatched` 单次退出 |

> 资产核对：`CombatFlow_Player` 中 `IdleNodeId=End`（End 即 Idle 节点）；`ActionC_Action→End` 为 **OnSegmentComplete**，`TargetRoute=null`。

---

## 健康链路（期望 Log 顺序）

```
[CombatGraph][Finisher] TRACE-BEGIN route=Route_Normal_Combo_C action=…Combo_C…
[CombatGraph][Finisher] STAGE-DONE … routeActive=false
[CombatGraph][Finisher] SEGMENT-END branch=GRAPH-ONLY … cursor ActionC_Action→End idleNode=End
[CombatGraph][Finisher] NATURAL-EXIT IDLE … cursor End→End at-idle
[CombatGraph][Finisher] ROUTE-INACTIVE … cursor==idleNode
[CombatGraph][Finisher] EXIT-GATE pass=true routeEnded=true nt=1.000
[CombatGraph][Finisher] EXIT-FIRED branch=RouteEnded …
[CombatGraph][Finisher] BASELINE-EXIT branch=Locomotion …
[CombatGraph][Finisher] TRACE-END outcome=Locomotion events=8
```

空中起手落地末段（H8 修复前典型坏链）：

```
EXIT-GATE pass=true → EXIT-FIRED
[GraphCtx] SET JumpLand …（每帧重复，无 BASELINE-EXIT）
STALL-SUSPECT …（误报，已收紧为 gatePass=false 才打）
```

修复后空中末段应出现：

```
BASELINE-EXIT branch=JumpLand forceReenter=true
TRACE-END outcome=JumpLand-reenter
```

---

## 代码落点

| 文件 | 职责 |
|---|---|
| `CombatGraphFinisherDiagnostics.cs` | 事件 Log + 去重 + 过滤 |
| `NormalRouteRuntime.cs` | `STAGE-DONE` |
| `SkillEntryService.cs` | `SEGMENT-END` / `ROUTE-INACTIVE` |
| `CombatGraphRunner.cs` | `NATURAL-EXIT` |
| `PlayerActionState.cs` | `EXIT-GATE` / `EXIT-FIRED` / `BASELINE-EXIT` / `STALL-SUSPECT` |
| `PlayerLocomotionState.cs` | `TRACE-END outcome=Locomotion` |

---

## 验收（Play）

- [ ] Combo C 打完 → Console 有完整链或明确 `STALL-SUSPECT` + 上一条 `SEGMENT-END`  
- [ ] 根据判定表定位 H1~H7 之一  
- [ ] 修复后：`EXIT-FIRED` + 角色回 Locomotion，Graph cursor=Idle  

---

## 面试口述（20 秒）

Graph 末段 Graph Route 在 Stage 完成时 **Route 先死、动画后完**（`routeEnded` 先于 `nt=1`）。空中起手的 Combat 末段会走 **JumpLand 分支**；若用 `Change<PlayerActionState>` 会被 FSM **同状态忽略**，导致每帧 `ArmPendingAction` 但永不重入 — MultiStage 同动作在**地面起手**走 `Locomotion` 故不受影响。专向 Log 过滤 **`[CombatGraph][Finisher]`**，看 `BASELINE-EXIT` 是否出现。
