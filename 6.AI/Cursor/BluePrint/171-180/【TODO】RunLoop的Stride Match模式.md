【1】
==【Q】==Stride Matching放到那？motion吗？
这个步幅策略最重要的就是根据run/walk策略，而且我还希望移速可以让步幅匹配跟进，移速越快，位移也越快，步幅匹配要跟进，

---
==【A】==
我觉得你这个理解已经非常接近商业动作系统了，但有一个关键点需要纠正：

> Stride Matching 不属于 Motion。



它属于：

> Locomotion Runtime（运行时运动同步层）



如果放错位置，后面一定会很痛苦。


---

先说结论

你现在已经有：

Input
↓
Controller（Intent）
↓
FSM
↓
Playable Anim
↓
Motion
↓
CharacterMotor

我建议演化成：

Input
↓
Controller（Intent）
↓
FSM
↓
LocomotionRuntime
    ├─Stride Matching
    ├─Speed Smoothing
    ├─Foot Phase
↓
Playable Anim
↓
Motion
↓
CharacterMotor

其中：

Motion
负责：
"这一帧角色移动多少"

Stride Matching
负责：
"动画应该播多快"

两者不能反过来。


---

为什么不放 Motion？

因为 Motion 是：

> 位移源



例如：

Run：

velocity = 6m/s;
motor.Move(velocity * dt);

Sprint：

velocity = 10m/s;

Buff：

velocity *= 1.5;

减速：

velocity = MoveTowards(...)

这些都属于：

真实速度


---

而 Stride Matching 是：

> 动画适配真实速度。



即：

真实速度
↓

动画速度跟随

不是：

动画速度决定真实速度

否则：

以后：

减速Buff
加速Buff
泥地减速
联机同步

都会炸。


---

正确的流向

应该是：

Controller
↓

DesiredSpeed

↓

Motor

↓

CurrentSpeed

↓

StrideMatching

↓

Playable.SetSpeed()

例如：

motor.CurrentSpeed=7.5f;

locomotionRuntime.UpdateStride();

内部：

float ratio=
motor.CurrentSpeed/
profile.ReferenceSpeed;

playable.SetSpeed(ratio);


---

ReferenceSpeed 怎么来？

这个其实是 ActionProfile 的数据。

我反而建议：

放进 ActionProfile。

例如：

class ActionProfile
{
    AnimationClip Clip;

    MotionProfile Motion;

    float ReferenceDistance;

    float ReferenceDuration;

    bool EnableStrideMatching;
}

编辑器自动计算：

ReferenceSpeed
=
ReferenceDistance
/
ReferenceDuration;

例如：

Run：

Clip长度：0.8s

一个完整步循环：
4m

得到：

ReferenceSpeed

=

5m/s


---

Walk：

0.9s

1.8m

得到：

2m/s


---

Sprint：

0.7s

6.3m

得到：

9m/s


---

运行时怎么算？

例如：

当前：

Run状态：

配置：

ReferenceSpeed=5

实际：

MotorSpeed=7.5

则：

AnimationSpeed

=

7.5/5

=

1.5

动画：

播：

150%

脚步频率增加。


---

反过来：

泥地：

MotorSpeed=3

则：

3/5

=

0.6

动画：

慢下来。


---

你提到最关键的一句话：

> 我还希望移速可以让步幅匹配跟进，移速越快，位移也越快，步幅匹配要跟进。



其实商业游戏就是这么干的。

流程：

移速决定位移

↓

位移决定动画倍率

↓

动画匹配位移

而不是：

动画决定位移


---

Motion 在这里还有什么作用？

非常重要。

你现在有 Motion 曲线：



例如：

RunStart：

前0.2秒：

慢加速

后0.4秒：

快速推进

这种：

属于：

离散动作 Motion

Stride Matching 不参与。


---

真正开启 Stride Matching 的是：

Continuous Locomotion

例如：

Walk
Run
Sprint

我建议：

ActionProfile 增加：

bool EnableStrideMatching;

例如：

状态	Stride

Idle	×
Walk	√
Run	√
Sprint	√
RunStart	×
RunEnd	×
Pivot	×
JumpLand	×
Attack	×



---

更进一步（推荐）

其实你的 LocomotionRuntime 可以这样：

class LocomotionRuntime
{
    float CurrentSpeed;

    float FootPhase;

    float PlaybackSpeed;

    void UpdateStride()
    {
        PlaybackSpeed=
            CurrentSpeed/
            Profile.ReferenceSpeed;

        playable.SetSpeed(PlaybackSpeed);
    }
}

以后：

FootIK：

FootPhase
↓

左脚落地

右脚落地

也可以接进去。

这就是很多 AAA Locomotion 的核心。


---

最后，说一个很多人会踩的坑

你现在可能会想到：

> 那我把 Motion 曲线也跟着乘这个倍率？



千万别。

不要：

motionDelta*=strideRatio;

原因：

Continuous：

速度决定动画

是对的。

但是 Discrete：

RunStart
Pivot
Attack
Land

这些 Motion 是设计出来的。

它们的位移是：

动作的一部分

不能被移速污染。

否则：

攻击加速Buff

↓

攻击位移变远

或者：

慢速Debuff

↓

Pivot突然转不动

会非常诡异。


---

所以如果让我帮你落地，我会把职责切成：

ActionProfile
    ↓
ReferenceSpeed（数据）

Motor
    ↓
CurrentSpeed（真实速度）

LocomotionRuntime
    ↓
Stride Matching（动画倍率）

Playable
    ↓
SetSpeed()

Motion
    ↓
角色真实位移

用一句话概括就是：

> Motion 决定角色走多远，Stride Matching 决定动画迈多快；永远让真实速度成为唯一真相，动画负责追随它。



我甚至觉得，这个 LocomotionRuntime（Stride + FootPhase + SpeedSmoothing）很可能会成为你现有 Motion 系统旁边的第二个核心运行时模块。它不是编辑器功能，而是真正把“滑动的人”变成“迈步的人”的那层魔法。

---

==【2】==
【Q】
Stride Matching放到那？motion吗？
这个步幅策略最重要的就是根据run/walk策略，而且我还希望移速可以让步幅匹配跟进，移速越快，位移也越快，步幅匹配要跟进，

==【A】==

这个问题问得非常好，而且我觉得你已经触碰到一个很多商业项目都会纠结的问题：

> 连续 Locomotion（Run Loop）到底是 Motion 驱动，还是 Motor 驱动？



我的答案是：

> Run Loop 不启用 Motion，反而是最合理、最推荐的做法。



而且和 Stride Matching 是绝配。


---

先说结论

对于你的系统，我会这样分类：

类型	例子	MotionProfile	Stride Matching

连续Locomotion	Walk Loop、Run Loop、Sprint Loop	❌关闭	✅开启
启动动作	WalkStart、RunStart	✅开启	❌关闭
停车动作	WalkEnd、RunEnd	✅开启	❌关闭
转向动作	Pivot	✅开启	❌关闭
落地动作	JumpLand	✅开启	❌关闭
攻击动作	Attack	✅开启	❌关闭
闪避动作	Dodge	✅开启	❌关闭


其实就是一句话：

Continuous → Motor驱动
Discrete  → Motion驱动


---

Run Loop 应该怎么跑？

例如：

RunLoop：

ActionProfile：

EnableMotion = false;
EnableStrideMatching = true;
ReferenceSpeed = 5f;

运行时：

Motor：

CurrentSpeed = 7f;
Move(CurrentSpeed * dt);

然后：

Stride：

ratio = CurrentSpeed / 5f;

playable.SetSpeed(ratio);

假设：

ReferenceSpeed = 5m/s

那么：

实际速度：

3m/s
↓
动画0.6倍速

5m/s
↓
动画1倍速

7m/s
↓
动画1.4倍速

于是：

> 角色移动多快，动画迈步就多快。



脚不会滑。


---

为什么不让 RunLoop 开 Motion？

假设：

RunLoop Motion：

一圈RootMotion=4m

然后：

Buff：

移速+20%

会发生什么？

你就必须：

RootMotion × 1.2

然后：

动画速度 × 1.2

最后：

Motor 还要同步 ×1.2

系统就会变成：

Motion
↓
动画
↓
Motor
互相依赖

以后联机、Debuff、泥地、减速都会特别难搞。


---

为什么 AAA 都喜欢 Continuous 不用 RootMotion？

因为：

真正变化的是：

真实速度

例如：

黑神话：

跑步

5m/s
↓
6m/s
↓
7m/s

实际是：

CharacterMotor
决定速度

动画：

只是：

跟随速度

而不是：

动画把角色带着跑

这样：

减速Buff
加速Buff
网络同步
AI移动
导航

全部统一。


---

那 RunLoop 没 Motion，会不会很假？

不会。

因为：

真正让人觉得“假”的不是没有 RootMotion。

而是：

脚滑

脚滑来源于：

位移速度
≠
动画步频

Stride Matching 正是解决这个问题的。

只要：

MotorSpeed
=
动画步频对应速度

人眼就会觉得：

> “这个人在真正地跑。”




---

那 Motion 什么时候该用？

我建议：

只有“动作本身就是位移”时才开 Motion。

例如：

RunStart：

身体前倾
蹬地
冲出去

这个推进感：

属于动作。

开 Motion。


---

RunEnd：

惯性
刹车
恢复

属于动作。

开 Motion。


---

Pivot：

重心切换
急停转身

属于动作。

开 Motion。


---

JumpLand：

压缩
缓冲
站稳

属于动作。

开 Motion。


---

Attack：

突刺
滑步
冲拳

属于动作。

开 Motion。


---

RunLoop：

只是持续跑步

不属于动作。

关 Motion。


---

甚至我建议你直接在 ActionProfile 里这样分类

enum MovementMode
{
    MotorDriven,
    MotionDriven
}

例如：

RunLoop：

MovementMode = MotorDriven;
EnableStrideMatching = true;

RunStart：

MovementMode = MotionDriven;
EnableStrideMatching = false;

这样运行时：

switch(profile.MovementMode)
{
    case MotorDriven:
        motor.Move(...);
        UpdateStride();
        break;

    case MotionDriven:
        ApplyMotionProfile();
        break;
}

整个系统会非常清晰。


---

更进一步：移速成长怎么办？

你刚才提到的这个其实特别重要：

> 我希望移速越快，步幅匹配跟进。



例如：

装备：

基础移速：5m/s

升级：

移速：6m/s

Buff：

移速：8m/s

在这种情况下：

Motor：

CurrentSpeed
=
最终速度

Stride：

PlaybackSpeed
=
CurrentSpeed
/
ReferenceSpeed

自动：

5m/s → 动画1.0x

6m/s → 动画1.2x

8m/s → 动画1.6x

完全不用改 Motion。


---

但有一个上限问题

比如：

Run动画：

ReferenceSpeed = 5m/s

结果：

Buff后：

12m/s

动画：

2.4倍速

会像快进。

这时候商业项目一般会：

Walk：0~3m/s

Run：3~7m/s

Sprint：7~11m/s

超过：

1.3~1.5倍速

就切换到下一档 Locomotion。

例如：

Run 1.5x
↓
Sprint 1.0x

保持自然。


---

所以，如果让我给你的 Locomotion 定一个原则，那就是：

> Loop 动作（Walk/Run/Sprint）永远是 Motor 驱动 + Stride Matching；只有 Start/End/Pivot/Land/Attack 这种“具有明确动作语义的离散行为”才使用 Motion 驱动。



我反而觉得，你现在已经逐渐把系统分成了两条非常健康的主线：

Continuous Locomotion
= Motor + Stride

Discrete Actions
= Motion + Animation

这正是很多 AAA 动作游戏 Locomotion 架构最终会收敛到的形态。
