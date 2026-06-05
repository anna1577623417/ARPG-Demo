ARPG 技能入口架构命名规划规范（AI 强约束版）


---

一、文档目标

本规范用于：

统一整个 ARPG 技能系统语义

并强制 AI 在后续代码生成中遵守：

技能入口语义

行为路由语义

Runtime 分层语义

输入与技能解耦语义


避免 AI 回退到传统：

SkillSlot = Skill

旧 MMO/MOBA 架构。


---

二、核心架构哲学


---

旧架构（禁止）

按钮 = 技能
技能槽 = 技能
Q = 火球术

典型命名：

PrimarySkill
SecondarySkill
UltimateSkill
QSkill
ESkill

问题：

强绑定 UI

强绑定当前玩法

无法支持上下文行为

无法支持技能变体

无法支持派生

无法支持运行时替换


属于：

“固定技能按钮思维”


---

新架构（强制）

按钮 = 行为入口
入口 = 路由请求
运行时决定最终行为

即：

Input
    ↓
Intent
    ↓
Entry
    ↓
Route
    ↓
Runtime

核心思想：

玩家按下的是“入口”

而不是：

固定技能


---

三、命名核心原则（AI 必须遵守）


---

原则1：禁止 UI 语义命名

禁止：

PrimarySkill
SecondarySkill
UltimateSkill
SkillButton
QSkill

原因：

这些命名属于：

UI展示层语义

而不是：

Gameplay Runtime 语义


---

原则2：禁止玩法耦合命名

禁止：

ComboSkill
ChargeSkill
SpecialAttackSkill

原因：

这些是：

行为类型

而不是：

入口

行为类型应该属于：

Route

而不是：

SkillSlot


---

原则3：入口必须中性化

入口层：

不表达具体行为

只表达：

“玩家触发了某个行为入口”

因此：

Skill_01

比：

PrimaryAttack

更高级。

因为：

Skill_01

没有预设玩法语义。


---

原则4：行为必须运行时决定

行为：

普攻

重击

蓄力

派生

连招

空中技


都属于：

Runtime Resolve Result

而不是：

输入层定义


---

四、标准命名规划（强制规范）


---

1. SkillEntrySlot


---

职责

表示：

玩家行为入口编号

注意：

它：

不是技能

不是玩法

不是行为类型


它只是：

输入入口标识


---

标准命名

public enum SkillEntrySlot
{
    Skill_01_LM,
    Skill_02_RM,

    Skill_03_Q,
    Skill_04_E,
    Skill_05_R,

    Skill_06_Key1,
    Skill_07_Key2,

    Skill_08_Mouse4,
}


---

设计原因


---

1. 去玩法语义

禁止：

Primary
Secondary
Ultimate

原因：

这些名称：

暗含玩法定义

会导致：

系统固化

AI错误推理

后续扩展困难



---

2. 保留输入来源可读性

例如：

Skill_03_Q

表示：

第3技能入口

当前默认绑定Q


但：

Q不是技能本体

只是：

当前输入绑定


---

五、Input Callback 命名规范


---

职责

表示：

输入事件生命周期


---

强制命名

OnSkill_01_Pressed()
OnSkill_01_Released()
OnSkill_01_Hold()


---

原则


---

1. 输入层禁止玩法语义

禁止：

OnPrimaryAttack()
OnUltimateSkill()

原因：

输入层：

不知道玩家最终执行什么行为


---

2. 输入层只表达输入状态

输入层只负责：

按下
抬起
持续

而不是：

技能逻辑


---

六、Intent 命名规范


---

职责

用于：

表达玩家行为意图

它是：

Input -> Gameplay

之间的桥梁。


---

强制命名

GameplayIntent.Skill_01
GameplayIntent.Skill_02


---

禁止命名

GameplayIntent.PrimaryAttack
GameplayIntent.Ultimate

原因：

Intent 层：

不应该预设最终行为


---

七、Route 命名规范


---

Route 是行为类型层

这一层：

才允许玩法语义

因为：

这里是真正：

决定行为模式

的位置。


---

标准 Route 命名

NormalRoute
ChargeRoute
ComboRoute
DerivativeRoute


---

Route 的职责


---

NormalRoute

普通即时行为：

普攻

单次施法

普通释放



---

ChargeRoute

蓄力行为：

长按检测

蓄力阶段

蓄力释放



---

ComboRoute

连段行为：

连招窗口

连段推进

Combo索引



---

DerivativeRoute

派生行为：

命中后追击

状态派生

Buff替换

上下文替换



---

Route 层原则


---

Route 可以表达玩法

因为：

它本身就是：

行为分发策略


---

八、Runtime 命名规范


---

Runtime 是真正执行层

这一层：

才是真正的技能运行时


---

推荐命名

SkillEntryRuntime
SkillRouteRuntime
SkillStageRuntime


---

分层职责


---

SkillEntryRuntime

负责：

当前入口运行状态

例如：

是否激活

当前锁定状态

当前入口CD

当前入口占用状态



---

SkillRouteRuntime

负责：

当前行为路由运行状态

例如：

当前ComboIndex

当前ChargeTime

当前派生状态



---

SkillStageRuntime

负责：

当前技能阶段运行状态

例如：

前摇

生效

后摇

取消窗口

派生窗口



---

九、AI 强约束规则（极重要）


---

AI 必须遵守


---

1. 禁止生成旧 MMO 语义

禁止生成：

PrimarySkill
SecondarySkill
UltimateSkill
QSkill


---

2. 禁止 SkillSlot 直接持有 SkillData

禁止：

SkillSlot -> SkillData

原因：

入口不等于技能。


---

3. 必须通过 Route Resolve

正确流程：

Entry
    ↓
Route Resolve
    ↓
Runtime Action


---

4. Route 才允许玩法语义

允许：

ChargeRoute
ComboRoute

禁止：

ChargeSkillSlot
ComboSkillSlot


---

5. Runtime 必须独立

禁止：

SkillData 内保存运行时状态

必须：

Config
    与
Runtime
完全分离


---

十、最终目标架构


---

最终推荐结构

InputAction
    ↓
GameplayIntent
    ↓
SkillEntrySlot
    ↓
SkillRoute
    ↓
SkillEntryRuntime
    ↓
SkillRouteRuntime
    ↓
SkillStageRuntime


---

十一、最终核心结论


---

现代 ARPG 技能系统：

不应该围绕“技能”设计

而应该围绕：

“行为入口”设计

因为：

真正变化的不是：

按钮

而是：

当前上下文下
玩家最终可以执行什么行为

这才是现代动作游戏技能系统的核心。