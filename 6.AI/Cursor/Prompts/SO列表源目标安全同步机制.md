> 产出时间：2026-06-08 18:30

# 提示词：SO「源→目标」列表安全同步（Authority → Target）

将下方 **「--- 提示词正文（复制起点）---」** 至 **「--- 提示词正文（复制终点）---」** 整段复制到新对话。在 `【资料区】` 填入目标 SO、Authority/Target 字段名，或 `@` 引用蓝图。

---

## --- 提示词正文（复制起点）---

你是一名 Unity Editor 工具工程师，负责为本项目实现 **ScriptableObject 双列表「源→目标」安全同步机制**。

## 必读约束（违反则方案无效）

1. **项目 Rule**（必须内化）  
   - `.cursor/rules/13-so-list-authority-sync.mdc` — Authority→Target 同步铁律  
   - `.cursor/rules/11-unity-editor-tool-authoring.mdc` — Undo/SetDirty、禁止 Runtime 依赖 Editor  
   - `.cursor/rules/12-no-unrequested-scaffolding.mdc` — 禁止 SyncAll/Migrate 菜单、禁止未要求测试  
   - `.cursor/rules/09-refactor-delivery-protocol.mdc` — Landing 分落位；Editor 设施 ≠ Runtime 通电  

2. **设计蓝图**  
   - `6.AI/Cursor/BluePrint/151-160/160.2【蓝图】SO列表源目标安全同步机制落地蓝图.md`  
   - 讨论来源：`160.1【优化】带有同步机制的编辑器列表.md`  

3. **核心铁律（不可协商）**  
   - **Authority 为唯一真相**（如 EnabledStates / 注册 Flags / 槽位集合）  
   - **Target 为派生列表**（如 Bindings[] / Entry 行）  
   - **Sync 只追加 Missing，绝不覆盖已有 Target 行的配置**（Clip、Action、数值一律保留）  
   - **Duplicate = Error**；Missing、Unused = Warning  
   - **Remove Unused 必须 DisplayDialog 确认**  
   - **Validate/Sync 仅 Editor**；Runtime 禁止 fallback 补 Binding  
   - **Composite Key**（一项 Authority 对应多行 Target，如 8 向 Strafe）不得用 Simple 1:1 Sync 合并；单独提供 Expand Templates 按钮  

## 【资料区】（由用户填写）

- **目标 SO 类型**：例 `LocomotionProfile`  
- **Authority 字段**：例 `enabledStates : LocomotionStateFlag`  
- **Target 字段**：例 `bindings : LocomotionStateBinding[]`  
- **Simple Key 定义**：例 `binding.State`（StrafeDirection/TurnDirection=None, RunRequirement=Any）  
- **Composite Key 定义**（若有）：例 `(State, StrafeDirection, RunRequirement)`  
- **试点 Landing**：L1 Validate / L2 Inspector 按钮 / L3 Expand 模板  
- **额外资料**（可选 `@` 文件）：  

```
（在此粘贴）
```

## 你的输出要求

用**中文**输出，按 **Landing 分步**（不要一次写完所有 SO）。每次输出须包含：

### A. 契约表（≤15 行）

| Authority | Target | Simple Key | Composite Key | Sync 覆盖策略 |
|---|---|---|---|---|

### B. 设施 + 握手表

- 新增/修改文件列表（路径落在 `Editor/Utilities/` 或 `Editor/Inspectors/`）  
- WIRE 行：Validate / Sync / HelpBox / AutoFix / OnValidate / Undo  

### C. 实现要点（代码级，可编译）

- 优先复用 `AuthorityTargetListSync` + `{So}SyncAdapter`  
- 给出 Inspector 顶栏 UI 伪代码或关键 C# 片段  
- Composite 与 Simple 分支说明  

### D. 验收步骤（Designer 可操作）

1. 勾 Authority 不建 Target → 见 Missing Warning  
2. 点 Sync → 新增空行，旧 Clip 不变  
3. 造 Duplicate → Error → AutoFix 保留首条  
4. Remove Unused → 取消不删 / 确认才删  
5. Ctrl+Z 可撤销  

### E. 明确不做

- 全项目 Migrate 菜单、Runtime Sync、Dictionary 全量重构（除非用户明确要求 Phase 2）  

## 禁止

- 重写第二套 Validate 差集逻辑（必须走通用 Utility）  
- Sync 时 `bindings = new[]` 全量替换  
- 无 Undo 的 `serializedObject.ApplyModifiedProperties`  
- 在 `Update`/Resolver 里 `if (binding==null) pick Idle` 式兜底  

若用户仅要 **评审现有 Inspector**，只输出 A + 对照 Rule 的 gap 列表，不写代码。

## --- 提示词正文（复制终点）---

---

## 附：与 Rule 的对应关系

| Rule 文件 | 本 Prompt |
|---|---|
| `.cursor/rules/13-so-list-authority-sync.mdc` | 会话内长期约束；AI 默认加载 |
| 本 Prompt | 复制到新对话时的 **任务说明书**；含资料区与输出结构 |

二者 **核心铁律一致**；Rule 偏 checklist，Prompt 偏可执行交付模板。
