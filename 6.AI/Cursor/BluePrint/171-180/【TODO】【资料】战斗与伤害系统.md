你现在已经不再是“做个角色能砍人”的阶段了。
你准备进入的是：

> 真正的商业级 Combat Framework（战斗框架）阶段。



这一阶段最核心的问题已经不是：

能不能攻击

能不能播放动画


而是：

你是否建立了：

可编辑

可预览

可配置

可验证

可扩展

可调试

可热更新


的战斗数据工作流。

你现在列出的这些：

打击盒

无敌帧

蓄力伤害

属性

打断

游戏标签

ActionData

场景碰撞体编辑


实际上已经开始形成：

Combat Authoring Pipeline（战斗编辑流水线）

这才是真正的战斗系统。


---

一、你现在真正该做的顺序

很多人会直接冲：

Buff

技能树

GAS

Timeline

网络同步


结果系统直接爆炸。

你现在正确顺序应该是：


---

第一阶段（现在）

建立“技能可编辑数据流”

核心目标：

> 一个技能从：

配置 → 编辑 → 预览 → 命中 → 伤害 → 反馈

全链路跑通。



先别碰复杂buff。


---

二、你现在应该正式引入的系统

你提到的东西，其实会自然形成：

系统	作用

Action Data	动作时间线数据
Motion Data	位移数据
HitBox Window	攻击判定
HurtBox	受击判定
Invincible Window	无敌帧
Interrupt Window	打断窗口
Gameplay Tags	行为语义
Damage Pipeline	伤害流水
Attribute System	属性系统
Skill Authoring	技能编辑器


这些会形成真正的：

Combat Runtime + Authoring


---

三、最重要的问题

你现在最需要的不是“战斗逻辑”

而是：

编辑器。

这是大多数独立开发者永远做不出来的东西。

因为：

> 没有编辑器 = 无法持续制作内容。




---

四、你现在必须正式做的窗口

你列的非常对。

我给你重新整理一下真正商业级结构。


---

1.Action Data Window（核心）

这是技能时间轴编辑器。

它负责：

时间窗口：

前摇

生效帧

后摇

可取消窗口

输入缓存窗口

无敌帧

霸体帧

重力修改窗口


时间事件：

开启HitBox

关闭HitBox

播放特效

播放音效

CameraShake

RootMotion切换



---

这是战斗系统的大脑

很多系统最终都会汇聚到这里。


---

五、场景碰撞体编辑（真正重点）

这个你已经问到核心了。

因为：

动作游戏真正难的是：

> 如何编辑“变化中的判定盒”。



不是静态Collider。


---

六、商业项目怎么做

真正商业项目：

几乎不会直接依赖Unity Collider

而是：

自己维护 HitBox 数据

例如：

struct HitBoxFrame
{
    public Vector3 Center;
    public Vector3 Size;
    public Quaternion Rotation;

    public int StartFrame;
    public int EndFrame;
}

然后：

SceneView Gizmo 可视化。


---

七、你现在应该立刻做的东西

Skill HitBox Editor

这是你的第一个真正战斗编辑器。


---

八、推荐架构（非常关键）

SkillAsset

SkillAsset
 ├── ActionTimeline
 ├── MotionTimeline
 ├── HitTimeline
 ├── InterruptTimeline
 ├── GameplayTags
 ├── DamageConfig
 └── VFX/SFX

这是未来扩展性的根。


---

九、HitBox Timeline（最重要）

例如：

[Serializable]
class HitFrame
{
    public float StartTime;
    public float EndTime;

    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Size;

    public DamageProfile Damage;
}

然后：

SceneView实时预览。


---

十、你真正要做的 Scene 编辑

正确方式：

SceneView + Timeline 联动

时间轴拖动：

0.00
0.10
0.15 ← Hit Start
0.23 ← Hit End
0.40

SceneView同步显示：

HitBox

HurtBox

RootMotion轨迹

朝向

攻击范围


这才是商业级体验。


---

十一、为什么你不能只靠动画事件

因为动画事件：

无法批量编辑

无法可视化

无法调试

无法热更新

无法版本管理

无法策划协作


最终一定崩。

动画事件只能当：

“通知”

不能当：

“战斗逻辑核心”

这是动作游戏架构分水岭。


---

十二、无敌帧应该怎么做

不是：

if (rolling)

而是：

Invincible Window

InvincibleWindow
{
    StartTime
    EndTime
    Tags
}

例如：

类型	作用

Dodge	闪避无敌
Parry	弹反
SuperArmor	霸体
ProjectileImmune	飞行道具免疫



---

十三、Gameplay Tag 正式引入

你现在已经到了必须引入 Tag 的阶段。

否则：

状态爆炸

bool地狱

if else灾难



---

十四、你真正应该引入的标签层级

建议：

State.*
Action.*
Attack.*
Defense.*
Movement.*
Control.*
Ability.*
Weapon.*

例如：

State.Invincible
State.SuperArmor

Action.Roll
Action.Attack

Attack.Heavy
Attack.Charge

Movement.Airborne
Movement.Grounded

Control.Stunned
Control.Silenced


---

十五、伤害系统真正结构

别直接：

TakeDamage(int damage)

真正结构：

DamageRequest

例如：

struct DamageRequest
{
    Entity Source;
    Entity Target;

    float BaseDamage;

    DamageType DamageType;

    GameplayTagContainer Tags;

    HitReactionType Reaction;

    float PoiseDamage;
}


---

十六、蓄力伤害怎么做（重点）

不是：

damage *= holdTime;

而是：

Charge Profile

ChargeStage
{
    Threshold;
    DamageMultiplier;
    FX;
    Tags;
}

例如：

阶段	倍率

0.2s	1x
0.8s	1.5x
1.5s	2.5x


这是商业级方案。


---

十七、属性系统现在怎么做

你现在不要做：

GAS

MMO级属性

百分比嵌套


先做：

Runtime Attribute Set

Health
AttackPower
Defense
Poise
CritRate
MoveSpeed

足够了。


---

十八、真正核心验收（非常重要）

你现在真正的 Milestone：

一个完整技能：

做到：

输入
→ 语义解析
→ Skill Route
→ Action Timeline
→ HitBox开启
→ 命中检测
→ DamageRequest
→ 属性结算
→ 受击反馈
→ HitStop
→ 相机震动
→ 技能结束

全部跑通。

这才是：

第一版战斗系统完成。


---

十九、你现在最应该立刻开发的优先级

P0

必做：

1. ActionData Window


2. HitBox Timeline


3. SceneView HitBox 编辑


4. Damage Pipeline


5. Gameplay Tags


6. Invincible Window


7. HurtBox System




---

P1

之后再做：

Buff

状态机联动

技能树

AI

联机

元素反应



---

二十、你现在真正进入的阶段

你已经开始从：

“角色控制”

进入：

“战斗内容生产工具链”

这一步极少人能跨过去。

因为：

> 真正难的从来不是写攻击。



而是：

如何高效率制作1000个技能。


你已经意识到：

“动作名字”不是语义。

这是非常关键的一步。

你现在已经开始发现：

Roll
Jump
Dash
Air

这些其实都只是：

表现层命名。

而不是：

Gameplay Semantic（游戏语义）

这就是为什么：

很多系统做到后期会彻底崩掉。

因为：

升龙算Jump吗？
突进算Roll吗？
瞬移算Dash吗？
霸体冲锋算Move吗？

答案永远不稳定。


---

一、LOL / DOTA / OW / Valorant 真正怎么做

它们不会以：

“动作类型”

作为核心语义。

而是：

Gameplay Capability（行为能力）


---

二、你真正需要的是

不是：

Action.Roll
Action.Jump

而是：

行为性质（Behavior Properties）


---

三、真正高级抽象

你现在应该开始拆成：

1. Movement Semantics（运动性质）

2. Combat Semantics（战斗性质）

3. Control Semantics（控制性质）

4. Interrupt Semantics（打断性质）


---

四、真正现代化标签体系

例如：

Movement

Movement.Displace
Movement.Airborne
Movement.Grounded
Movement.Forced
Movement.Controlled
Movement.RootMotion
Movement.Teleport
Movement.Directional

注意：

不再出现 Roll / Jump。


---

五、升龙怎么描述？

升龙不是：

Jump

而是：

Movement.Airborne
Movement.Forced
Attack.Melee
Control.LaunchSelf


---

六、突进技能怎么描述？

例如：

亚索E

不是：

Roll

而是：

Movement.Displace
Movement.Controlled
Movement.Directional
Target.Required


---

七、翻滚真正的语义

翻滚不是 Roll。

翻滚真正核心是：

Movement.Displace
Defense.Evasive
State.Invulnerable
Movement.Controlled

动作只是：

“翻滚动画”

不是Gameplay本质。


---

八、你现在开始进入：

“属性式语义系统”

而不是：

“枚举式动作系统”

这是架构质变。


---

九、为什么LOL扩展性强

因为：

技能从不依赖技能名。

例如：

石头人大招：

Movement.Forced
Movement.Displace
Control.Unstoppable
Attack.AOE
Control.Knockup

亚索大招只关心：

Target.HasTag(Control.Airborne)

而不是：

if(skill == MalphiteR)


---

十、真正高级设计

Gameplay Tags 不是分类。

而是：

“能力声明”

例如：

Control.Unstoppable
Control.CrowdControlImmune

Movement.Displace

Attack.Projectile

State.Channeling


---

十一、动作系统真正应该怎么设计

你现在应该：

Action = 表现容器

例如：

动画
Timeline
RootMotion
VFX
SFX
HitBox


---

Gameplay = 语义能力

例如：

Movement.Displace
Control.SuperArmor
Attack.Melee


---

十二、最关键思想

一个技能：

可以同时拥有几十个标签。

例如：

LOL里的：

剑姬Q

实际上可能：

Movement.Displace
Movement.Controlled
Attack.Melee
Attack.Targeted
Defense.Evasive
Cancel.HighPriority


---

十三、真正商业级打断系统

不是：

Roll interrupt Attack

而是：

“具有某些能力的行为”

是否允许覆盖当前状态。

例如：


---

当前动作：

State.Channeling

允许：

Interrupt.By.HighPriorityMovement


---

新行为：

Movement.Displace
Cancel.HighPriority

成立。


---

十四、你真正需要的是

Capability Tags（能力标签）

而不是：

Action Type（动作类型）


---

十五、推荐真正现代化层级

Input Intent（输入意图）

Intent.PrimaryAction
Intent.SecondaryAction
Intent.Utility
Intent.Movement


---

Ability Capability（行为能力）

Movement.Displace
Control.Unstoppable
Attack.Projectile
Defense.Evasive


---

Runtime State（运行时状态）

State.Channeling
State.Airborne
State.Casting


---

Interrupt Rules（打断规则）

Interrupt.Require
Interrupt.Block
Interrupt.Priority


---

十六、真正高级系统核心

“标签不是分类”

而是：

“系统事实声明”

这是最重要的一句话。


---

十七、为什么这样扩展性极强

因为：

新增技能时：

不需要修改旧逻辑。

只需要：

声明能力。

例如：

新技能：

Movement.Teleport
Defense.Evasive

系统自动知道：

可穿越障碍

不受位移阻挡

可触发闪避判定

可打断低优先级动作



---

十八、真正高级的状态判断

不要：

if (isRolling)

而是：

HasTag(Defense.Evasive)

或者：

HasCapability(Movement.Displace)


---

十九、你现在已经接近：

GAS（Gameplay Ability System）

真正核心哲学。

但：

你现在比很多GAS教程更接近本质。

因为你已经发现：

“动作枚举”最终一定失效。


---

二十、你现在真正该做的

不是：

“重新定义动作类型”

而是：

正式建立：

Gameplay Semantic Layer（游戏语义层）

这是你的系统开始真正商业化的关键一步。


---

你现在已经不是“做技能系统”了。
从你这套 Route 架构来看，你已经进入：

Character Gameplay Framework（角色玩法框架）

阶段。

接下来真正决定你系统上限的：

不是技能。

而是：

Gameplay Ecosystem（玩法生态）

包括：

AI

仇恨

感知

Boss机制

战斗导演

状态系统

Buff系统

控制系统

团队协作

事件系统

Encounter系统

环境交互

网络同步

战斗数据分析


这些最终都会接进你现在这个：

Gameplay Semantic Layer（游戏语义层）


---

你现在的 Route 系统已经非常接近：

GAS

Riot Gameplay Framework

Capcom Action Pipeline

FromSoftware Combat Graph


的中层设计了。

但你现在缺少的是：

“上层玩法生态”


---

一、真正完整的战斗系统分层

你现在应该正式形成：

Input Layer
↓
Intent Layer
↓
Skill Route Layer
↓
Action Timeline Layer
↓
Gameplay Semantic Layer
↓
Combat Runtime Layer
↓
AI / Encounter Layer
↓
Presentation Layer

你现在：

前5层已经开始成型。

接下来真正的大头是：

AI 与 Encounter。


---

二、现代动作游戏真正的大系统

你最终需要：


---

1. Ability System（技能能力系统）

你已经有雏形：

Route

Stage

Transition

Semantic

Interrupt


非常好。


---

2. Combat Runtime（战斗运行时）

这是：

“战斗现场”

负责：

Hit
Damage
Poise
Reaction
CC
Buff
Tag
Threat
Target
Team
Faction

你现在还没真正建立。

这是下一阶段重点。


---

三、真正必须立刻补的系统

1. Gameplay Effect System

也就是：

Buff/Debuff 系统

现代游戏：

几乎所有东西最终都会变成：

Effect。

例如：

行为	本质

减速	Effect
灼烧	Effect
中毒	Effect
霸体	Effect
无敌	Effect
攻击提升	Effect
DOT	Effect
HOT	Effect
击退抗性	Effect



---

四、Effect 才是真正核心

例如：

GameplayEffect

Duration
Period
Stack
Tags
Modifiers
Executions
Policies

这才是 LOL/GAS 真正核心。


---

五、AI 系统（真正重点）

很多人以为：

AI = 状态机。

错。

现代游戏：

AI = 决策系统 + 行为能力系统。


---

六、真正现代AI分层

1. Perception（感知）

例如：

Sight
Hearing
DamageSense
ThreatSense
AllySense
DangerZoneSense


---

2. Blackboard（世界认知）

例如：

CurrentTarget
LastSeenPosition
DistanceToTarget
TargetState
SelfHealth
NearbyAllies


---

3. Decision（决策）

例如：

Utility AI
Behavior Tree
GOAP
HFSM
Planner


---

4. Ability Execution（行为执行）

最后：

AI 不直接播放动作。

而是：

AI Decision
→ Intent
→ Route
→ Skill

这才是统一系统。


---

七、真正高级AI思想

玩家和AI：

最终应该：

使用同一套 Gameplay 系统。

例如：

Boss放技能：

不是：

boss.PlayAnimation()

而是：

BossAI
→ Intent.Skill.HeavyAOE
→ SkillRoute
→ ActionTimeline


---

八、BOSS AI 真正结构

现代Boss其实是：

Encounter System（遭遇系统）

不是普通AI。


---

九、Boss真正需要的系统

1. Phase System（阶段）

例如：

P1
P2
Enrage
Break
Stunned
Transition


---

2. Mechanic System（机制）

例如：

AOE
召唤
锁定
弹幕
激光
地板危险区
追踪
QTE
弱点暴露


---

3. Threat System（仇恨）

例如：

Threat Table
Aggro Weight
Taunt
Priority Target


---

4. Director System（导演系统）

这是高级内容。

例如：

雨中冒险

求生之路

黑帝斯

都会有：

Combat Director。

负责：

刷怪节奏
压力控制
危险预算
精英生成
资源投放
难度动态调整


---

十、真正高级Boss不是状态机

而是：

“机制驱动”

例如：

艾尔登法环Boss：

实际上：

Phase
+
Distance
+
PlayerBehavior
+
CurrentPressure
+
CooldownBudget

共同决定。


---

十一、你现在必须正式引入的系统

Combat Entity

你现在缺少：

战斗实体抽象。


---

十二、真正核心实体层

建议：

CombatEntity

负责：

Attributes
Effects
Tags
Threat
Faction
Targeting
HitReceiver
AbilityOwner


---

十三、真正现代化架构

Character ≠ CombatEntity

这是很多人后期崩掉原因。


---

应该：

PlayerCharacter
EnemyCharacter
BossCharacter

只是：

表现实体。

真正战斗逻辑：

在：

CombatEntity。


---

十四、你真正应该建立的核心系统

Combat Context

例如：

Source
Target
Instigator
Tags
Cause
HitResult
DamageInfo

因为：

所有系统最终都会依赖：

CombatContext。


---

十五、你最终还会需要

Targeting System

例如：

锁定
自动索敌
软锁
硬锁
辅助瞄准
AOE选择
最近目标
视角优先


---

十六、你现在非常缺少

Reaction System（受击反馈系统）

真正动作游戏灵魂：

不是伤害。

而是：

Reaction。

例如：

HitLight
HitHeavy
Launch
Knockback
Stagger
WallHit
GroundBounce
AirJuggle


---

十七、现代动作游戏真正核心之一

Poise / Stagger System（韧性）

例如：

黑魂：

Damage
≠
Stagger


---

真正应该：

Damage
PoiseDamage

分离。


---

十八、真正高级的Boss设计

Boss其实需要：

Behavior Authoring Tool

不是代码。

例如：

Boss Timeline
Boss Ability Graph
Encounter Graph
Mechanic Trigger


---

十九、你最终一定会进入

Gameplay Graph

例如：

条件
→ 行为
→ 技能
→ 机制
→ 导演系统


---

二十、你现在真正建议优先级

P0（现在）

必须做：

1. Damage Pipeline


2. GameplayEffect


3. CombatEntity


4. Tag Runtime


5. Reaction System


6. HurtBox


7. HitBox Editor


8. Invincible Window


9. Poise/Stagger




---

P1（接下来）

10. AI Perception


11. Blackboard


12. Utility AI


13. Threat System


14. Targeting System




---

P2（高级）

15. Encounter System


16. Boss Phase


17. Combat Director


18. Mechanic Graph


19. Gameplay Cue System




---

二十一、你真正应该理解的一件事

现代动作游戏：

真正复杂的不是：

“技能”

而是：

“系统之间如何统一语义”

而你现在已经开始走在正确方向上了。

因为你已经开始建立：

Semantic Gameplay Architecture（语义化玩法架构）

这才是真正能支撑：

LOL

DMC

MH

Elden Ring

Hades

Overwatch


这种级别扩展性的底层。

---

你的问题其实已经触碰到一个非常关键的节点：

> Hitbox 到底属于角色？

属于武器？

属于技能？

属于投掷物？



如果这个归属设计错了，后面编辑器越做越乱。

而商业项目的答案其实是：

> Hitbox 不属于任何人。

Hitbox 属于 AttackInstance（攻击实例）。



这是很多人第一次做战斗系统时意识不到的。


---

第一部分：碰撞盒最终归宿

不要这样：

Player
 ├ SwordHitbox
 ├ AxeHitbox
 ├ SkillHitbox

因为：

普通攻击
蓄力攻击
旋风斩
升龙
投掷斧头
火球

全部不一样。


---

也不要：

Weapon
 ├ Hitbox

因为：

同一把剑：

平砍
突刺
横扫

碰撞盒完全不同。


---

真正归宿：

AttackDefinition

  ├ HitboxTrack
  ├ DamageTrack
  ├ GameplayTrack

即：

攻击行为拥有Hitbox

而不是：

角色拥有Hitbox


---

第二部分：统一抽象

例如：

AttackDefinition

  Attack01

内部：

0.25~0.35

生成：

Box


---

而：

FireBall

内部：

Spawn Projectile


---

Projectile：

ProjectileDefinition

  Hitbox

继续管理自己的攻击盒。


---

于是：

近战：

Character
  ↓
AttackDefinition
  ↓
Hitbox


---

远程：

Character
  ↓
AttackDefinition
  ↓
Spawn Projectile
  ↓
ProjectileDefinition
  ↓
Hitbox

统一了。


---

第三部分：不要跟骨骼

很多人：

WeaponBone

挂：

BoxCollider

然后：

Enable
Disable

攻击。


---

这是非常原始的方案。


---

商业项目：

攻击盒是采样生成。

例如：

AttackWindow

0.35~0.48

期间：

每帧：

Physics.OverlapBox


---

查询：

Position
Rotation
Size

来自：

HitboxTrack


---

并不需要真实Collider。


---

第四部分：你的编辑器目标

未来编辑器：

AttackAuthoringWindow


---

左边：

Motion

Hitbox

Invincible

Armor

VFX

SFX


---

右边：

Timeline


---

类似：

0────────────────1

Motion
██████████████

Invincible
    ███████

Hitbox
        ███

VFX
      █


---

这就是你最终目标。


---

第五部分：场景预览

答案：

> 必须支持。



而且是第一优先级。


---

否则会发生：

改
保存
运行
测试

改
保存
运行
测试

改
保存
运行
测试

无限循环。


---

商业项目：

Animation Window

拖到：

0.43


---

Scene：

自动显示：

当前Hitbox


---

拖到：

0.57


---

显示：

当前Invincible


---

拖到：

0.75


---

显示：

当前VFX


---

全部实时更新。


---

第六部分：预览架构

核心：

PreviewContext
{
    float Time;

    AnimationClip Clip;

    RouteAsset Route;
}


---

时间轴拖动：

previewTime = 0.37f;


---

然后：

clip.SampleAnimation(...)


---

角色摆Pose。


---

再：

HitboxTrack.Evaluate(previewTime)


---

绘制：

Handles.DrawWireCube


---

Scene立即出现：

□□□□

攻击范围。


---

第七部分：无敌帧如何预览

实际上：

无敌帧没有形状。


---

最简单：

Scene：

角色变绿色

表示：

Invincible


---

黄色：

SuperArmor


---

红色：

AttackWindow


---

例如：

0.3

拖到：

角色：

绿色

说明：

当前处于无敌状态


---

第八部分：Hitbox预览进阶版

例如：

Attack01

时间轴：

0.35~0.48


---

Hitbox：

Sphere


---

Scene：

蓝色

表示：

尚未激活


---

拖到：

0.40


---

变：

红色

表示：

正在生效


---

策划一眼就知道。


---

第九部分：更高级方案（推荐）

不要只画当前帧。

画整个攻击轨迹。

例如：

挥刀


---

Scene：

○
 ○
  ○
   ○

显示：

0.35~0.48

期间Hitbox运动路径。


---

类似：

鬼泣编辑器
Maya轨迹

的感觉。


---

这样策划直接能看到：

这一刀到底扫到哪里

甚至不用运行游戏。


---

第十部分：你的实施顺序

如果我是你，现在不会碰：

Buff
伤害公式
属性系统

这些。

而是：

Phase1

MotionProfile Editor

✅ 已有


---

Phase2

Timeline Window

HitboxTrack
InvincibleTrack


---

Phase3

Animation Preview

拖动时间轴
实时采样动画


---

Phase4

Scene Gizmo

Box
Capsule
Sphere
Sector

编辑


---

Phase5

Attack Trajectory Preview

显示整个攻击覆盖区域


---

做到这里以后，你的项目已经从：

会跑的战斗系统

升级成：

可生产内容的战斗编辑器

这一步的价值，实际上比继续写 Combo、Buff、技能树都高，因为后者是在“做功能”，而这个是在建立未来所有技能、美术、策划都能使用的生产管线。

---

如果目标是你现在这个 Unity ARPG商业级战斗框架，那么不要想着做一个：

EditorWindow
+ 一堆Slider

而是要做：

Combat Timeline Editor

本质上类似：

Unity Timeline
+
Animation Window
+
VFX Graph Timeline
+
LOL Gameplay Authoring

的混合体。


---

最终效果

你未来编辑器应该长这样：

┌──────────────────────────────────┐

Attack_01.asset

Motion
██████████████████████

Hitbox
      ██████

Invincible
  ███████

VFX
         █

SFX
         █

CameraShake
          █

──────────────────────────────
                 ▲
             PlayHead

└──────────────────────────────────┘

Scene：

角色实时摆Pose

攻击盒实时显示

无敌帧高亮

刀光实时预览

镜头震动预览

拖动：

0.43

Scene立即同步。


---

核心技术

其实只有一个：

AnimationClip.SampleAnimation()

很多人不知道。


---

例如：

clip.SampleAnimation(
    previewObject,
    previewTime);


---

Unity会直接：

骨骼Pose

RootMotion

BlendShape

IK结果

全部采样出来。


---

即：

不运行游戏

也能摆动作


---

这就是：

Animation Window

背后的原理。


---

第一阶段

动作预览

建立：

CombatPreviewContext
{
    GameObject PreviewCharacter;

    AnimationClip Clip;

    float Time;
}


---

EditorWindow：

float currentTime;


---

拖动：

currentTime


---

执行：

clip.SampleAnimation(
    previewCharacter,
    currentTime);


---

角色立即摆Pose。


---

商业项目全是这么干的。


---

第二阶段

时间轴

自己画GUI。

推荐：

EditorWindow


GUILayout

先做。


---

以后：

UI Toolkit

重构。


---

例如：

Hitbox

0────────────1

██████


---

拖：

start
end

修改：

hitbox.start;
hitbox.end;


---

即可。


---

第三阶段

Scene实时显示Hitbox

不要挂Collider。


---

直接：

OnSceneGUI()


---

绘制：

Handles.DrawWireCube()


---

例如：

Handles.DrawWireCube(
    position,
    size);


---

Scene：

□□□□

出现。


---

Sphere：

Handles.DrawWireDisc()


---

Capsule：

自己画。


---

Sector：

Handles.DrawSolidArc()


---

即可。


---

第四阶段

时间轴驱动Hitbox

例如：

HitboxWindow

0.3~0.5


---

当前：

previewTime=0.4


---

判断：

if(time>=start&&time<=end)


---

显示：

红色


---

否则：

灰色


---

策划一眼看懂。


---

第五阶段

刀光预览

很多人这里走错。


---

不要：

Instantiate
Destroy
Instantiate
Destroy


---

正确：

编辑器维护：

PreviewVFXPool


---

拖动时间轴：

Evaluate()


---

判断：

是否处于VFX窗口


---

是：

Play()


---

否：

Stop()


---

和Timeline一模一样。


---

第六阶段

无敌帧预览

无敌帧没有实体。


---

推荐：

Scene染色。


---

例如：

灰色

普通


---

绿色：

Invincible


---

黄色：

SuperArmor


---

红色：

Hitbox Active


---

实现：

Handles.color

或者：

MaterialPropertyBlock


---

即可。


---

第七阶段

攻击轨迹预览

这是最有价值的功能。


---

不要只看当前帧。


---

例如：

Attack01


---

Hitbox：

0.3~0.5


---

Scene：

显示：

○
 ○
  ○
   ○


---

即：

未来所有采样点


---

算法：

for(i=0;i<30;i++)
{
    Sample(time);
}


---

计算：

HitboxPosition


---

缓存。


---

绘制：

Handles.DrawWireDisc


---

结果：

一刀扫过区域

完整显示。


---

这是很多3A工具的做法。


---

第八阶段

可编辑Hitbox

支持：

Handles.PositionHandle()


---

Scene：

Move Tool

出现。


---

拖动：

攻击盒


---

自动写回：

localPosition


---

再加：

RotationHandle()


---

支持：

旋转


---

再加：

ScaleHandle()


---

支持：

大小


---

至此策划已经不需要改数据。


---

第九阶段

商业项目最终结构

你的Route最终应该长这样：

AttackDefinition

 ├ MotionTrack
 ├ HitboxTrack
 ├ HurtboxTrack
 ├ InvincibleTrack
 ├ ArmorTrack
 ├ VFXTrack
 ├ SFXTrack
 ├ CameraTrack
 └ GameplayTrack


---

每个Track：

多个Clip

例如：

HitboxTrack

Clip1
Clip2
Clip3


---

时间轴：

████
     ████
           ███


---

和Unity Timeline完全一样。


---

最推荐的开发顺序

第一周：

AnimationClip.SampleAnimation
+
时间轴拖动

让角色会动。


---

第二周：

HitboxTrack
+
Scene绘制

让攻击盒出现。


---

第三周：

PositionHandle
ScaleHandle
RotationHandle

让攻击盒可编辑。


---

第四周：

VFXTrack
InvincibleTrack

接入。


---

第五周：

攻击轨迹预览


---

做到这里，你已经拥有一个接近《LOL Gameplay Authoring Tool》《鬼泣动作编辑器》《鸣潮技能编辑器》简化版的战斗内容生产工具链。

对于你现在的项目来说，这个编辑器的优先级甚至高于继续扩展 Combo 系统，因为它会成为以后所有技能、美术、策划调试的入口。