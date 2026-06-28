这个计划书我建议直接定位成：

> 【蓝图】战斗相机状态机（Camera State Machine）与镜头表现系统（Camera Authoring）设计方案



目标不是做一个简单跟随相机。

而是构建未来：

普攻
连段
弹反
处决
锁定
闪避
技能
大招
Boss演出

统一使用的镜头系统。


---

【蓝图】战斗相机状态机与镜头表现系统设计方案

---

一、项目背景

当前战斗框架已经拥有：

Motion System
Combat Graph
Action Timeline
HitBox
Invincible
FX
Audio

这些系统已经负责：

动作执行
伤害判定
无敌帧
表现层特效

但是：

缺少：

Camera Layer（镜头层）

导致：

动作已经发生

但玩家感知不足。

例如：

同样一个攻击动作：

无镜头处理：

60分

增加镜头推进：

75分

增加镜头震动：

85分

增加镜头锁敌：

90分

增加镜头轨迹：

95分

因此需要建立：

统一的战斗镜头框架。

---

二、设计目标

建立：

Camera State Machine

+ 

Camera Track

+ 

Camera Effect

+ 

Camera Preset

四层结构。

---

三、总体架构

Player Input
↓

Combat Graph
↓

Action
↓

Camera Layer
↓

Cinemachine

---

职责划分：

Combat：

决定发生什么

Camera：

决定如何表现

---

禁止：

Action直接控制Camera

应改为：

Action触发Camera事件

由Camera系统执行。

---

四、Camera State Machine

相机采用状态机管理。

---

FreeLook

自由探索

职责：

第三人称跟随

支持：

旋转
缩放

---

Combat

战斗状态

特点：

镜头距离更近

角色保持屏幕中心

目标优先显示

---

LockOn

锁定状态

特点：

角色与敌人同时入镜

允许环绕

禁止失焦

---

Attack

攻击状态

特点：

轻微推进

轻微FOV变化

短时恢复

---

HeavyAttack

重攻击

特点：

推进更明显

允许震屏

允许慢动作

---

Dodge

闪避状态

特点：

镜头跟随位移

提升速度感

---

Skill

技能状态

特点：

允许镜头轨迹

允许动态FOV

允许特殊跟踪

---

Execution

处决状态

特点：

完全接管镜头

进入演出模式

---

Ultimate

大招状态

特点：

进入Cinematic模式

暂时脱离自由相机

由Timeline控制

---

五、Camera Track设计

新增：

Camera Track

作为Action Timeline组成部分。

---

Motion Track

HitBox Track

Invincible Track

FX Track

Audio Track

Camera Track

TimeScale Track

---

每个Action都可以编辑镜头。

---

示例：

Attack01

0.00

CameraPush

0.15

FOVZoom

0.30

Shake

0.50

Restore

---

实现：

技能数据驱动镜头表现。

---

六、镜头效果库

镜头效果统一模块化。

禁止每个技能单独实现。

---

Camera Push

镜头推进

作用：

增强冲击力

参数：

Distance

Duration

Curve

---

Camera Pull

镜头后撤

作用：

增强空间感

---

FOV Zoom

动态视角变化

作用：

强化力量感

---

Shake

震屏

作用：

强化重量感

参数：

Amplitude

Frequency

Duration

---

Orbit

镜头环绕

作用：

展示角色动作

---

Target Focus

目标聚焦

作用：

增强战斗阅读性

---

LookAt Override

强制观察目标

作用：

演出镜头

---

Camera Roll

镜头倾斜

作用：

速度感

危险：

过度使用容易晕

限制：

≤5°

---

Slow Motion

时间缩放

作用：

强化打击点

---

Freeze Frame

顿帧

作用：

制造打击停顿

常用：

1~4帧

---

七、镜头预设系统

建立：

CameraPreset

SO资产。

---

LightAttack

轻攻击镜头

---

HeavyAttack

重攻击镜头

---

Parry

弹反镜头

---

Dodge

闪避镜头

---

Execution

处决镜头

---

BossSkill

Boss技能镜头

---

优点：

策划可直接复用。

---

八、编辑器需求

Action Timeline新增：

Camera Track

---

支持：

新增事件

拖动

复制

批量编辑

预览

---

Scene视图实时同步。

---

支持：

播放时：

看到：

相机位置

观察方向

FOV变化

轨迹

Shake区域

---

九、验收标准

一级验收

---

支持：

Push

Pull

Shake

FOV

四种效果

---

支持：

技能驱动

---

支持：

编辑器预览

---

二级验收

---

支持：

LockOn

Skill

Execution

状态切换

---

支持：

Camera Preset

---

三级验收

---

支持：

镜头轨迹编辑

---

支持：

Timeline控制

---

支持：

Boss演出

---

十、未来扩展

后续接入：

Cinemachine

Timeline

PlayableGraph

---

最终形成：

Action Authoring

↓

Camera Authoring

↓

Combat Presentation

统一编辑链路。

达到：

只狼

鬼泣

原神

绝区零

同类战斗表现架构。
---

我对你当前项目的建议

按照优先级排序：

P0 Camera State Machine
P0 Camera Push
P0 Camera Shake
P0 FOV Zoom

P1 Camera Track
P1 LockOn Camera
P1 Camera Preset

P2 Orbit
P2 Execution Camera
P2 Timeline Camera

P3 Cinematic Camera
P3 Boss演出

先别碰复杂轨迹和大招电影镜头。

因为《只狼》80%的高级感其实来自：

锁敌
推进
FOV
震屏
顿帧
慢动作

这几个基础模块。

当这几个做好以后，你的 Combat Graph + Motion Timeline + Camera Timeline 基本就已经进入商业ARPG战斗编辑器的范畴了。