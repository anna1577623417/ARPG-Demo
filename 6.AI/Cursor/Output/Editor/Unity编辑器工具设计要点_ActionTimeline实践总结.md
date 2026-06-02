> 产出时间：2026-05-31 22:30

# Unity 编辑器工具设计要点（Action Timeline 实践总结）

> **适用范围**：`Assets/GameMain/Scripts/Editor/**` 下所有 `EditorWindow`、`CustomEditor`、`PropertyDrawer`、Scene 预览桥、批处理窗口。  
> **配套 Rule**：`.cursor/rules/11-unity-editor-tool-authoring.mdc`（编辑 Editor 目录时自动启用）。  
> **相关 Rule**：`10-so-inspector-authoring-foldout.mdc`（SO Inspector 折叠策略）。

---

## 一、设计目标

专业 Authoring 工具应达到：

| 维度 | 目标 |
|------|------|
| **配置即结果** | 编辑时间轴 / 曲线 / 窗口时，Scene 与 Inspector 反馈与运行时语义一致，减少 Play Mode 往返 |
| **IDE 感** | Toolbar + 可拖拽分栏 + 状态栏 + 持久化布局，而非「功能堆叠窗口」 |
| **可扩展** | 新增 Track / 标记种类时，只增轨绘制与属性块，不改核心窗口骨架 |
| **零运行时污染** | Editor 代码不进战斗帧；预览用 `AnimationMode` / `Handles`，不写 Player 真状态 |

数据流目标形态：

```text
Authoring SO（ActionData / MotionProfile / Route…）
        │
        ▼
  EditorWindow / CustomEditor
        │
        ├─► IMGUI 时间轴 / 表单（编辑）
        ├─► SceneBridge（Handles 预览）
        └─► EditorPrefs（布局记忆）
        │
        ▼
  Runtime 只读 SO，单轨执行
```

---

## 二、目录与文件组织

### 2.1 层级

| 路径 | 职责 |
|------|------|
| `Editor/Authoring/` | 策划向大工具：Timeline、批处理、迁移 |
| `Editor/Inspectors/` | `CustomEditor` / `PropertyDrawer` |
| `Editor/Gizmos/` | 可选的全局 Scene 绘制（`[InitializeOnLoad]` + `duringSceneGui`） |
| `Editor/Tools/` | 菜单入口、一次性脚本 |

**禁止**：在 `3_Gameplay` / `2_Framework` 运行时程序集里写 `#if UNITY_EDITOR` 大块 UI（除极小的 `DrawGizmo` 调试）。

### 2.2 大窗口拆分（partial class）

参考 `ActionDataTimelineEditor`：

| 文件后缀 | 职责 |
|----------|------|
| 主文件 `.cs` | 枚举、状态、时间轴绘制、输入命中、Undo |
| `.Layout.cs` | 分栏、Toolbar、StatusBar、ScrollView |
| `.WindowInspector.cs` | 选中 Window 片段的属性折叠 |
| `.Presentation.cs` | FX / Camera / TimeScale 轨与 Marker |
| `*EditorUI.cs` | 共享常量、Splitter、EditorPrefs、LayoutScope |

**原则**：单文件不超过 ~500 行；UI 布局与「轨逻辑」分离。

### 2.3 共享 UI 静态类

命名：`{Feature}EditorUI.cs`，集中：

- 布局常量（最小列宽、Padding、Splitter 宽度）
- `EditorPrefs` 键名
- `BeginVerticalScrollView`、Splitter、`PropertyLayoutScope` 等复用 API

避免每个窗口复制一套 Splitter 实现。

---

## 三、布局规范（142.1 IDE 风格）

### 3.1 推荐骨架

```text
┌─────────────────────────────────────────────────────────┐
│ Toolbar（ObjectField、保存、刷新、预览、缩放）              │
├─────────────────────────────────────────────────────────┤
│ [可选] 摘要 Foldout（Action 元数据，默认可折叠）            │
│ 预览条（归一化时间 + Pose / Scene 开关）                  │
├──────────────────────────────┬──────────────────────────┤
│ Timeline 列                   │ Property 列               │
│ （仅纵向滚动）                 │ （仅纵向滚动）             │
│ 轨道名 | gap | 刻度/片段       │ 选中项属性 + 快捷添加       │
├──────────────────────────────┴──────────────────────────┤
│ StatusBar（Action名、Clip长、t、Track、选中、Anchor）      │
└─────────────────────────────────────────────────────────┘
```

### 3.2 可拖拽分割线

- **Timeline 最小宽度**：`600px`
- **Property 最小宽度**：`320px`
- **禁止**把 Property 最大宽度写死（如 380px），应 `Clamp( desired, min, totalWidth - timelineMin - splitter )`
- 拖动逻辑：`MouseDown` 在 Splitter 上记录起点 → 全局 `MouseDrag` 更新宽度 → `MouseUp` 写入 `EditorPrefs`
- 光标：`EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal)`

### 3.3 滚动策略

| 区域 | 横向 | 纵向 |
|------|------|------|
| Timeline 内容 | **禁止** | 允许 |
| Property 内容 | **禁止** | 允许 |

实现：

```csharp
EditorGUILayout.BeginScrollView(scroll, false, true, options);
// ...
scroll.x = 0f; // 每帧锁定，防止 IMGUI 漂移出横向条
```

**反模式**：属性区同时出现横向 + 纵向滚动，视线要在 X/Y 间跳。

### 3.4 留白（避免「文字被裁切」感）

| 常量 | 建议值 | 用途 |
|------|--------|------|
| `ColumnContentPadding` | `8f` | 列内容左右、战斗按钮行、状态栏文字与窗口边缘 |
| `TimelineLabelLaneGap` | `6f` | 轨道名列与刻度条/片段区之间 |

三层留白：

1. **窗口内容区**左右各 `ColumnContentPadding`
2. **Timeline / Property 列内**再各包一层 `GUILayout.Space(pad)`
3. **轨道名与 lane** 之间 `TimelineLabelLaneGap`

### 3.5 属性栏自适应

- 使用 `PropertyLayoutScope(columnWidth)`：`labelWidth = Clamp(columnWidth * 0.4f, 72f, 168f)`
- **禁止**在属性栏大量使用 `GUILayout.Width(固定值)` 排字段
- 战斗/阶段等「核心」Foldout **默认展开**；Runtime Event、Debug **默认折叠**（见 Rule 10）

### 3.6 布局持久化（EditorPrefs）

至少持久化：

- Property 列宽
- 时间轴缩放（Zoom）
- 各 ScrollView 的 Y（Timeline 可锁 X=0）

在 `OnEnable` 加载、`OnDisable` 保存；Splitter `MouseUp` 时可增量保存列宽。

键名约定：`"{ToolName}.PropertyWidth"`，避免全局冲突。

---

## 四、Scene 预览（141.1）

### 4.1 三类 API 分工

| API | 允许调用位置 | 用途 |
|-----|----------------|------|
| `Gizmos.*` | **仅** `OnDrawGizmos` / `OnDrawGizmosSelected` | 挂在 `MonoBehaviour` 上的调试 |
| `Handles.*` | `OnSceneGUI` / `SceneView.duringSceneGui` | 编辑器预览、可点击、带 Label |
| `EditorGUI` / `Handles.BeginGUI` | 同上 | 少量 HUD 提示 |

**致命错误**：在 `EditorWindow.OnSceneGUI` 或 `duringSceneGui` 里调用 `Gizmos.DrawSphere` → `ArgumentException`，并可能连带 `GUIClip` 不平衡。

### 4.2 SceneBridge 模式

```text
EditorWindow.OnSceneGUI
    → Build PreviewContext（Anchor、归一化时间、StateTag）
    → PreviewController.SamplePose（AnimationMode）
    → SceneBridge.DrawSceneGUI（仅 Handles）
```

- **PreviewContext**：只读 struct，由静态 `Build(action, time, anchorOverride)` 构造；**避免 C# 9 `init`**（旧 Unity 无 `IsExternalInit`）→ 用构造函数 + get-only 属性。
- **PreviewController**：`AnimationMode.StartAnimationMode` → `BeginSampling` → `SampleAnimationClip(gameObject, clip, seconds)` → `EndSampling`；窗口 `OnDisable` 必须 `StopAnimationMode`。
- **Anchor 解析顺序**：显式 Override → `Selection.activeTransform` → 场景 `Player`。

`SampleAnimationClip` 参数顺序：**`(GameObject, AnimationClip, float)`**（勿颠倒）。

### 4.3 多窗口互斥

多个 Timeline 窗口时，仅 **当前聚焦** 的实例驱动 Scene 预览（`OnFocus` 更新 `s_activeEditor`），避免双份采样打架。

---

## 五、时间轴编辑器专项

### 5.1 轨扩展 checklist

新增 Track 时：

1. `enum TrackId` + `ActiveTracks[]` 顺序
2. `DrawTrackRow` 分支：片段 / Marker / 只读 hint
3. `GetTrackLabel`、双击创建、`TrackToDefaultMarkerKind`
4. `SceneBridge` 可选绘制（Handles）
5. Property 面板：选中态 Inspector 或 Foldout

**禁止**在 `DrawTimeline` 单方法里堆 500 行 switch；按轨类型拆方法或 partial。

### 5.2 选中与编辑

- 数据修改走 `SerializedObject` + `Undo.RecordObject`
- 拖拽改 Window 起止：记录 `_dragOrigStart/End`，`MouseUp` 时一次性提交
- 预览时间 `_previewTime` 与 Scene、Playhead 黄线同步

### 5.3 CompactPropertyContext

属性栏绘制前设 `ActionTimelineEditorUI.CompactPropertyContext = true`，在 `PropertyDrawer` 内：

- 省略冗长 `HelpBox`
- 缩短 Mask 类控件高度

`finally` 中必须还原为 `false`。

---

## 六、CustomEditor / PropertyDrawer

与 Rule **10** 配合：

| 字段性质 | Inspector 表现 |
|----------|----------------|
| Runtime 必填 | 常驻 |
| 迁移 / 批处理 / Scene 调试 | **FoldoutHeaderGroup，默认关** |
| 与 Timeline 重复的原始 List | 在专用 Editor 隐藏，引导打开 Timeline 窗口 |

Tooltip：**禁止**内嵌未转义英文双引号；用 **【】** 或「」。

---

## 七、工程与 C# 约束

| 项 | 要求 |
|----|------|
| 程序集 | Editor 脚本在 `Editor` asmdef，仅引用 Runtime/Data |
| C# 版本 | 勿用 `record`/`init`/file-scoped namespace，除非项目统一升级 |
| 分配 | 编辑器 UI 可接受少量 GC；热路径（每帧 SceneGUI）避免 LINQ、`new` 字符串拼接 |
| 菜单 | `Tools/GameMain/...` 与现有工具一致 |

---

## 八、反模式清单（本项目已踩坑）

| 反模式 | 后果 |
|--------|------|
| OnSceneGUI 里用 `Gizmos` | 报错 + GUIClip 异常 |
| Property 列 `MaxWidth = 380` | 无法拉宽，长字段挤爆 |
| 无 `TimelineLabelLaneGap` | 轨道名贴滚动条，像被裁切 |
| `AnimationMode` 不 Stop | 退出窗口后场景_pose 卡住 |
| 预览写 Animator.Play | 污染场景状态、Undo 脏 |
| 属性栏横向 ScrollView | 双轴滚动，配置效率差 |
| 每个 Track 改核心 `OnGUI` 不拆文件 | God Window，无法维护 |

---

## 九、验收清单（新工具 / 大改）

### 布局

- [ ] 左右分栏可拖，尊重最小宽度
- [ ] 关闭 Unity 再开，列宽/缩放/滚动恢复
- [ ] 属性区无横向滚动条
- [ ] 列内容与按钮行左右有 `ColumnContentPadding`
- [ ] Timeline 轨道名与 lane 有间隙

### Scene

- [ ] 拖预览条，Pose 与 Handles 同步
- [ ] Console 无 Gizmo/Handles 相关异常
- [ ] 关窗口后 `AnimationMode` 结束

### 数据

- [ ] 修改可 Undo
- [ ] 保存后 SO 脏标记正确
- [ ] 运行时程序集无 Editor 引用

---

## 十、参考实现索引

| 能力 | 文件 |
|------|------|
| 双栏 + Splitter + StatusBar | `Editor/Authoring/ActionDataTimelineEditor.Layout.cs` |
| 共享 UI / EditorPrefs | `Editor/Authoring/ActionTimelineEditorUI.cs` |
| Scene Handles 预览 | `Editor/Authoring/ActionDataTimelineSceneBridge.cs` |
| Pose 采样 | `Editor/Authoring/ActionTimelinePreviewController.cs` |
| 预览上下文 | `Editor/Authoring/ActionTimelinePreviewContext.cs` |
| 全局轨迹 Handles | `Editor/Gizmos/MotionPathGizmoDrawer.cs` |
| SO 折叠策略 | `.cursor/rules/10-so-inspector-authoring-foldout.mdc` |

---

## 十一、面试口述（一句话）

> Action Timeline 采用 IDE 式双栏与可持久化 Splitter，Scene 预览统一走 Handles + AnimationMode 采样，与运行时 ActionData 单轨对齐，避免 Gizmos 误用和横向滚动，让策划在编辑器里完成「配置即结果」的闭环。
