# AI_KnowledgeBase — 项目知识库索引

> **生成时间**: 2026-06-08  
> **代码基线**: Ver 4.6+ (~386 C# 脚本, ~28,000 行)  
> **引擎**: Unity 2022 LTS · URP

---

## 快速导航

### 新手入门 (推荐阅读顺序)
1. **[00_ProjectMap](./00_ProjectMap.md)** — 项目目录结构 + 五层螺旋架构
2. **[01_FrameworkOverview](./01_FrameworkOverview.md)** — 28个系统全景矩阵

### 深入理解
3. **[02_SystemInteraction](./02_SystemInteraction.md)** — 系统间调用链/事件流/数据流
4. **[03_RuntimePipeline](./03_RuntimePipeline.md)** — 按键→动作结束完整时序

### 特定领域
5. **[04_EditorPipeline](./04_EditorPipeline.md)** — 编辑器工具链
6. **[05_ClassIndex](./05_ClassIndex.md)** — 全量类索引 (按层级+重要度)
7. **[06_ActionCatalog](./06_ActionCatalog.md)** — Action 体系 (数据模型)
8. **[07_MotionCatalog](./07_MotionCatalog.md)** — MotionProfile 体系
9. **[08_StateMachineCatalog](./08_StateMachineCatalog.md)** — 四支柱状态机
10. **[09_TechDebt](./09_TechDebt.md)** — 架构债务 + 11个关键问题分析

---

## 系统速查

| 想知道... | 看哪个文档 | 章节 |
|----------|-----------|------|
| 这个文件放在哪层？ | `00_ProjectMap` | 五层螺旋架构 |
| 这个类是做什么的？ | `05_ClassIndex` | 按层搜索 |
| 按键到动作的完整链路？ | `03_RuntimePipeline` | 完整时序图 |
| 谁能打断当前动作？ | `02_SystemInteraction` | 交互图谱3 |
| 位移是谁控制的？ | `02_SystemInteraction` | 交互图谱4 |
| MotionProfile怎么配置？ | `07_MotionCatalog` | 位移类型矩阵 |
| CombatGraph怎么编辑？ | `04_EditorPipeline` | CombatFlowGraphWindow |
| 有什么架构问题？ | `09_TechDebt` | 全部章节 |

---

## 项目关键数字

| 指标 | 值 |
|------|-----|
| C# 脚本数 | 386 |
| 代码行数 | ~28,000 |
| 架构层级 | 5 (1_Core → 2_Framework → 3_Gameplay → 4_Data → 5_Presentation) |
| 支柱状态 | 4 (Locomotion / Airborne / Action / Dead) |
| 标签轨道 | 5 (State / Status / Ability / Mechanic / Faction) |
| 输入槽位 | 17 (LM/RM/Q/Shift/Space/R/Key0-9 + Primary) |
| 路由类型 | 5 (Normal / Combo / Charge / MultiStage / Derivative) |
| 伤害阶段 | 5 (BaseDamage → DefenseReduction → Crit → FinalClamp → DamageTextEmit) |
| 编辑器窗口 | 4 个主要工具 (Timeline / CombatGraph / MotionCurve / Batch) |
| 时间轴轨道 | 15 |

---

## 更新记录

| 日期 | 更新内容 |
|------|---------|
| 2026-06-08 | 初始生成：全 10 篇知识库文档 |
