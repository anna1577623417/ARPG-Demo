这份计划书我会按照你目前的框架路线整理：

InputReader ↓ Ability System ↓ Stance System ↓ Route System ↓ Combat Graph ↓ Action ↓ Motion ↓ PlayableGraph 

目标不是做一个简单技能系统，而是支撑：

原神 只狼 鬼泣 怪猎 战神 

级别的3D动作框架。

蓝图38

Ability + Stance + PlayableGraph上半身混合系统

第一部分

Ability System

现状问题

目前项目：

Space → Jump Shift → Dodge LMB → Attack 

实际上属于：

输入绑定动作 

模式。

问题：

新增能力时：

飞行 攀爬 滑翔 二段跳 冲刺 格挡 锁定 

代码会快速膨胀。

并且：

角色拥有啥能力 

无法配置。

例如：

Boss战：

禁止跳跃 

必须写特殊逻辑。

设计目标

Ability回答：

角色是否拥有某种能力 

而不是：

角色当前播放什么动作 

Ability架构

Ability ↓ AbilitySpec ↓ AbilityTag 

例如：

Ability.Jump  Ability.Parry Ability.Climb Ability.Swim 

Ability.Dodge与动作强绑定，需要再抽象一层

角色：

GrantedAbilities { Jump Dodge Attack } 

输入：

Space 

检查：

HasAbility(Jump) 

允许：

JumpRoute 

否则：

拒绝 

Ability不是动作

错误：

JumpAction 就是Jump 

正确：

Jump Ability 决定能否跳 ↓ Jump Route 决定跳哪种 ↓ Jump Action 执行动作 

跳跃能力设计

JumpAbilityConfig

public class JumpAbilityConfig { int MaxJumpCount; float JumpForce; float GravityScale; float ApexGravityScale; float FallGravityScale; float CoyoteTime; float JumpBufferTime; } 

支持内容

普通跳

按一次跳 

二段跳

MaxJumpCount=2 

三段跳

MaxJumpCount=3 

蓄力跳

按住增加JumpForce 

可变高度跳

马里奥模式：

轻点 矮跳 长按 高跳 

实现：

松开Space 提前增加重力 

土狼时间

CoyoteTime

离开地面 0.15s 仍然允许起跳 

跳跃缓冲

JumpBuffer

落地前按下跳 自动触发 

动作手感核心。

高级跳跃玩法

墙跳

WallJump

Ability

滑墙

WallSlide

Ability

空中冲刺

AirDash

Ability

滑翔

Glide

Ability

飞行

Fly

Ability

钩锁

Grapple

Ability

全部属于：

Ability 

而非特殊代码。

跳跃是否纳入技能系统

答案：

推荐纳入

但不是普通技能。

设计：

Ability ↓ Ability Route ↓ Action 

JumpRoute：

GroundJump AirJump WallJump Glide 

这样：

Combat Graph：

Jump ├ GroundJump ├ AirJump ├ WallJump 

全部统一。

第二部分

Stance System

为什么需要Stance

同一个输入：

LMB 

普通状态：

攻击 

举盾状态：

盾击 

弓箭状态：

射箭 

所以：

输入 

本身没有意义。

意义来自：

当前姿态 

Stance定义

当前战斗模式 

例如：

Normal Shield Bow TwoHand DualBlade Magic Mounted 

Stance职责

决定：

输入映射 

例如：

Normal

Attack → NormalAttackRoute 

Bow

Attack → ShootRoute 

Shield

Attack → ShieldAttackRoute 

推荐结构

StanceDefinition { RouteMap } Input ↓ Stance ↓ Route 

Stance不要负责

错误：

播放动画 

错误：

伤害计算 

错误：

Motion 

只负责：

输入解释 

Stance未来扩展

武器姿态

单手剑 双手剑 长枪 法杖 

状态姿态

受伤 狂暴 潜行 

载具姿态

骑马 驾驶 飞行器 

全部统一。

第三部分

PlayableGraph上下半身分离系统

现状问题

普通Animator：

全身动画 

导致：

移动 + 攻击 

冲突。

攻击：

覆盖全身 

人物：

无法移动 

或者：

脚步错乱 

目标

实现：

下半身负责移动 上半身负责技能 

类似：

原神 只狼 怪猎 战神 

推荐结构

Locomotion Layer ↓ UpperBody Layer ↓ Output 

Layer1

Locomotion

负责：

Idle Walk Run Sprint JumpLocomotion 

骨骼：

全身 

Layer2

UpperBody

负责：

Attack Skill Aim Block Cast 

Mask：

Spine Chest Shoulder Arm Hand 

PlayableGraph结构

LocomotionMixer ↓ LayerMixer ↑ ActionMixer 

即：

AnimationLayerMixerPlayable 

运行流程

移动：

Locomotion Weight=1 

攻击：

UpperBody Weight=1 

结果：

边跑边砍 

Stance结合

举盾：

ShieldIdle 

Locomotion：

ShieldWalk ShieldRun 

Upper：

ShieldAttack ShieldBlock 

实现：

持盾移动 + 持盾攻击 

AimPose支持

射箭：

BowStance 

Playable：

AimPose + UpperBodyMask 

结果：

角色移动 身体瞄准 腿继续跑 

后续扩展

Additive Layer

用于：

呼吸 受击 抖动 

LookAt Layer

用于：

头部跟随目标 

Facial Layer

用于：

表情 

Procedural Layer

用于：

IK FootIK HandIK AimIK 

最终架构

InputReader ↓ AbilitySystem ↓ StanceSystem ↓ RouteSystem ↓ CombatGraph ↓ Action ↓ Motion ↓ PlayableGraph ├ Locomotion Layer ├ UpperBody Layer ├ Additive Layer ├ IK Layer └ Output 

这套结构最重要的一点是：

Ability 管“能不能做”；Stance 管“输入代表什么”；Route/CombatGraph 管“接下来走哪条行为链”；Action/Motion 管“实际执行”；PlayableGraph 管“最终怎么表现出来”。

这样以后你做二段跳、空中冲刺、滑翔、持盾、双刀、法杖、骑乘、飞行，甚至变身，都不用改核心架构，只是新增 Ability、Stance、Route 和对应资源配置。