# 07_MotionCatalog — MotionProfile 体系目录

> **生成时间**: 2026-06-08  
> **分析方法**: 全量扫描 `MotionProfileSO` 数据模型 + `MotionExecutor` 运行时行为  
> **注意**: 具体 SO 资产需在 Unity Editor 中查看，本目录描述数据结构和运行时能力。

---

## MotionProfileSO 数据模型

```csharp
// 完整字段清单
class MotionProfileSO : ScriptableObject
{
    // ── Clip 提取 (Authoring only) ──
    AnimationClip SourceClip;        // 参考动画 (运行时忽略)
    AnimationClip XSourceClip;       // X轴专用Clip (可选)
    AnimationClip YSourceClip;       // Y轴专用Clip (可选)
    AnimationClip ZSourceClip;       // Z轴专用Clip (可选)
    MotionAxisExtractSource XExtractSource;  // Auto / Manual
    MotionAxisExtractSource YExtractSource;
    MotionAxisExtractSource ZExtractSource;

    // ── XYZ 三轴曲线 ★ 唯一位移源 ──
    MotionAxisCurves AxisCurves;     // XCurve×XScale, YCurve×YScale, ZCurve×ZScale

    // ── Y轴三权分离 (V2) ──
    YMotionMode YMotion;             // None / Curve / GroundTargeted
    GravityMode Gravity;             // UseGravity / SuspendGravity / AdditiveGravity
    GroundConstraintMode GroundConstraint;  // ClampToGround / None

    // ── GroundTargeted 落地 ──
    float LandingOffset;             // 落地高度偏移 (米)
    AnimationCurve LandingCurve;     // t→落地进度
    float LandingDetectionRadius;    // 向下检测距离 (米)

    // ── 动画节奏 ──
    AnimSpeedMode AnimSpeedMode;     // Constant / Curve
    AnimationCurve SpeedOverTime;    // 局部节奏倍率 (motionT→speed)

    // ── 属性缩放 ──
    MotionScaleType ScaleType;       // 位移幅度缩放 (None / AttackSpeed ...)

    // ── 参考空间 ──
    MotionSpace MotionSpace;         // CharacterForward / CameraForward / LockTarget / WorldSpace
}
```

---

## 位移类型矩阵

| YMotion | Gravity | GroundConstraint | 典型用途 | 位移行为 |
|---------|---------|-----------------|---------|---------|
| `Curve` | `UseGravity` | `ClampToGround` | 地面突进/冲锋 | Y轴曲线+重力叠加, 不浮空 |
| `Curve` | `SuspendGravity` | `None` | 空中技能/跳劈 | Y轴曲线支配, 完全无重力 |
| `Curve` | `AdditiveGravity` | `ClampToGround` | 跳跃攻击 | 曲线+重力叠加, 落地钳位 |
| `Curve` | `UseGravity` | `None` | 空中冲刺 | Y轴曲线+重力, 不钳地面 |
| `None` | `UseGravity` | `ClampToGround` | 纯地面移动技 | 仅Locomotion驱动XZ, 重力驱动Y |
| `GroundTargeted` | `SuspendGravity` | `ClampToGround` | 空中落地技 | LandingCurve控制高度, 忽略AxisCurves.Y |
| `None` | `SuspendGravity` | `None` | 纯原地技 | 完全无位移, 无重力 |

---

## MotionSpace 参考空间

| 空间 | Z轴映射 | X轴映射 | 适用场景 |
|------|---------|---------|---------|
| `CharacterForward` | 角色面朝方向 | 角色右手方向 | 冲锋/前刺/剑气冲刺 |
| `CameraForward` | 镜头水平前向 | 镜头右手方向 | 四向闪避 (Dodge4) |
| `LockTarget` | 锁敌方向 | 锁敌右手 | 锁定突进 |
| `WorldSpace` | 世界+Z | 世界+X | 固定方向位移 |

**运行时解析**: `Player.ResolveMotionPlanarForward(space)` → `MotionSpaceBasis.ResolvePlanarForward(player, movementContext, space)`

---

## 动画速度控制

### AnimSpeedMode

| 模式 | 行为 | 公式 |
|------|------|------|
| `Constant` | 恒为 1.0 | `animSpeed = baseAnimSpeed * 1.0` |
| `Curve` | 按 SpeedOverTime(motionT) 采样 | `animSpeed = baseAnimSpeed * SpeedOverTime(t)` |

**额外覆盖**: `MotionPlaybackContext.AnimatorSpeedOverride` (Charge蓄力时用)

**最终写入**: `IAnimSpeedControl.SetSpeed(finalSpeed)` → `Animator.speed`

---

## MotionAxisCurves 三轴曲线

```csharp
struct MotionAxisCurves
{
    AnimationCurve XCurve;  float XScale;   // 左右位移 (米)
    AnimationCurve YCurve;  float YScale;   // 上下位移 (米)
    AnimationCurve ZCurve;  float ZScale;   // 前后位移 (米)

    bool HasAnyCurve;  // XCurve!=null || YCurve!=null || ZCurve!=null

    // 采样函数:
    Vector3 SampleLocalDelta(float t0, float t1, float motionScale);
    //   dX = (XCurve.Evaluate(t1) - XCurve.Evaluate(t0)) * XScale * motionScale
    //   dY = (YCurve.Evaluate(t1) - YCurve.Evaluate(t0)) * YScale * motionScale
    //   dZ = (ZCurve.Evaluate(t1) - ZCurve.Evaluate(t0)) * ZScale * motionScale

    Vector3 SampleLocalPosition(float t);
    //   X = XCurve.Evaluate(t) * XScale, Y/Z同理
}
```

### 典型曲线配置示例

| 动作类型 | X | Y | Z | 说明 |
|---------|---|---|---|------|
| 前冲刺 | 0 | 0→微抬→0 | EaseInOut 0→4m | 4m 冲锋 |
| 后撤步 | 0 | 0 | EaseInOut 0→-2m | 2m 后撤 |
| 跳跃攻击 | 0 | EaseInOut 0→3m→0 | Ease 0→2m | 弧形跳跃 |
| 空中落地 | 0 | GroundTargeted (忽略) | 0→1m | LandingCurve控制高度 |
| 原地技能 | 0 | 0 | 0 | 完全无位移 |
| 四向闪避 | 按方向旋转X/Z | 0 | EaseInOut 0→3m | CameraForward参考 |

---

## 运行时位移计算流程 (MotionExecutor)

```
MotionExecutor.Begin(profile, baseDuration, direction, startPos, baseAnimSpeed)
  ├── _motionScale = stats.GetMotionScale(profile.ScaleType)
  ├── GroundTargeted? → GroundLanding.TryResolveEndHeight → _groundStartY, _groundEndY
  └── _active = true

MotionExecutor.Tick(dt, timeScale, currentPosition)
  ├── FreezeNormalizedAdvance? → dtScale = 0  (Charge蓄力冻结)
  ├── _elapsed += dtScale
  │
  ├── TickAxisCurves(prevT, currT, dt):
  │     ├── GroundTargeted:
  │     │     localDelta = AxisCurves.SampleLocalDelta(prevT, currT, _motionScale)
  │     │     localDelta.y = 0  (重新计算)
  │     │     targetWorldY = GroundLanding.SampleTargetWorldY(_groundStartY, _groundEndY, landingCurve, currT)
  │     │     worldDeltaY = targetWorldY - _groundPrevWorldY
  │     │     localDelta.y = worldDeltaY
  │     │
  │     ├── Normal:
  │     │     localDelta = AxisCurves.SampleLocalDelta(prevT, currT, _motionScale)
  │     │     if YMotion == None → localDelta.y = 0
  │     │
  │     ├── worldDelta = LocalDeltaToWorld(localDelta)
  │     │     = Right * dx + Up * dy + Forward * dz
  │     │
  │     ├── desiredVelocity = worldDelta / dt
  │     └── SetDesiredVelocity + SetMotionComposeContext
  │
  ├── TickAnimSpeed(motionT):
  │     profileFactor = profile.SampleAnimSpeed(motionT)
  │     finalSpeed = baseAnimSpeed * profileFactor
  │     if playback.HasAnimatorSpeedOverride → override
  │     animSpeed.SetSpeed(finalSpeed)
  │
  └── SyncPostMotorPosition(transform.position)
```

---

## 曲线编辑管线 (Editor)

```
AnimationClip (FBX)
  ↓ ClipMotionExtractor
  ├── 逐帧采样根骨骼位置
  ├── 滤波 (MovingAverage window=5)
  ├── 拟合 (CatmullRom / Linear)
  └── → AnimationCurve
      ↓ MotionCurveGenerator
      ↓ MotionCurveFitPipeline
      → MotionProfileSO.AxisCurves (XYZ三轴)
          ↓ MotionAxisCurveEditorWindow (手工微调)
          ↓ MotionProfileEditor (Inspector总览)
          → 最终资产
```

---

## MotionProfile 与 Movement 控制权竞合

| 场景 | XZ位移控制者 | Y位移控制者 | 重力 |
|------|-------------|------------|------|
| Locomotion (Walk/Run/Idle) | `MoveByLocomotionIntent` | 重力系统 | `UseGravity` |
| Airborne (跳/落) | `MoveByLocomotionIntent(airMoveMultiplier)` | 重力系统 | `UseGravity` |
| Action + MotionProfile | `MotionExecutor.AxisCurves` | `MotionExecutor.AxisCurves` (或GroundTargeted) | 按`MotionProfile.Gravity` |
| Action (无MotionProfile) | 无脚本位移 | 重力系统 | `UseGravity` (可Suspend) |
| Action + Charge (Freeze) | 冻结 (位移暂停) | 冻结 | 保持不变 |

**控制权交接**: `PlayerActionState.OnEnter` → `BeginActionMotorSession()` → `MotionExecutor.Begin()`
**控制权归还**: `PlayerActionState.OnExit` → `MotionExecutor.End()` → `EndActionMotorSession()`

---

## [待验证] — 需在 Unity Editor 确认

1. **MotionProfileSO 资产总数**: 搜索 `t:MotionProfileSO`
2. **各Profile的AxisCurves配置**: 哪些有完整三轴, 哪些仅Z轴
3. **SourceClip引用**: 每个Profile引用了哪个动画
4. **MotionSpace分布**: CharacterForward vs CameraForward 使用比例
5. **GroundTargeted使用**: 哪些Profile用到GroundTargeted落地
6. **各处引用计数**: 哪些Profile被多个ActionDataSO共享
