# 06_ActionCatalog — Action 体系目录

> **生成时间**: 2026-06-08  
> **分析方法**: 代码全量扫描 `ActionDataSO` 数据模型 + `ActionWindow` + `ActionTimelineMarker`  
> **注意**: 具体 SO 资产实例 (.asset 文件) 为二进制格式，需在 Unity Editor 中查看。本目录描述数据结构模型和运行时能力。

---

## ActionDataSO 数据模型

```csharp
// 完整字段清单
class ActionDataSO : ScriptableObject
{
    // ── 动画 ──
    AnimationClip MainClip;          // 主表现片段
    float CrossfadeTime;             // 动画过渡时长
    float AnimSpeed;                 // Clip 播放倍率
    float Duration;                  // 逻辑时长 (秒)
    float AnimationEndRatio;         // Clip 完成比例

    // ── 意图车道 ──
    ActionIntentCategory IntentCategory;   // Combat / Locomotion / Reaction / Interaction
    GraphParticipation GraphParticipation; // Auto / None / SourceOnly / Full

    // ── 打断语义 ──
    ActionCategory Category;         // Movement / Offense / Defensive / Utility / Locomotion
    int InterruptPriority;           // 打断优先级 (越大越高)
    int InterruptStability;          // 强韧度 (被硬打断阈值)
    bool AllowSelfInterrupt;         // 自打断开关

    // ── Motion ──
    MotionProfileSO MotionProfile;   // 非空→程序化位移; 空→纯表现
    MotionPrincipalAxis PrincipalAxis;
    MotionScaleType DurationStatScaling;

    // ── 时间轴 ──
    List<ActionWindow> Windows;          // 标签切片 (Phase/Interrupt/Hitbox/Invincible/ComboInput)
    List<TeleportTrigger> TeleportTriggers;
    List<ActionTimelineMarker> TimelineMarkers;  // FX/Audio/Camera/TimeScale
}
```

---

## Action 分类体系

### ActionCategory (B轴 — 打断判定用)

| 类别 | 比特值 | 默认优先级 | 说明 |
|------|--------|-----------|------|
| `Movement` | 0x01 | 30 | 战斗位移 (翻滚/突进) |
| `Offense` | 0x02 | 20 | 攻击 |
| `Defensive` | 0x04 | 40 | 防御 |
| `Utility` | 0x08 | 10 | 功能性 (Buff等) |
| `Locomotion` | 0x10 | 30 | 基础移动 (WASD/Jump) |

### ActionIntentCategory (A轴 — 仲裁车道)

| 车道 | 说明 | 路由方式 |
|------|------|---------|
| `Combat` | 战斗动作 | SkillEntryService → CombatGraph (或Entry单轨) |
| `Locomotion` | 移动动作 | 全局仲裁 (不经SkillEntry) |
| `Reaction` | 受击响应 | 全局仲裁 |
| `Interaction` | 交互 | 全局仲裁 |

### GraphParticipation (C轴 — 图参与身份)

| 身份 | 说明 | 双闸门要求 |
|------|------|-----------|
| `Auto` | 按IntentCategory派生 | — |
| `None` | 不参与图 | 仅ActionWindow闸门 |
| `SourceOnly` | 仅作图源节点 | 动作中不要求图命中 |
| `Full` | 完整图参与 | 需要 Graph Edge + ActionWindow 双闸门 |

---

## ActionWindow 时间轴轨道 (15轨)

ActionDataTimelineEditor 中定义的完整轨道列表：

| 轨道 | 类型 | 说明 | 运行时行为 |
|------|------|------|-----------|
| Interrupt | 打断配置 | 在此时间窗口内允许哪些 Category 打断 | `ActionInterruptResolver.CanInterrupt` |
| PhaseStartup | 标签 | 起手阶段 | 写入 StateTag.PhaseStartup |
| PhaseActive | 标签 | 活跃阶段 | 写入 StateTag.PhaseActive |
| PhaseRecovery | 标签 | 后摇阶段 | 写入 StateTag.PhaseRecovery |
| Hitbox | 事件 | 攻击判定窗口 | `ActionTimelineRuntime.Tick` 检测→DamagePipeline |
| Hurtbox | 事件 | 受击判定窗口 | 受击窗口放大/缩小 |
| Invincible | 事件/标签 | 无敌窗口 | 受击判定跳过 |
| ComboInput | 事件 | 连招输入窗口 | 允许连招输入 |
| RootMotion | 事件 | RootMotion窗口 | [待验证] |
| RuntimeEvent | 事件 | 自定义运行时事件 | 触发回调 |
| Teleport | 事件 | 瞬移触发 | `TeleportTo` |
| Fx | 表现 | 特效标记 | `ActionTimelinePresentationPlayer` 播放VFX |
| Audio | 表现 | 音效标记 | 播放音效 |
| Camera | 表现 | 相机效果 | 震动/FOV/切换 |
| TimeScale | 表现 | 时停/慢动作 | `ActionTimeScaleDriver` |

---

## Action 运行时能力清单

每个 Action 可配置的能力矩阵：

| 能力 | 来源字段 | 说明 |
|------|---------|------|
| **程序化位移** | `MotionProfile != null` | MotionExecutor 驱动位移 |
| **纯表现动画** | `MotionProfile == null` | 仅播动画，无脚本位移 |
| **可被打断** | `Windows` + `Category` | 窗口内允许指定类别打断 |
| **硬打断** | `InterruptPriority > target.Stability` | 跨优先级强制打断 |
| **自打断** | `AllowSelfInterrupt` | 同一动作重入 |
| **Hitbox判定** | 窗口 Hitbox 轨道 | 伤害判定触发 |
| **无敌** | 窗口 Invincible 轨道 | 受击免疫 |
| **连招输入** | 窗口 ComboInput 轨道 | 接受连招操作 |
| **瞬移** | `TeleportTriggers` | 离散闪现 |
| **时停** | TimelineMarker TimeScale | 子弹时间 |
| **镜头震动** | TimelineMarker Camera | 相机震动 |
| **特效/音效** | TimelineMarker FX/Audio | VFX/SFX 触发 |
| **属性缩放** | `DurationStatScaling` | 时长受攻速影响 |
| **重力挂起** | `MotionProfile.Gravity == SuspendGravity` | 动作中无重力 |
| **地面约束** | `MotionProfile.GroundConstraint` | 接地禁止浮空 |
| **空中起手** | `ActionAirborneLock` | 动作期禁止接地判true |

---

## Action 与 Skill 的绑定关系

```
SkillEntryDefinition
  ├── NormalRoute → SkillRouteDefinition
  │     └── SkillStageDefinition[0]
  │           └── ActionDataSO (首段动作)
  │
  ├── ComboRoute → ComboRouteDefinition
  │     └── ComboChain[]
  │           └── SubRoute (SkillRouteDefinition)
  │                 └── SkillStageDefinition[]
  │                       └── ActionDataSO (每段动作)
  │
  ├── ChargeRoute → ChargeRouteDefinition
  │     ├── TapStage → ActionDataSO (点按动作)
  │     └── HoldReleaseStages[] → ActionDataSO (分档动作)
  │
  ├── MultiStageRoute → MultiStageRouteDefinition
  │     └── SkillStageDefinition[]
  │           └── ActionDataSO (每段动作, Auto-advance)
  │
  └── DerivativeRoute → DerivativeRouteDefinition
        └── SubRoute → ActionDataSO
```

---

## Action 关键运行时行为

### 位移控制

| MotionProfile状态 | 位移来源 | 动画表现 |
|------------------|---------|---------|
| `!= null` | `MotionExecutor` → `AxisCurves` → `DesiredVelocity` → KCC | 动画同步播放 (AnimSpeed by Motion) |
| `== null` | 无脚本位移 (仅Locomotion驱动或RootMotion) | 动画独立播放 |

### 打断行为

```
CanInterrupt 判定顺序:
  1. action == null || Windows == null → false
  2. incomingPriority > action.InterruptStability → HARD BREAK (true)
  3. IsCategoryAllowedAtWindow(action, nt, incomingCategory)
     → 遍历 Windows: t ∈ [w.Start, w.End] && category match → true
  4. else → false
```

### 结束行为

```
ExitToBaseline 判定:
  if startedWhileAirborne && IsGrounded && JumpLand != null && action is Combat:
    → JumpLand动作 (播落地后摇)
  else:
    → LocomotionState (回到移动)
```

---

## [待验证] — 需在 Unity Editor 中确认的项目

1. **具体Action资产数量**: 需在 Project 窗口中搜索 `t:ActionDataSO` 统计
2. **MotionProfile引用情况**: 哪些 Action 有 MotionProfile，哪些没有
3. **动画Clip来源**: MainClip 引用的是哪个 AnimationClip
4. **时间轴窗口配置**: 每个 Action 的 Windows 实际切了几个窗口
5. **CombatGraph 节点**: 哪些 Action 被注册为图节点
6. **LocomotionGraphContext**: JumpStart/JumpLoop/JumpLand 具体指向哪个 Action
