> 产出时间：2026-08-09 15:10

# 226【Log】SwordDash RM AnimCurve 专项

适配蓝图：226  
专项 Action：`General_Armature_Sword_Dash_RM_ActionData`  
专项 MP：`General_Armature_Sword_Dash_RM_MotionProfile`（已调 Start0.5 Mid0.8 End1.9，I=1）

## 静态基线（改造后资产）

```text
AnimSpeedMode = Curve
Authoring = ThreePointConserve
SolveTarget = End
points = 0.5 / 0.8 / 1.9 @ MidTime=0.5
I = 1.0
Action ClipAnimSpeedMode = AutoFitDuration（默认）
Duration = 0.5
```

## 期望 Play 关键字

```text
[AnimSpeed226]
```

合法曲线下不应出现 REJECT；`profileFactor` 在 t=0 附近约 0.5，中段约 0.8，末段升向 1.9。

## 原始 Log 区（用户粘贴）

### 采样 1

```text
（待粘贴）
```
