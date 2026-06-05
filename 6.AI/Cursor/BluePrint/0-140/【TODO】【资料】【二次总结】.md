我把你提供的资料全部串起来看了一遍后，结论其实非常明确：

你现在不是要继续扩展 CombatGraph。

你现在要完成的是：

# v4.4 架构收敛

把：

- SkillEntry
    
- SkillGroup
    
- Route
    
- CombatFlow
    
- GameplayTag
    
- InterruptWindow
    

真正变成一套职责清晰的系统。

而不是继续增加新概念。

从你现有实现来看，CombatGraph 已经具备基础能力，但职责边界仍然模糊。

而标签系统已经具备 State / Ability / Mechanic 五轨体系，不需要推翻重做。

所以这次方案遵循：

> 能不改标签体系就不改标签体系
> 
> 能不改 CombatGraph 就不改 CombatGraph
> 
> 重点是重新划分职责边界

---

# 第一部分

# 问题分析（30%）

---

# 问题1

# 四向翻滚职责错位

目前 CombatGraph 已经支持：

```text
Idle
 + Space
 + Direction
 ↓
 DodgeForward
```

这种配置。

但这是一个设计错误。

因为：

```text
Idle
→ 第一个行为
```

本质属于：

```text
SkillEntry Resolution
```

而不是：

```text
Combat Flow
```

Combat Flow 应该解决：

```text
AttackA
→ AttackB

AttackA
→ Dodge

AttackA
→ JumpCancel
```

这种：

```text
Route
→ Route
```

的问题。

而不是：

```text
Input
→ Route
```

的问题。

---

结果导致：

现在系统出现：

```text
SkillEntry 在选 Route

CombatGraph 也在选 Route
```

双入口。

职责重叠。

---

# 问题2

# CombatGraph 挂 Player 不合理

当前：

```text
Player
 ├─ SkillEntryService
 └─ CombatGraphRuntime
```

这意味着：

CombatGraph 变成：

```text
角色级行为图
```

但实际上：

CombatGraph 本质是：

```text
当前技能集的行为图
```

例如：

```text
剑士
AttackA
AttackB
Dodge
Parry
```

一套图。

---

法师：

```text
CastA
CastB
Teleport
```

另一套图。

---

未来：

```text
职业切换
武器切换
形态切换
```

都会导致：

```text
Graph切换
```

而不是：

```text
Player切换
```

所以：

CombatGraph 应该属于：

```text
SkillLoadout
```

而不是：

```text
Player
```

---

# 问题3

# Ability 与 State 职责混乱

你现在已经有：

```text
Ability Tag
```

轨道。

但是：

它仍然承担：

```text
CanJump
CanDodge
CanLightAttack
```

这种动作级能力。

这其实已经暴露出问题。

因为未来：

```text
Jump
Dodge
Roll
SwordDash
Ascend
SwimUp
WallRun
```

是不确定的。

---

Ability 不应该描述：

```text
具体动作
```

而应该描述：

```text
能力语义
```

例如：

```text
Ability.Mobility

Ability.Attack

Ability.Defense

Ability.Counter

Ability.Interaction
```

---

# 第二部分

# 落地蓝图（70%）

---

# 目标1

# 跑通四向位移 Group

---

不要再走：

```text
CombatGraph
```

方案。

改为：

```text
Entry
↓
Group
↓
Route
```

---

新增：

```csharp
RouteSelectionContext
{
    MoveDirection8 MoveDir;
    bool Grounded;
    bool Airborne;
    bool LockOn;
}
```

---

新增：

```csharp
IRouteSelector
```

```csharp
SkillRouteDefinition Resolve(
    RouteSelectionContext ctx);
```

---

实现：

```csharp
DirectionalRouteSelector
```

---

例如：

```text
DodgeGroup

Forward
→ DodgeForward

Backward
→ DodgeBackward

Left
→ DodgeLeft

Right
→ DodgeRight
```

---

SkillEntry：

```text
Space
 ↓
 MobilityGroup
 ↓
 DirectionalSelector
 ↓
 Route
```

---

这样：

未来：

```text
飞行角色
```

直接换：

```text
MobilityGroup

Forward
→ DashForward

Backward
→ AirBrake

Left
→ StrafeLeft

Right
→ StrafeRight
```

无需修改任何代码。

---

# 目标2

# 引入 Context Group

这是你真正缺的东西。

---

新增：

```csharp
SkillGroupDefinition
```

支持：

```text
Directional
Grounded
Airborne
StateTag
AbilityTag
```

决策。

---

例如：

```text
MobilityGroup
```

配置：

```text
Grounded
 → DodgeGroup

Airborne
 → AirDashGroup

Swimming
 → SwimMoveGroup
```

---

形成：

```text
Entry
↓
Context Group
↓
Route Group
↓
Route
```

---

这比 CombatGraph 更适合：

```text
第一个动作选择
```

---

# 目标3

# CombatGraph迁移到Loadout

---

当前：

```text
Player
 └─ CombatGraph
```

---

调整：

```text
SkillLoadout
 ├─ Entries
 ├─ Groups
 ├─ CombatFlow
 └─ AbilityMap
```

---

SkillEntryService.Rebuild：

```csharp
AttachGraph(loadout.Flow);
```

---

Player：

不再持有：

```csharp
CombatGraphRuntime
```

---

只持有：

```csharp
SkillEntryService
```

---

这样：

切武器：

```text
LoadoutA
→ LoadoutB
```

即可切换：

```text
Flow
```

---

# 目标4

# 重构 Ability 系统

---

新增：

```csharp
AbilityTag
```

不要复用当前 CanJump 系列。

---

推荐：

```text
Ability.Attack

Ability.Mobility

Ability.Defense

Ability.Counter

Ability.Special

Ability.Interaction
```

---

删除未来扩展：

```text
CanRoll
CanJump
CanDash
CanSwim
```

思路。

---

角色只配置：

```text
Ability.Mobility
```

---

AbilityMap：

```text
Ability.Mobility
 ↓
 MobilityGroup
```

---

这样：

不同职业：

```text
Mobility
```

实现完全不同。

---

# 目标5

# 打断窗口升级

现有：

```text
AllowInterruptByDodge

AllowInterruptByJump
```

建议逐步废弃。

---

改为：

```text
AllowAbilityTags
```

例如：

```text
Ability.Mobility

Ability.Defense
```

---

时间轴：

```text
0.3~0.8
Allow:
 Mobility
```

---

此时：

玩家按：

```text
Space
```

↓

解析：

```text
Ability.Mobility
```

↓

查 AbilityMap

↓

得到：

```text
MobilityGroup
```

↓

选择：

```text
DodgeForward
```

---

整个链路完全解耦。

---

# 目标6

# 最终统一架构

最终我建议你收敛到：

```text
Input
 ↓
SkillEntry

 ↓
AbilityMap

 ↓
ContextGroup

 ↓
RouteGroup

 ↓
Route

Route运行
 ↓
Timeline

 ├─ Hitbox
 ├─ VFX
 ├─ SFX
 ├─ StateTag
 └─ PermissionWindow

PermissionWindow
 ↓
CombatFlow

CombatFlow
 ↓
Next Route
```

这里职责非常清晰：

|系统|职责|
|---|---|
|StateTag|当前状态是什么|
|Ability|当前能做什么|
|SkillEntry|输入是什么|
|Group|该选哪个 Route|
|Route|执行动作|
|Window|当前允许什么|
|CombatFlow|如果允许，下一步去哪|
|EffectSystem|Route执行后的效果投递|

---

# v4.4实施顺序（非常重要）

不要同时改。

按照下面顺序推进：

### Phase1（必须先完成）

Ability MVP

```text
Ability.Attack
Ability.Mobility
Ability.Defense
```

跑通。

---

### Phase2

MobilityGroup

```text
Space
→ 四向翻滚
```

跑通。

---

### Phase3

CombatGraph迁移到Loadout

完成。

---

### Phase4

Window支持：

```text
AllowAbilityTags
```

---

### Phase5

AttackA

```text
→ DodgeCancel
→ JumpCancel
→ AttackB
```

全部走：

```text
Window + CombatFlow
```

验证。

---

完成这五步后，你的系统就会形成：

```text
Ability
↓
Group
↓
Route
↓
Window
↓
Flow
```

这条主干线。

后面的：

```text
EffectSystem
Buff
Environment
Swimming
Climbing
Flying
Boss技能树
```

都可以直接挂进来，而不用再次推翻架构。

---

如果只讨论：

- Action.Category
    
- Interrupt Window 标签
    

并且彻底摆脱：

```text
Dodge
Jump
Roll
SwordDash
```

这种具体动作名。

那么我现在不会再设计：

```text
Movement
Offense
Defensive
Utility
```

也不会设计：

```text
AllowDodge
AllowJump
AllowSkill
```

了。

因为它们都停留在：

```text
具体行为
```

层。

而你的系统已经进入：

```text
技能系统
CombatFlow
AbilitySystem
```

阶段。

应该提升一个抽象层级。

---

# Action.Category

我建议最终版：

```csharp
[Flags]
public enum ActionCategory : ushort
{
    None         = 0,

    Mobility     = 1 << 0,
    Attack       = 1 << 1,
    Defense      = 1 << 2,
    Counter      = 1 << 3,
    Special      = 1 << 4,
    Interaction  = 1 << 5,
}
```

---

# Mobility

表示：

```text
位置重构
```

而不是：

```text
翻滚
跳跃
冲刺
```

---

例如：

```text
Roll
Jump
Dash
Blink
Teleport
WallRun
AirDash
SwimUp
FlyBoost
```

全部属于：

```text
Mobility
```

---

# Attack

表示：

```text
主动进攻
```

例如：

```text
轻攻击
重攻击
空中攻击
技能攻击
终结技
```

---

# Defense

表示：

```text
防御行为
```

例如：

```text
格挡
举盾
防御架势
```

---

# Counter

表示：

```text
反制行为
```

例如：

```text
弹反
反击
受击反制
Guard Counter
```

---

# Special

表示：

```text
特殊能力
```

例如：

```text
Q
E
R

职业技能

武器技能
```

---

# Interaction

表示：

```text
世界交互
```

例如：

```text
拾取
开门
对话
机关
载具
```

---

这套最大的优点：

未来出现：

```text
Flying
Swimming
Climbing
Grapple
Zipline
```

完全不用加 Category。

---

# Interrupt Window

重点来了。

这里我不建议：

```text
AllowMobility
AllowAttack
```

这种 bool。

---

而是：

```csharp
AllowedCategories
```

直接复用 Action.Category。

---

例如：

```csharp
Window
{
    AllowedCategories =
        Mobility |
        Defense;
}
```

---

含义：

```text
当前时间窗

允许 Mobility 类动作进入

允许 Defense 类动作进入
```

---

不允许：

```text
Attack
Special
```

进入。

---

# 举例

攻击A

```text
0.0~0.3
AllowedCategories = None
```

完全锁死。

---

```text
0.3~0.8
AllowedCategories =
Mobility
```

允许：

```text
翻滚
跳跃
空中冲刺
闪现
```

全部取消。

---

```text
0.6~0.9
AllowedCategories =
Mobility
| Attack
```

允许：

```text
翻滚取消

以及

Attack_A → Attack_B
```

---

# 更进一步

如果以后你引入 AbilitySystem。

甚至可以这样：

```csharp
ActionCategory
```

只用于：

```text
动作语义
```

---

而 Window：

```csharp
AllowedAbilityTags
```

用于：

```text
许可
```

---

例如：

```text
Ability.Mobility

Ability.Attack

Ability.Defense
```

---

这样：

Action 和 Ability 解耦。

但目前你的项目还没到那一步。

---

# 我认为最适合你当前项目的最终版

## ActionDataSO

```csharp
Category
{
    Mobility,
    Attack,
    Defense,
    Counter,
    Special,
    Interaction
}
```

---

## ActionWindow

```csharp
AllowedCategories
```

而不是：

```text
dodge_interrupt

jump_interrupt

light_attack_interrupt

heavy_attack_interrupt
```

全部删掉。

---

最终编辑器会变成：

```text
Action_A

Window #1

0.30 ~ 0.80

Allowed Categories

☑ Mobility
☑ Defense
☐ Attack
☐ Counter
☐ Special
☐ Interaction
```

看到这个配置。

设计师立刻知道：

```text
这个窗口允许位移取消

允许防御取消

但不能连招

不能接技能
```

这比：

```text
AllowDodge
AllowJump
AllowSwordDash
AllowAirDash
AllowBlink
```

这种不断膨胀的列表更稳定，也更符合你后面要接 Ability、CombatFlow、Environment 的发展方向。