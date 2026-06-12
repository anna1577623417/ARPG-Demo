> 产出时间：2026-06-05 18:00

# 6.AI/Cursor/Log 目录约定

## 三层分工

| 路径 | 用途 | 谁写入 |
|------|------|--------|
| **`Log/Test/**`** | **只读**：用户粘贴 Play/Editor 原始 Log，供 AI 对照分析 | **用户**；AI **禁止**在此落盘产出 |
| **`Log/Record/Output/**`** | AI 验收对照、施工记录、操作指南、TechDoc 等**落盘产出** | **AI**（用户明确要求或 Landing 验收时） |
| **`Log/SkillRoute/`** 等历史路径 | 旧版施工 Log，逐步迁入 `Record/Output/` | 只读归档，新文件优先写 Output |

## Record/Output 子目录

按主题分子文件夹；无合适目录时**新建**（小写驼峰或 PascalCase 与现有一致）：

| 子目录 | 内容 |
|--------|------|
| `CombatFlowGraph/` | Combat Flow / 双闸门 / Graph 编辑器验收对照 |
| `SkillRoute/` | SkillRoute Landing 施工记录、通电对照 |
| `TechDoc/` | 操作指南、编辑器使用说明 |
| `Refactor/` | 重构经验、验收指南、命名规范 |
| `Version/` | 版本施工日志 |
| `Editor/` | Unity 编辑器工具总结 |
| `ActionTimeline/` | Action 时间轴相关指南 |
| `Combat/` | 战斗域通用记录 |

**命名**：`{编号}【Record】{主题}.md` 或 `【Log】`（仅施工通电记录）；指南可用 `【操作指南】`。

## 其它产出路径（勿混）

| 类型 | 路径 |
|------|------|
| **蓝图 / 资料 / 重构方案** | `6.AI/Cursor/BluePrint/{区间}/` — 当前 `141-150/`；条目满后新建 `151-160/`（`xx1`–`xx9` 编号续写） |
| **长期 Output 文档** | `6.AI/Cursor/Output/**`（与 Log/Record 并列，非 Play 原始 Log） |

## AI 落盘原则

1. **非必要不写总结类 Log**；用户贴 `Test/` 原始 Log 时，只在 `Record/Output/` 写**简短对照表**（结论 + 关键行 + 待修项）。
2. **禁止**向 `Log/Test/` 写入 AI 生成的分析/总结。
3. 新建 Markdown 首行时间戳见 `.cursor/rules/08-document-output-timestamp.mdc`。
