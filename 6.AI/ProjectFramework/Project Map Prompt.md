PROJECT KNOWLEDGE SKILL

Role

你正在参与一个大型 Unity AAA Action Combat Framework 项目。

项目规模可能达到：

- 30,000+
- 100,000+
- 300,000+

代码量。

禁止直接全盘扫描源码。

在进行任何分析之前，必须优先利用 Project Knowledge Base。

---

核心原则

永远遵循：

Knowledge First

Code Second

Implementation Third

即：

KnowledgeBase
↓

System Understanding
↓

Targeted Code Scan
↓

Architecture Analysis
↓

Implementation Discussion

---

工作流程

收到任务后：

禁止立即扫描工作区。

必须先执行：

Step 1

读取：

AI_KnowledgeBase/

目录。

优先阅读：

00_ProjectMap.md

01_FrameworkOverview.md

02_SystemInteraction.md

09_TechDebt.md

---

Step 2

建立：

当前问题

涉及哪些系统

例如：

用户提问：

Jump Land 被移动打断

则先定位：

Jump

Land

Movement

Action

StateMachine

Motion

---

Step 3

利用知识库确定：

需要扫描哪些目录

例如：

不要：

扫描整个项目

而是：

仅扫描：

Character/

Movement/

Action/

Motion/

相关目录

---

Step 4

建立：

问题相关系统图

例如：

Movement
↓
PlayerState
↓
Action
↓
Motion

确认涉及模块。

---

Step 5

仅扫描必要代码

禁止：

全盘重新扫描。

优先：

精准定位。

---

KnowledgeBase 权威等级

当知识库与猜测冲突时：

以知识库为准。

当知识库与代码冲突时：

以代码为准。

并记录：

Knowledge Drift

知识漂移。

---

扫描预算原则

优先级：

Level 1

KnowledgeBase

成本：

极低

必须优先使用。

---

Level 2

相关目录

成本：

低

允许扫描。

---

Level 3

相关类

成本：

低

允许扫描。

---

Level 4

全项目扫描

成本：

极高

禁止默认执行。

仅当：

知识库不足

且无法确认系统关系

时允许。

---

问题分类路由

战斗问题

优先读取：

06_ActionCatalog.md

07_MotionCatalog.md

02_SystemInteraction.md

然后扫描：

Combat/

Action/

Motion/

Graph/

---

移动问题

优先读取：

08_StateMachineCatalog.md

09_TechDebt.md

然后扫描：

Movement/

Character/

StateMachine/

---

动画问题

优先读取：

01_FrameworkOverview.md

02_SystemInteraction.md

然后扫描：

Animation/

Playable/

Motion/

---

编辑器问题

优先读取：

04_EditorPipeline.md

然后扫描：

Editor/

Authoring/

Graph/

Inspector/

---

架构升级问题

优先读取：

全部KnowledgeBase

然后：

仅扫描相关系统。

---

技术债务优先检查

任何系统分析前：

必须检查：

09_TechDebt.md

确认：

是否属于已知问题。

例如：

Movement绕过Action

Motion控制权冲突

Jump未Action化

Land未Action化

TurnAround未Action化

禁止重复分析已经确认的问题。

---

系统分析输出规范

每次分析必须输出：

当前问题

问题描述

---

涉及系统

列出系统。

---

知识库事实

引用KnowledgeBase结论。

---

代码事实

引用实际代码。

---

差异分析

知识库与代码是否一致。

---

影响范围

列出影响模块。

---

升级建议

说明升级方向。

---

架构升级模式

当用户要求：

重构

升级

统一

兼容

扩展

时：

必须先回答：

当前架构是什么

然后回答：

目标架构是什么

最后回答：

迁移路径是什么

禁止直接重构。

---

Combat Framework 特殊规则

本项目默认采用：

Action
作为战斗最小执行单元。

MotionProfile
作为位移描述层。

CombatGraph
作为行为编排层。

PlayableGraph
作为动画表现层。

StateMachine
作为状态管理层。

任何分析必须优先检查：

是否存在绕过上述体系的实现。

例如：

Locomotion直接播放Clip

Jump直接播放Clip

Land直接播放Clip

TurnAround直接播放Clip

Movement直接修改Transform

这些均视为潜在架构债务。

必须重点标记。

---

最终目标

通过KnowledgeBase进行系统理解。

通过精准扫描进行事实验证。

避免重复全盘扫描。

降低Token消耗。

提高架构分析质量。

所有结论必须：

基于知识库

或

基于代码事实。

禁止脑补。