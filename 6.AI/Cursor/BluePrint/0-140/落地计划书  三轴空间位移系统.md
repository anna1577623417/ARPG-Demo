> 最后更新：2026-05-19 17:00
> 产出时间：2026-05-19 09:30

# 《XYZ Motion Runtime 三轴空间位移系统》落地计划书

## 0. 元信息

| 项               | 内容                                                                               |
| --------------- | -------------------------------------------------------------------------------- |
| 计划名             | XYZ Motion Runtime — 局部空间三轴位移 + 主导权治理                                            |
| 切片名             | Slice-Motion-XYZ-MVP                                                             |
| 总 Landing 数 N   | **5**                                                                            |
| 最早 Play Mode 验证 | **Landing 2**（Z 轴负方向"加里奥 E 撤步冲锋"可在新 Sample 路径跑通）                                 |
| 关联 Rule         | 07-single-track-migration / 09-refactor-delivery-protocol / SkillRoute v4.3.6 经验 |
| 建议 git tag      | `refactor/motion-xyz-v1`                                                         |

---

## 1. 目标与诚实边界

### 架构目标（3 条）

1. **三轴位置曲线**：`MotionProfileSO` 由【单向 ForwardCurve（DisplacementCurve+BaseDistance）+ LateralCurve + WarpCurve】重构为统一的【XCurve / YCurve / ZCurve（局部空间位置）】，全部允许负方向，支持任意叠加（S 轨迹、撤步冲锋、空中下劈）。
2. **Motion Composer 单写入者**：所有位移贡献（Motion / Gravity / Knockback / AirControl）汇入 `MotionComposer`，**只有 KCC Motor 真正写 Transform**，杜绝 Y 轴多源写入冲突。
3. **YAxisPolicy 主导权枚举**：用 `UseGravity / SuspendGravity / MotionControlled / AdditiveGravity` 显式表达"本动作 Y 归谁管"，替代现有的 `MotionGravityBehavior` 二元布尔。

### 本计划不解决什么（明确不做）

- ❌ **不做 BlendGravity（曲线混合重力）**：留待环境系统 / 空中阻尼 Slice。
- ❌ **不做 Motion Layer 多层组合**（AirControl、WindForce、击退等仅占位接入，Knockback 不做实际逻辑）—— 多 Layer 留下一 Slice。
- ❌ **不做 Scene 路径预览 Gizmo 完整 UI**：仅 Landing 5 提供最简轨迹 Gizmo，全功能编辑器（颜色、标记点）下一 Slice。
- ❌ **不做 Rotation Motion 独立轨道**：本 Slice 朝向仍随移动方向，Rotation Curve 留下一 Slice。
- ❌ **不做 PrevPosition 曲线**：本 Slice 一律 Position 曲线 + Delta 采样（不引入 Velocity 曲线分支）。
- ❌ **不动 ChargeRoute 的 `MotionPlaybackContext.LoopWindow / Freeze`**：照原样接入新 MotionExecutor，不重构蓄力凝滞点。

### 与资料/蓝图的差异

- 蓝图 §25 列出了 4 个 Phase；本 Slice 对应 **Phase 1 全量 + Phase 2 部分 + Phase 3 部分**。
- 蓝图 §8 提出的 `YAxisPolicy.BlendGravity` 明确不做。
- 蓝图 §22 提出的"Scene 路径预览"裁剪为单色 Gizmo MVP，避免 Editor 框架膨胀（参考 SkillRoute 经验：编辑器炫技优先会拖死 Runtime）。

---

## 2. 垂直切片策略

### 主切片

**Slice-Motion-XYZ-MVP**：三轴 Position 曲线 + 主导权治理 + 4 项验收

### 附属切片

无（同迭代 1 主 + 0 附属，避免双切片膨胀）

### 推荐落地顺序（为何此顺序最快验证）

1. **L1 — `MotionAxisCurves` 结构 + `YAxisPolicy` 枚举**：数据骨架。**不接通 MotionExecutor**（设施已就位但未通电），可被 Inspector 编辑。
2. **L2 — MotionExecutor 改 Sample XYZ + Delta**：第一次 Play Mode 验证（Z 负方向）。Y 仍走旧重力，X 用新 Curve。
3. **L3 — MotionComposer + YAxisPolicy 接管**：Gravity 不再直接写 Transform；本 Landing 接通 MotionControlled / SuspendGravity 模式。
4. **L4 — 多轴叠加 + AdditiveGravity**：S 轨迹 / 空中下劈 / 自然下落混合。
5. **L5 — 删除旧字段 + Inspector Foldout + Scene Gizmo**：清理 + 编辑器最小 UX。

### 依赖关系简图

```
┌──────────────────────────────────────────────────────────────────┐
│  MotionAxisCurves struct (XCurve / YCurve / ZCurve)             │ L1
│  YAxisPolicy enum                                                │
│  MotionProfileSO 加字段（旧字段保留但不接通）                       │
└──────────────────────────────┬───────────────────────────────────┘
                               ↓
┌──────────────────────────────────────────────────────────────────┐
│  MotionExecutor.Tick 重写采样路径                                │ L2
│  Sample XYZ at t → Delta (current - prev) → MotionContribution  │
│  输出：MotionContribution { Vector3 LocalDelta; YAxisPolicy }    │
└──────────────────────────────┬───────────────────────────────────┘
                               ↓
┌──────────────────────────────────────────────────────────────────┐
│  MotionComposer (新)                                              │ L3
│  Inputs: MotionContribution / GravityContribution / ...          │
│  Output: combinedDelta → PlayerKCCMotor.MoveByContribution       │
│  规则：按 YAxisPolicy 裁决 Y 主导权                                │
└──────────────────────────────┬───────────────────────────────────┘
                               ↓
┌──────────────────────────────────────────────────────────────────┐
│  多轴叠加：X+Z（S 轨迹）/ Y+Z（下劈）/ AdditiveGravity（自然下落）  │ L4
└──────────────────────────────┬───────────────────────────────────┘
                               ↓
┌──────────────────────────────────────────────────────────────────┐
│  旧字段删除（DisplacementCurve/LateralCurve/Warp/BaseDistance）   │ L5
│  Inspector Foldout + Scene Gizmo                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## 3. 设施表（Slice 结束前必须存在）

|设施项|类型/路径|层级|创建于|备注|
|---|---|---|---|---|
|`MotionAxisCurves`|struct / `4_Data/3.Motion/MotionAxisCurves.cs`|4_Data|L1|含 XCurve/YCurve/ZCurve + XScale/YScale/ZScale（曲线归一化 × Scale = 米）|
|`YAxisPolicy`|enum / `4_Data/3.Motion/YAxisPolicy.cs`|4_Data|L1|UseGravity / SuspendGravity / MotionControlled / AdditiveGravity|
|`MotionProfileSO.AxisCurves` 字段|field / 现有 `MotionProfileSO.cs`|4_Data|L1|新增；旧 DisplacementCurve / LateralCurve 标【Obsolete】，L5 删除|
|`MotionProfileSO.YPolicy` 字段|field / 同上|4_Data|L1|替代旧 `MotionGravityBehavior`|
|`MotionContribution`|struct / `2_Framework/Motion/Runtime/MotionContribution.cs`|2_Framework|L2|{ Vector3 LocalDelta; YAxisPolicy YPolicy; bool IsActive }|
|`MotionExecutor.SampleAxisDeltas()`|method / 现有 `MotionExecutor.cs`|3_Gameplay|L2|Position 曲线 Delta 采样|
|`MotionComposer`|class / `2_Framework/Motion/Runtime/MotionComposer.cs`|2_Framework|L3|单写入者：组合 Motion+Gravity+Knockback|
|`GravityContribution`|struct / `2_Framework/Motion/Runtime/GravityContribution.cs`|2_Framework|L3|{ float Vy }|
|`PlayerKCCMotor.MoveByComposedDelta()`|method / 现有 `PlayerKCCMotor.cs`|3_Gameplay|L3|唯一对外写 Transform 入口|
|`MotionAxisCurvesDrawer`|Editor / `Editor/PropertyDrawers/MotionAxisCurvesDrawer.cs`|Editor|L5|Inspector Foldout: X/Y/Z 三轴折叠|
|`MotionPathGizmoDrawer`|Editor / `Editor/Gizmos/MotionPathGizmoDrawer.cs`|Editor|L5|Scene 视图轨迹预览（仅 Editor）|
|验证场景 `Scene_Motion_XYZ_MVP`|Scene / `Assets/_Verification/Scenes/Scene_Motion_XYZ_MVP.unity`|_Verification|L5|含 4 个 MotionProfile 资产|
|`Motion_Galio_E.asset` 等 4 个验收资产|SO / `Assets/_Verification/MotionProfiles/`|_Verification|L5|4 项验收对应配置|

---

## 4. 握手表（全切片通电清单）

|#|通电点|上游|下游|Landing|状态|通电成功 Log|断点未通时 Log|
|---|---|---|---|---|---|---|---|
|W1|MotionProfileSO 编辑 → AxisCurves 字段|Inspector|SerializedProperty"axisCurves"读取|L1|**WIRE**|无（Editor 验证）|—|
|W2|MotionExecutor → SampleAxisDeltas|`_profile.AxisCurves`|`MotionContribution.LocalDelta`|L2|**WIRE**|`[Motion] sample t=0.42 deltaLocal=(0.15,0,-0.30) yPolicy=UseGravity`|`[Motion] axisCurves missing → fallback zero`|
|W3|MotionContribution → PlayerKCCMotor|MotionExecutor.Tick|KCCMotor.SetDesiredVelocity|L2|**WIRE**|`[Motion] desired vel=(0.5,0,-1.0)`|—|
|W4|MotionComposer.Compose|MotionContribution + GravityContribution|KCCMotor.MoveByComposedDelta|L3|**WIRE**|`[Composer] motion=(0.1,0,0.5) gravity=(0,-0.1,0) policy=UseGravity → final=(0.1,-0.1,0.5)`|`[Composer] no contributions → final=zero`|
|W5|YAxisPolicy 裁决|MotionContribution.YPolicy|Composer Y 通道选择|L3|**WIRE**|`[Composer] yPolicy=MotionControlled → useY=motion(0.2) ignoreGravity`|`[Composer] yPolicy=unknown → fallback UseGravity`|
|W6|Gravity 不再直接 Move|PlayerKCCMotor.ApplySimpleGravity|改为 GravityContribution.Vy 写入 Composer|L3|**WIRE**|`[Gravity] contribute vy=-0.5 (no direct Move)`|`[Gravity] direct Move detected (REGRESSION!)`|
|W7|X+Z 多轴叠加|XCurve.Evaluate + ZCurve.Evaluate|Composer 接收两分量|L4|**WIRE**|`[Motion] sample x=1.2 z=2.0 → local=(1.2,0,2.0)`|—|
|W8|Y+Z 叠加 + AdditiveGravity|YCurve + ZCurve + Gravity|Composer 输出|L4|**WIRE**|`[Composer] yPolicy=AdditiveGravity → useY=motion(0.3)+gravity(-0.1)=0.2`|—|
|W9|旧字段删除|MotionProfileSO.DisplacementCurve / LateralCurve / Warp*|全仓 grep = 0|L5|**WIRE**|无（编译验证）|编译失败 → 漏删引用|
|W10|AxisCurves Drawer|Inspector|Foldout 显示 X/Y/Z|L5|**WIRE**|无（Editor 验证）|—|
|W11|Scene Gizmo|OnDrawGizmos|轨迹折线显示|L5|OPEN（不接）|—|故意延后：完整 UX 留下一 Slice，本 Landing 只画单色折线|
|W12|Rotation Curve|独立 Rotation 轨道|—|—|OPEN|—|故意延后：本 Slice 朝向沿 Move 方向|
|W13|Knockback / AirControl Layer|Composer 额外通道|—|—|OPEN|—|故意延后：Motion Layer 留下一 Slice|

---

## 5. 契约表

### 5.1 时间/状态/结算/优先级契约

|契约项|唯一真相 API/字段|谁写入|谁只读|Inspector/配置|
|---|---|---|---|---|
|三轴曲线|`MotionProfileSO.AxisCurves` (XCurve/YCurve/ZCurve + XScale/YScale/ZScale)|Inspector / Editor 工具|MotionExecutor 只读|MotionProfileSO Inspector|
|Y 主导权|`MotionProfileSO.YPolicy`|Inspector|MotionComposer 只读|MotionProfileSO Inspector|
|位移贡献量|`MotionContribution.LocalDelta` (角色局部空间)|MotionExecutor 单点产生|MotionComposer 只读|无（运行时态）|
|重力贡献量|`GravityContribution.Vy`|GravitySystem 单点产生|MotionComposer 只读|无|
|最终位移|`combinedDelta`|MotionComposer 唯一产生|PlayerKCCMotor.MoveByComposedDelta 唯一消费|无|
|Transform 写入|`transform.position += ...`|**仅** PlayerKCCMotor 内部|无人|无|

### 5.2 配置面 → 运行时锚点

|Inspector 字段|写入 SO|读取运行时锚点|同 PR 修改 Editor?|
|---|---|---|---|
|`AxisCurves.XCurve`|MotionProfileSO.asset|`MotionExecutor.SampleAxisDeltas` 每帧采样|**L1 同 PR**（仅字段 + 默认 Drawer）|
|`AxisCurves.XScale`|同上|`_axisCurves.SampleX(t) * XScale`|L1 同 PR|
|`YPolicy = MotionControlled`|MotionProfileSO.asset|`MotionComposer.SelectY` 分支|**L3 同 PR**（Drawer 折叠 + 文档 Tooltip）|

---

## 6. Landing 明细

### Landing 1/5：MotionAxisCurves + YAxisPolicy 数据骨架

- **本落位目标**（Landing Done 判定句）：`MotionAxisCurves` struct / `YAxisPolicy` enum 已就位；`MotionProfileSO` 新增 `axisCurves` 与 `yPolicy` 字段；Inspector 可编辑；**MotionExecutor 仍走旧路径，本落位允许玩法未变化**。
- **WIRE 行**：W1
- **OPEN 行**：W2-W11（设施未通电）
- **设施增量**：
    - 新建：`MotionAxisCurves.cs`、`YAxisPolicy.cs`、`MotionContribution.cs`（先建空壳，L2 填充）
    - 修改：`MotionProfileSO.cs` 加字段；旧 `DisplacementCurve / BaseDistance / LateralCurve / LateralDistance / WarpCurve / MaxWarpDistance / MotionGravityBehavior` 标 `[System.Obsolete("XYZ Motion 系统 L5 删除", false)]` 但不删
    - 删除：无
- **禁止在本落位做**：
    - ❌ 改 MotionExecutor 任何代码（避免运行行为改变）
    - ❌ 写 MotionComposer
    - ❌ 删除任何旧字段
- **改动文件清单**：
    - `4_Data/3.Motion/MotionAxisCurves.cs` — 含 XCurve/YCurve/ZCurve + XScale/YScale/ZScale (Vector3 ScalesMeters)
    - `4_Data/3.Motion/YAxisPolicy.cs` — 4 值 enum
    - `4_Data/3.Motion/MotionProfileSO.cs` — 加 `axisCurves` / `yPolicy` 字段 + 公有只读属性 + 旧字段 Obsolete
    - `2_Framework/Motion/Runtime/MotionContribution.cs` — struct 空壳
- **可观测**：无（本 Landing 仅数据层）
- **验证步骤**（≤5）：
    1. 编译通过；Console 0 warning（除 Obsolete 警告）
    2. 任意 MotionProfileSO 资产 Inspector 显示新增的 AxisCurves Foldout（默认 Drawer 即可）
    3. 旧字段 Inspector 仍可见但标灰提示 Obsolete
    4. 进 Play Mode → 玩法**完全无变化**（关键：未通电的验证）
    5. grep `YAxisPolicy` ≥ 4 命中（enum 定义 + 字段 + Editor + 1 处文档）
- **通过标准**：
    - [ ] 编译 0 error
    - [ ] 玩法无回归（任意攻击行为与 Landing 前一致）
    - [ ] AxisCurves 字段在 Inspector 可见可编辑
    - [ ] `yPolicy = UseGravity` 默认值
- **常见失败与通路修复**：
    - 序列化报错 → 检查 `MotionAxisCurves` 是否标 `[System.Serializable]`；不允许"如果 axisCurves==null 就用 default"兜底
- **风险与回滚**：tag `pre/motion-xyz-v1` → `git reset --hard pre/motion-xyz-v1`

---

### Landing 2/5：MotionExecutor 切到 XYZ 采样（**最早 Play Mode 验证**）

- **本落位目标**：`MotionExecutor.Tick` 内的位移采样路径**整体改读** `_profile.AxisCurves`；Delta 采样输出 `MotionContribution.LocalDelta`；KCCMotor 改读此 `MotionContribution`；**Y 仍走旧重力**（YAxisPolicy 接管留 L3）；**加里奥 E 撤步冲锋（Z 负方向）应可见**。
- **WIRE 行**：W2、W3
- **OPEN 行**：W4-W11
- **设施增量**：
    - 修改：`MotionExecutor.cs` — `Tick` 内重写采样；删除旧 `SampleDisplacement / SampleLateral / SampleWarp / BaseDistance / PeakSpeedMultiplier / LateralDistance` 引用
    - 修改：`MotionContribution.cs` — 填充 `LocalDelta / YPolicy / IsActive`
    - 修改：`PlayerMotorAdapter.cs`（`SetDesiredVelocity` 现读 LocalDelta 转世界）
- **禁止在本落位做**：
    - ❌ 写 MotionComposer（留 L3）
    - ❌ 改 PlayerKCCMotor.ApplySimpleGravity（重力仍由 KCC 处理）
    - ❌ 改 ChargeRouteRuntime / Playback Context（避免与凝滞点交互）
    - ❌ 删 MotionProfileSO 旧字段
- **改动文件清单**：
    - `3_Gameplay/Motion/Runtime/MotionExecutor.cs` — `SampleAxisDeltas(prevT, currT)` 替代旧 4 段采样
    - `2_Framework/Motion/Runtime/MotionContribution.cs` — 填充
    - `3_Gameplay/Motion/Runtime/PlayerMotionAdapters.cs` — Adapter 转读 LocalDelta
    - `Assets/_Verification/MotionProfiles/Motion_Galio_E.asset` — 新建：`ZCurve` 关键帧 `0→0, 0.2→-1.2, 0.4→0, 1.0→6.0`（先撤后冲）
    - `Assets/_Verification/Scenes/Scene_Motion_XYZ_MVP.unity` — 新建：单 Player + 1 个测试装载该 Profile 的 Action
- **可观测**：新增 `[Motion]` 类别（采样节流：每 0.1 秒一次输出，避免刷屏）
- **验证步骤**：
    1. 进 Play Mode（Scene_Motion_XYZ_MVP）
    2. 触发装载 `Motion_Galio_E` 的攻击
    3. 期望 Console 出现 `[Motion] sample t=0.10 deltaLocal=(0,0,-0.5)` 后逐步 `t=0.30 deltaLocal=(0,0,-0.3)`，再 `t=0.70 deltaLocal=(0,0,1.2)`
    4. 视觉确认：角色先后退 → 短暂凝滞 → 前冲
    5. 旧 LateralCurve / Warp 资产配置仍存在但**不再影响运行时**（log 验证 `deltaLocal.x == 0`）
- **通过标准**：
    - [ ] 加里奥 E：角色 Z 方向走负值 → 0 → 正值，肉眼可见撤步与冲锋
    - [ ] Console `[Motion] sample` 显示 deltaLocal.z 有负值阶段
    - [ ] 所有其他攻击（普攻 / Combo / Charge）**不破**（功能不退化，但位移可能因旧 Profile 全 Y 上的 BaseDistance/LateralDistance 不再生效而变弱 — 这是预期，留 L5 修资产）
- **常见失败与通路修复**：
    - 没有撤步 → 查 `[Motion] sample` log 是否输出，AxisCurves.ZCurve 是否真有负值关键帧；不许"如果 LocalDelta.z 为正则强制取负"
    - 角色完全不动 → 查 MotionProfileSO 的 AxisCurves 字段是否非空（必须配置至少 ZCurve），不许 fallback 旧 DisplacementCurve
- **风险与回滚**：tag `motion-xyz/L1-done`（最重要的回退点）

---

### Landing 3/5：MotionComposer 单写入者 + YAxisPolicy 接管

- **本落位目标**：`MotionComposer` 类成形；`PlayerKCCMotor` 内部不再直接 `transform.position +=`，全部走 `MoveByComposedDelta`；Gravity 改为提供 `GravityContribution`，不再直接写；YAxisPolicy = `UseGravity / SuspendGravity / MotionControlled` 三模式可观察生效；**AdditiveGravity 留 L4**。
- **WIRE 行**：W4、W5、W6
- **OPEN 行**：W7-W11
- **设施增量**：
    - 新建：`MotionComposer.cs`、`GravityContribution.cs`
    - 修改：`PlayerKCCMotor.cs` — `ApplySimpleGravity` 改为 `BuildGravityContribution`；新增 `MoveByComposedDelta`；唯一的 `transform.position +=` 仅在此处
    - 修改：`MotionExecutor.cs` — 输出的 `MotionContribution` 含 `YPolicy = _profile.YPolicy`
    - 修改：`Player.cs` — `ApplyMotor*` API 调用链路最终都走 Composer
- **禁止在本落位做**：
    - ❌ 写 Knockback / AirControl Layer（OPEN）
    - ❌ 改 ChargeRouteRuntime（继续沿用旧 Playback Context）
    - ❌ 删除 MotionProfileSO 旧字段
    - ❌ 修改 Inspector / Editor（留 L5）
- **改动文件清单**：
    - `2_Framework/Motion/Runtime/MotionComposer.cs` — `Compose(motion, gravity) → Vector3`
    - `2_Framework/Motion/Runtime/GravityContribution.cs` — struct
    - `3_Gameplay/Characters/Player/Motion/PlayerKCCMotor.cs` — 重力贡献化 + 单写入者
    - `3_Gameplay/Motion/Runtime/MotionExecutor.cs` — 注入 YPolicy
    - `Assets/_Verification/MotionProfiles/Motion_Launcher.asset` — 新建：`YCurve 0→0, 0.5→3` + `YPolicy=MotionControlled`（升龙）
- **可观测**：`[Composer]` `[Gravity]` 类别
- **验证步骤**：
    1. 触发普攻 → `[Composer] yPolicy=UseGravity → useY=gravity(-0.1)` 持续输出
    2. 触发 Motion_Launcher → `[Composer] yPolicy=MotionControlled → useY=motion(0.5) ignoreGravity` 输出；视觉确认角色腾空且**不下落**
    3. 触发 Motion_Galio_E → 仍 `yPolicy=UseGravity`，撤步冲锋正常
    4. grep `transform.position +=` → 仅 `PlayerKCCMotor.cs` 1 处（或经 KCC API），其他文件 0 命中
    5. 跳起按 F → 仍能跳跃（Jump 路径需复用 GravityContribution 或临时 Y Force，本 Landing 内确认不破跳跃）
- **通过标准**：
    - [ ] 升龙腾空可见且不立即下落
    - [ ] 普攻仍受重力（落地正常）
    - [ ] `[Gravity] direct Move detected` 一次也不出现（这是退化告警，不该出现）
    - [ ] grep `transform.position\s*\+=` 在 `_Gameplay` 和 `_Framework` 命中 ≤ 1
- **常见失败与通路修复**：
    - 升龙仍下落 → 查 `MotionContribution.YPolicy` 是否真的 `MotionControlled`，查 Composer 分支是否抛弃了 Gravity.Vy
    - 普攻不能落地 → 查 GravityContribution.Vy 是否每帧产出；不允许"如果 yPolicy=UseGravity 但 motion.y!=0 就忽略 motion.y"兜底
- **风险与回滚**：tag `motion-xyz/L2-done`

---

### Landing 4/5：多轴叠加 + AdditiveGravity（S 轨迹 / 空中下劈 / 自然下落）

- **本落位目标**：MotionExecutor 多轴叠加无特殊代码（自然由 Vector3 加法实现）；Composer 支持 `AdditiveGravity` 模式（Motion.y + Gravity.Vy）；3 项剩余验收（S 轨迹 / 上挥腾空+自然下落 / 空中下劈）资产配齐并可见。
- **WIRE 行**：W7、W8
- **OPEN 行**：W11、W12、W13
- **设施增量**：
    - 修改：`MotionComposer.cs` — 加 `AdditiveGravity` 分支
    - 新建资产：
        - `Motion_S_Curve.asset` — `XCurve [0→0, 0.25→2, 0.5→-2, 0.75→2, 1.0→0]` + `ZCurve 持续正向` → S 轨迹
        - `Motion_AirSlam.asset` — `YCurve 0→0, 0.3→3, 0.6→3, 1.0→-2` + `ZCurve 持续推进` + `YPolicy=MotionControlled` → 空中下劈
        - `Motion_LeapAttack.asset` — `YCurve 0→0, 0.4→2` + `ZCurve 0→3` + `YPolicy=AdditiveGravity` → 跃击（Motion 抬升后自然下落）
- **禁止在本落位做**：
    - ❌ 改 MotionExecutor 采样代码（多轴叠加是数据驱动）
    - ❌ 编辑器 UI 改进（留 L5）
    - ❌ 删除旧字段（留 L5）
- **改动文件清单**：
    - `2_Framework/Motion/Runtime/MotionComposer.cs` — `AdditiveGravity` 分支
    - 3 个新 Profile 资产
    - Scene_Motion_XYZ_MVP 加 4 个测试按键（数字键 1/2/3/4 各触发一个）
- **可观测**：复用 `[Motion]` `[Composer]`
- **验证步骤**（4 项验收一次性点亮）：
    1. **加里奥 E**：数字键 1 → 撤步冲锋（已 L2 验证）
    2. **S 轨迹**：数字键 2 → 角色 Z 方向持续前进，X 方向左右摆动形成 S 形（俯视轨迹应明显 S）
    3. **空中下劈**：数字键 3 → 角色 Y 上升 → 滞空短暂 → Y 急速下落 + Z 推进
    4. **跃击自然下落**：数字键 4 → 角色 Motion Y 抬升到 2，AdditiveGravity 让 Y 不会恒定在 2 而是被重力慢慢拉下来
- **通过标准**：
    - [ ] 4 项验收 Play Mode 全过
    - [ ] `[Composer] yPolicy=AdditiveGravity → useY=motion(X)+gravity(Y)=Z` log 输出
    - [ ] 跃击落地正常，无悬空 / 穿地
- **常见失败与通路修复**：
    - S 轨迹是直线 → 查 XCurve 关键帧是否真有 [-2, 2] 摆动；不允许"如果 XCurve.length==0 就用 LateralCurve"兜底
    - 跃击始终悬空 → AdditiveGravity 分支可能误把 motion.y 完全覆盖了 gravity.vy；公式应是 **加法**，不是择一
- **风险与回滚**：tag `motion-xyz/L3-done`

---

### Landing 5/5：清理旧字段 + Inspector Foldout + 最简 Gizmo

- **本落位目标**：删除 MotionProfileSO 内所有 Obsolete 字段；新建 `MotionAxisCurvesDrawer` 折叠显示 X/Y/Z；Scene 视图选中带 Motion 的角色时绘制最近一次 Motion 的轨迹折线；**Slice Done = §7 60 秒验收脚本全过**。
- **WIRE 行**：W9、W10
- **OPEN 行**：W11（完整 Gizmo UX）、W12、W13
- **设施增量**：
    - 删除：`MotionProfileSO.DisplacementCurve / BaseDistance / PeakSpeedMultiplier / LateralCurve / LateralDistance / WarpCurve / MaxWarpDistance / MotionGravityBehavior` 字段及所有引用
    - 新建：`Editor/PropertyDrawers/MotionAxisCurvesDrawer.cs`
    - 新建：`Editor/Gizmos/MotionPathGizmoDrawer.cs`（最简：白色折线 + 端点小球）
- **禁止在本落位做**：
    - ❌ 新增任何功能字段（仅清理 + Inspector）
    - ❌ Gizmo 加颜色 / 标记点 / 路径 length 显示（OPEN）
- **改动文件清单**：
    - `4_Data/3.Motion/MotionProfileSO.cs` — 删 7 个字段 + 删旧 Sample 方法
    - `3_Gameplay/Motion/Runtime/MotionExecutor.cs` — 删旧 Sample 引用残留
    - `Editor/PropertyDrawers/MotionAxisCurvesDrawer.cs` — 折叠 Foldout【X 轴 Left ↔ Right】【Y 轴 Down ↕ Up】【Z 轴 Back ↔ Forward】
    - `Editor/Gizmos/MotionPathGizmoDrawer.cs` — `OnDrawGizmosSelected` 单色折线
    - 所有受影响的 Profile 资产人工迁移（如有遗留）
- **可观测**：编译 0 warning（Obsolete 警告全消）
- **验证步骤**：见 §7 60 秒脚本
- **通过标准**：
    - [ ] §7 验收全过
    - [ ] grep `DisplacementCurve` = 0
    - [ ] grep `LateralCurve` = 0（含资产）
    - [ ] grep `MotionGravityBehavior` = 0
    - [ ] 编译 0 warning
- **常见失败**：
    - 删 DisplacementCurve 后某 Profile 资产破损 → 用 `AssetDatabase` 扫库定位并改造为 AxisCurves（不许"如果资产破损就保留 LateralCurve"）
- **风险与回滚**：tag `motion-xyz/L4-done`

---

## 7. Slice 闭环验收

### 60 秒 Play Mode 操作脚本

|秒|操作|预期|
|---|---|---|
|0–5|进 Scene_Motion_XYZ_MVP|Console 0 Error；玩家正常落地|
|5–10|数字键 1（Motion_Galio_E）|撤步后再冲锋；`[Motion]` 显示 deltaLocal.z 负 → 正|
|10–20|数字键 2（Motion_S_Curve）|俯视角下角色轨迹明显 S 形|
|20–30|数字键 3（Motion_AirSlam）|角色腾空滞空再急速下劈|
|30–40|数字键 4（Motion_LeapAttack）|跃起后被重力慢慢拉下，自然落地|
|40–50|数字键 5（Motion_Launcher，YPolicy=MotionControlled）|角色腾空且**不下落**（演出技能）|
|50–55|移动 + 跳跃 + 普攻|跳跃与普攻不破，落地正常|
|55–60|选中场景中 Player，看 Scene 视图|最近一次 Motion 轨迹折线可见|

### 必须出现的 Log

- `[Motion] sample t=... deltaLocal=...`
- `[Composer] motion=... gravity=... → final=...`
- `[Composer] yPolicy=MotionControlled → useY=motion(...) ignoreGravity`
- `[Composer] yPolicy=AdditiveGravity → useY=motion(...)+gravity(...)`

### 不得出现的 Log

- `[Gravity] direct Move detected` — Gravity 绕过 Composer 写 Transform
- 任何 `LateralCurve / DisplacementCurve / WarpCurve` 字符串
- Console Error / NullReference

### EditMode 测试项

|测试|输入|预期|
|---|---|---|
|`Motion_SampleDelta_PositionCurve`|XCurve [(0,0),(0.5,2),(1,0)] + Scale=1，t=0.4→0.5|LocalDelta.x ≈ 0.5 → 0 之间正向递减|
|`Composer_UseGravity`|motion.y=0, gravity.vy=-9.8*dt|final.y = -9.8*dt|
|`Composer_MotionControlled`|motion.y=2, gravity.vy=-9.8|final.y = 2（重力被忽略）|
|`Composer_SuspendGravity`|motion.y=0, gravity.vy=-9.8|final.y = 0|
|`Composer_AdditiveGravity`|motion.y=0.3, gravity.vy=-0.1|final.y = 0.2|

---

## 8. 单轨与删除清单

### 主循环将删除的旧调用点

- `MotionProfileSO`：
    - `DisplacementCurve` / `BaseDistance` / `PeakSpeedMultiplier`
    - `LateralCurve` / `LateralDistance`
    - `WarpCurve` / `MaxWarpDistance`
    - `MotionGravityBehavior` 枚举与 `GravityBehavior` 字段
    - `SampleDisplacement / SampleLateral / SampleWarp` 三方法
- `MotionExecutor`：
    - 旧 `forwardDistance / lateralDistance / warpOffset` 计算块
    - `Vector3.Cross(Vector3.up, _direction)` 横向通道（改由 transform.right 局部空间）
- `PlayerKCCMotor`：
    - 任何**非** `MoveByComposedDelta` 的 `transform.position +=`
    - `ApplySimpleGravity` 内部直接 Move 路径

### grep 验收关键词（应为 0 命中）

```
DisplacementCurve
LateralCurve
LateralDistance
WarpCurve
MaxWarpDistance
MotionGravityBehavior
SampleDisplacement
SampleLateral
SampleWarp
BaseDistance
PeakSpeedMultiplier
```

```
# Transform 单写入校验（应仅命中 PlayerKCCMotor 内部）
grep -rn "transform.position\s*\+=" --include=*.cs
```

### Editor/HUD 同步项

- `MotionAxisCurvesDrawer` 折叠显示 X/Y/Z 三轴（L5 同 PR）
- `MotionProfileSO` Inspector 顶部加 HelpBox：【三轴位置曲线 — 局部空间。Scale 米数 × 归一化曲线 = 实际位移。允许负值。】
- 资产 Migration 脚本（一次性，L5 后期）：扫所有 MotionProfileSO，把 DisplacementCurve+BaseDistance 自动转 ZCurve+ZScale，提示无法转换的人工处理

---

## 9. PR / 提交切分建议

|PR#|对应 Landing|类型|预估范围|禁止混入|
|---|---|---|---|---|
|PR-1|L1|契约（数据骨架）|MotionAxisCurves / YAxisPolicy / MotionProfileSO 加字段 + Obsolete 标|任何 MotionExecutor 改动、Composer 新增|
|PR-2|L2|接线（Executor 切换）|MotionExecutor.Tick 重写 + MotionContribution 填充 + Adapter 改读 + Motion_Galio_E.asset + Scene_Motion_XYZ_MVP|Composer 新增、Gravity 改造、字段删除|
|PR-3|L3|接线（Composer 单写入者）|MotionComposer + GravityContribution + KCCMotor 重力贡献化 + Motion_Launcher.asset|多轴叠加资产、字段删除|
|PR-4|L4|资产 + AdditiveGravity|Composer.AdditiveGravity 分支 + S/AirSlam/LeapAttack 3 个 Profile 资产|删除旧字段、Editor Drawer|
|PR-5|L5|删除 + Editor|旧字段全删 + MotionAxisCurvesDrawer + 最简 Gizmo + 资产迁移脚本|任何 Runtime 修改|
|PR-6|L5 后置|修 Bug（仅 Slice Done 后）|§7 验收发现的资产微调|任何契约 / 接线变更|

---

## 10. 禁止模式自检

### 已规避（设计层）

- ✅ **不双轨**：MotionExecutor 切换在 L2 一次性完成；旧字段在 L1-L4 标 Obsolete 不被读，L5 物理删除
- ✅ **不 fallback**：L2 不允许 "如果 AxisCurves 为空就用 DisplacementCurve"；L3 不允许 "如果 yPolicy=Unknown 就用 UseGravity"，必须 LogError
- ✅ **Y 单写入者**：仅 PlayerKCCMotor.MoveByComposedDelta 写 Transform；grep 校验
- ✅ **YAxisPolicy 显式枚举**：替代 `if (suspendGravity)` 全局布尔（蓄电池经验文档明令禁止的模式）
- ✅ **Position 曲线，不双轨支持 Velocity 曲线**：单一采样语义（current - prev）
- ✅ **PR 不混类型**：契约 / 接线 / 资产 / 删除 分 PR

### 需人工盯防

- ⚠ **L2 Z 负方向但旧 Profile 设的 BaseDistance=正数**：迁移期所有受影响 Profile 资产可能位移幅度变小（因 BaseDistance 不再读）—— L2 验收时只看新 Motion_Galio_E 是否对，**不必修旧资产**（留 L5 迁移）
- ⚠ **L3 Charge 凝滞点交互**：`Playback.FreezeNormalizedAdvance` 仍走原路径；Composer 在 frozen 时收到 `LocalDelta = 0` 即可，不要在 Composer 内额外判 Charge
- ⚠ **L3 Jump 路径**：跳跃通过 `Vy = jumpForce` 直接写 KCC，本 Landing 内 Jump 仍可特殊（不强制 Jump 也走 GravityContribution）；但**禁止** Jump 内 `transform.position +=`
- ⚠ **L4 AdditiveGravity 公式必须是加法**：常见错误是写成 `Mathf.Max(motion.y, -gravity.vy)` 等择一逻辑，必须是 `motion.y + gravity.vy`
- ⚠ **L5 资产迁移**：自动迁移脚本可能对复杂 LateralCurve+Warp 组合处理不完整，必须列出无法迁移的资产并人工修
- ⚠ **Tooltip 用【】**：新增字段 Tooltip 中不可出现 `""`（参考 SkillRoute 经验）

---

## 11. 未决问题（需用户确认）

|#|问题|影响 Landing|不答会卡在哪|
|---|---|---|---|
|Q1|**`XScale / YScale / ZScale` 的语义**：是【曲线值直接是米】还是【归一化曲线 × Scale = 米】？ 推荐后者（与现有 BaseDistance 等价），但需用户确认。这决定 Inspector 文案与 Drawer 单位标识|L1|L1 不能定 MotionAxisCurves 字段类型|
|Q2|**YCurve 是否影响 KCC 的 `IsGrounded` 判定**：升龙腾空时 `IsGrounded=false`，落地恢复 `true`；但 `MotionControlled` 模式下 Y 完全由 Motion 控制，IsGrounded 应当持续 false 还是基于 Transform.y 判？影响空中连段 / Locomotion 切换|L3|L3 写 Composer + KCCMotor 交互时拍不了板|
|Q3|**跳跃（Jump）系统的归属**：F 跳跃由 `Player.Jump()` 直接 `_motor.Jump(jumpForce)` 写 vy，跳跃过程中 Y 由 KCC 内部弹道处理。本 Slice 是否要把 Jump 也纳入 Composer？还是仅蓄电池经验文档说的"Skill Motion + Gravity"两通道，跳跃临时豁免？|L3|L3 决定 KCCMotor.Jump 是否需要改写|
|Q4|**凝滞点（ChargeRoute Freeze）期间 Composer 接收的 motion = (0,0,0)**：本是预期行为；但**重力是否也应被冻结**？目前 ChargeRoute 凝滞时角色站在空中（演出态），若 yPolicy=UseGravity 会下落；是否在凝滞期间强制 `MotionControlled` 切换？这是 ChargeRoute 与 Motion 系统的握手问题|L3 / L4|L3 写 Composer 时遇到的第一类 corner case|
|Q5|**资产迁移自动化范围**：是否要写一个一次性 Editor 工具扫库把 `DisplacementCurve + BaseDistance` 自动写到 `ZCurve + ZScale`？ 还是仅 L5 提供 Migration helper 但人工触发？前者风险高（自动改资产），后者保守但工作量|L5|L5 决定是否要 PR-5 内含 Migration 脚本|

---

**计划生效条件**：用户确认 §11 全部 5 项后，由 AI Landing 1 起手；否则 Q1–Q5 任一悬空都会让 L1（数据结构语义）或 L3（Y 主导权交互边界）出现架构分叉，违反 §0 输出纪律。

---

## 12. 施工记录（2026-05-19）

| Landing | 状态 | 说明 |
|---------|------|------|
| L1 | ✅ | `MotionAxisCurves` / `YAxisPolicy` / `MotionProfileSO.AxisCurves+YPolicy` / `MotionContribution` |
| L2 | ✅ | `MotionExecutor` 三轴 Delta；`UsesAxisCurves` 时单轨采样；未配置仍走 Obsolete 旧通道（迁移前） |
| L3 | ✅ | `MotionComposer` + `GravityContribution`；`PlayerKCCMotor` + `PlayerMotorAdapter` 汇合 |
| L4 | ✅ | `AdditiveGravity` 分支；`Tools/Motion XYZ/Create Verification Profiles` |
| L5 | ✅ | 删 Obsolete 字段；`MotionAxisCurvesDrawer`；Scene Gizmo；`MotionProfileLegacyMigration` 批量迁移 |

**§11 默认决议（本迭代采用）**

- Q1：归一化曲线 × Scale = 米
- Q3：Jump 仍走 `Jump(jumpForce)`，不纳入 Composer
- Q5：提供 `Migrate Selected Profile`，不自动扫库

**Play Mode 起手**

1. Unity 菜单 **`Tools/Motion XYZ/Migrate All Profiles In Project`**（若 YAML 未迁移）
2. `Tools/Motion XYZ/Create Verification Profiles`（验收用）
3. 将 `Motion_Galio_E` 绑到测试 Action；开启 `Player.DebugSkillRoute`
4. Inspector 点 **Scene 预览轨迹** 或选中 Profile 自动画 Gizmo