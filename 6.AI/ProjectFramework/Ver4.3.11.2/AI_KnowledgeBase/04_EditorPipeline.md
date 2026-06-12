# 04_EditorPipeline — 编辑器体系分析

> **生成时间**: 2026-06-08  
> **分析依据**: Editor/ 目录 50+ 脚本全量扫描

---

## 编辑器工具全景

```
Editor/
├── Authoring/                           ★ 资产创作工具链
│   ├── ActionDataInspector.cs           ActionDataSO 自定义 Inspector
│   ├── ActionDataInspector.TimeAuthority.cs  时长匹配计算
│   ├── ActionDataTimelineEditor.cs         ★ 动作时间轴编辑器 (15轨)
│   ├── ActionDataTimelineEditor.Layout.cs  编辑器布局
│   ├── ActionDataTimelineEditor.Presentation.cs  表现层绘制
│   ├── ActionDataTimelineEditor.WindowInspector.cs  窗口属性面板
│   ├── ActionDataTimelineSceneBridge.cs   场景预览桥接
│   ├── ActionSkillStageBatchWindow.cs     Action→SkillStage 批量窗口
│   ├── ActionSkillStageNormalRouteBatchWindow.cs  NormalRoute 批量窗口
│   ├── ActionTimelineEditorUI.cs          时间轴编辑器 UI 绘制
│   ├── ActionTimelinePreviewContext.cs    预览上下文
│   ├── ActionTimelinePreviewController.cs 预览控制器 (Scene View)
│   ├── BatchOutputPathUtil.cs            批处理路径工具
│   ├── ClipActionMotionBatchWindow.cs     Clip→Action+Motion 批量
│   ├── ClipMotionExtractor.cs             Clip 位移提取
│   ├── ClipMotionRootTSampling.cs         Root 采样
│   ├── CombatFlowChainPreviewDrawer.cs    CombatFlow 链预览
│   ├── CombatFlowEdgeKindRules.cs         边类型规则
│   ├── CombatFlowGraphCompiler.cs         图编译器
│   ├── CombatFlowGraphValidator.cs        图校验器
│   ├── MotionAxisCurveEditorWindow.cs     ★ 三轴曲线编辑窗口
│   ├── MotionCurveGenerator.cs           曲线生成器 (CatmullRom/滑动平均)
│   ├── MotionCurveSegmentPreset.cs        曲线预设
│   ├── MotionCurveSegmentPresetGUI.cs     预设 UI
│   ├── MotionCurveSegmentPresetUtility.cs 预设工具
│   ├── MotionExtractSource.cs            提取源枚举
│   ├── MotionMigrationTool.cs            迁移工具 (旧→新三轴)
│   ├── MotionProfileEditor.cs            ★ MotionProfile 自定义 Inspector
│   ├── MotionProfileEditor.ManualCurves.cs 手动曲线编辑
│   ├── MotionXYZAuthoring.cs             XYZ 创作工具
│   ├── RefSpeedExtractor.cs              参考速度提取
│   ├── SkillGroupFourDirEditorUtil.cs    四方向编辑器工具
│   └── SkillStageNormalRouteBatchWindow.cs  NormalRoute Stage 批量
│
├── CombatFlow/                          ★ CombatFlow 图编辑器
│   ├── CombatFlowConditionDragDrop.cs   条件节点拖放
│   ├── CombatFlowEdgeConditionsDrawer.cs 边条件绘制器
│   ├── CombatFlowGraphEdgeInspector.cs   边 Inspector
│   ├── CombatFlowGraphEdgeSelectionUtility.cs  边选择工具
│   ├── CombatFlowGraphInputDebug.cs      输入调试
│   ├── CombatFlowGraphInspectorFeedback.cs  Inspector 反馈
│   ├── CombatFlowGraphInspectorLayout.cs   Inspector 布局
│   ├── CombatFlowGraphNodeInspector.cs   节点 Inspector
│   ├── CombatFlowGraphNodeSearch.cs      节点搜索
│   ├── CombatFlowGraphSelectionClassifier.cs  选择分类器
│   ├── CombatFlowGraphSelectionController.cs  选择控制器
│   ├── CombatFlowGraphSelectionKinds.cs  选择类型
│   ├── CombatFlowGraphSync.cs            同步工具
│   ├── CombatFlowGraphView.cs            图视图
│   └── CombatFlowGraphWindow.cs          ★ 图编辑器窗口 (基于 GraphProcessor)
│
├── Gizmos/
│   └── MotionPathGizmoDrawer.cs          运动路径 Gizmo 绘制
│
├── Inspectors/                          自定义 Inspector
│   ├── ActionCategoryPropertyDrawer.cs   ActionCategory 绘制器
│   ├── ActionWindowDrawer.cs             ActionWindow 绘制器
│   ├── ChargeRouteDefinitionEditor.cs    ChargeRoute 编辑器
│   ├── CombatFlowConditionDefinitionEditor.cs  条件定义编辑器
│   ├── CombatGraphAssetEditor.cs         CombatGraph 资产编辑器
│   ├── ComboRouteDefinitionEditor.cs     ComboRoute 编辑器
│   ├── EntityStatsSOEditor.cs            属性 SO 编辑器
│   ├── NormalRouteDefinitionEditor.cs    NormalRoute 编辑器
│   ├── PlayerStateManagerEditor.cs       StateManager 编辑器
│   ├── RouteGraphTypeInspectorDrawer.cs  RouteGraph 类型绘制
│   ├── SkillContextGroupDefinitionEditor.cs  上下文组编辑器
│   ├── SkillEntryLoadoutEditor.cs        Loadout 编辑器
│   ├── SkillRouteDefinitionEditor.cs     Route 定义编辑器
│   ├── SkillRouteGroupMembershipDrawer.cs Route 组归属绘制
│   └── StateTagMaskPropertyDrawers.cs    StateTag 掩码绘制器
│
├── KCCMeshCombineWindow.cs              KCC 网格合并窗口
│
└── PropertyDrawers/
    └── MotionAxisCurvesDrawer.cs         三轴曲线绘制器
```

---

## 核心编辑器工具详解

### 1. ActionDataTimelineEditor — 动作时间轴编辑器

**定位**: `Editor/Authoring/ActionDataTimelineEditor.cs` (1000+ 行，partial 类)

**功能**: ActionDataSO 的 15 轨时间轴

```
TrackId 轨道:
  Interrupt    — 打断窗口 (可打断类别/优先级)
  PhaseStartup — 起手阶段
  PhaseActive  — 活跃阶段
  PhaseRecovery — 后摇阶段
  Hitbox       — 攻击判定窗口
  Hurtbox      — 受击判定窗口
  Invincible   — 无敌窗口
  ComboInput   — 连招输入窗口
  RootMotion   — RootMotion 窗口
  RuntimeEvent — 运行时事件
  Teleport     — 瞬移触发点
  Fx           — 特效标记
  Audio        — 音效标记
  Camera       — 相机效果标记
  TimeScale    — 时停/慢动作标记
```

**数据流**:
```
ActionDataSO (选中资产)
  → SerializedObject → SerializedProperty
  → Windows[], TeleportTriggers[], TimelineMarkers[]
  → 编辑区可视化 (矩形条/标记)
  → 修改 → serializedObject.ApplyModifiedProperties
  → AssetDatabase.SaveAssets
```

**预览功能**:
```
ActionTimelinePreviewContext (预览上下文)
  → 场景中选中角色
  → ActionTimelinePreviewController
  → ActionTimelineSceneBridge (场景桥接)
  → 即时播放预览 (不进Play Mode)
```

### 2. CombatFlowGraphWindow — 技能衔接图编辑器

**定位**: `Editor/CombatFlow/CombatFlowGraphWindow.cs` (700+ 行)

**功能**: 基于 GraphProcessor 的可视化节点图编辑器

```
CombatGraphAsset (SO)
  ↓ 双击打开
CombatFlowGraphWindow (继承 BaseGraphWindow)
  ├── CombatFlowGraphView (BaseGraphView)
  │     ├── CombatFlowGraphNode[] (节点)
  │     │     ├── Start Node (入口)
  │     │     ├── Combat Node (动作节点, 引用 SkillRouteDefinition)
  │     │     ├── Idle Node (待机)
  │     │     └── End Node (结束)
  │     └── CombatFlowGraphEdge[] (边)
  │           └── CombatFlowConditionDefinition
  │                 ├── MoveDirection8 条件
  │                 ├── IsAirborne 条件
  │                 ├── HitTally 条件
  │                 ├── Resource 条件
  │                 ├── Cooldown 条件
  │                 └── Tag 条件
  │
  ├── CombatFlowGraphSelectionController (选择管理)
  ├── CombatFlowGraphNodeInspector (节点属性面板)
  ├── CombatFlowGraphEdgeInspector (边条件面板)
  ├── CombatFlowGraphNodeSearch (节点搜索)
  ├── CombatFlowConditionDragDrop (拖放条件)
  └── CombatFlowGraphInspectorLayout (Inspector 布局)
```

**编译流程**:
```
CombatGraphAsset (图数据)
  → CombatFlowGraphValidator.Validate (校验)
    ├── 检查节点引用完整性
    ├── 检查边条件有效性
    └── 输出 ValidationReport
  → CombatFlowGraphCompiler.TryCompile
    ├── 节点 → CombatFlowCompiledNode[]
    ├── 边 → 条件评估器
    └── 产出 CombatFlowData (运行时数据)
       → EditorSetCompileResult → AssetDatabase.SaveAssets
```

### 3. MotionProfileEditor — 运动曲线编辑器

**定位**: `Editor/Authoring/MotionProfileEditor.cs` (partial 类, 400+ 行)

**功能**: MotionProfileSO 的自定义 Inspector

**核心功能**:
- **Clip 提取**: 从 AnimationClip 提取位移曲线
  - `ClipMotionExtractor` — 根骨骼采样 → 位移数据
  - `MotionCurveGenerator` — 数据拟合 → AnimationCurve (CatmullRom / MovingAverage)
  - `MotionCurveFitPipeline` — 完整提取管线
- **手动编辑**: 三轴曲线直接编辑
- **三轴独立**: X/Y/Z 各自 SourceClip + ExtractSource (Auto/Manual)
- **预设系统**: `CurvePresetType` (Linear / EaseIn / EaseOut / EaseInOut ...)
- **默认生成**: ApplyDefaultForwardAxis(distanceMeters)
- **迁移工具**: `MotionMigrationTool` (旧爆发段 → 新三轴曲线)
- **参考速度**: `RefSpeedExtractor`

**MotionAxisCurveEditorWindow**: 独立三轴曲线编辑窗口
- 三轴曲线可视化
- 拖动控制点
- 缩放/平移
- 实时预览

### 4. 批处理工具

| 工具 | 文件 | 功能 |
|------|------|------|
| Clip→Action+Motion 批量 | `ClipActionMotionBatchWindow.cs` | 批量从 Clip 生成 ActionDataSO + MotionProfileSO |
| Action→SkillStage 批量 | `ActionSkillStageBatchWindow.cs` | 批量创建 SkillStageDefinition 并绑定 Action |
| Action→NormalRoute 批量 | `ActionSkillStageNormalRouteBatchWindow.cs` | 批量创建 NormalRouteDefinition |
| NormalRoute Stage 批量 | `SkillStageNormalRouteBatchWindow.cs` | 批量创建 Stage |
| Motion 迁移 | `MotionMigrationTool.cs` | 旧数据→新三轴曲线迁移 |
| Motion 默认生成 | `MotionProfileEditor.cs` | 一键生成默认 Z 轴曲线 |

---

## SO 资产生命周期

### 创建流程 (Authoring)

```
Unity Editor
  │
  ├── 手动创建: Project 右键 → Create → GameMain/...
  │     MotionProfileSO, ActionDataSO, SkillRouteDefinition, SkillEntryLoadoutSO, ...
  │
  ├── 编辑器创建:
  │     ActionDataTimelineEditor → 创建/修改 Window/Marker/Teleport
  │     CombatFlowGraphWindow → 创建/连接 Node/Edge
  │     MotionAxisCurveEditorWindow → 编辑三轴曲线
  │
  └── 批处理创建:
        ClipActionMotionBatchWindow → 从 Clip 创建 Action+Motion
        ActionSkillStageBatchWindow → 从 Action 创建 Stage+Route
```

### 编辑流程

```
选择 SO 资产 (Project 面板)
  ↓
Inspector 面板
  ├── 默认 Inspector (Unity 原生)
  ├── 自定义 Inspector (CustomEditor):
  │     MotionProfileEditor → MotionProfileSO
  │     NormalRouteDefinitionEditor → NormalRouteDefinition
  │     ComboRouteDefinitionEditor → ComboRouteDefinition
  │     ChargeRouteDefinitionEditor → ChargeRouteDefinition
  │     CombatGraphAssetEditor → CombatGraphAsset
  │     SkillRouteDefinitionEditor → SkillRouteDefinition
  │     SkillEntryLoadoutEditor → SkillEntryLoadoutSO
  │     EntityStatsSOEditor → EntityStatsSO
  │     PlayerStateManagerEditor → PlayerStateManager
  │     ActionDataInspector → ActionDataSO (含 TimeAuthority)
  │
  └── 专属编辑器窗口 (双击打开):
        ActionDataTimelineEditor → ActionDataSO
        CombatFlowGraphWindow → CombatGraphAsset
        MotionAxisCurveEditorWindow → MotionProfileSO
```

### 保存流程

```
所有编辑器修改 → Unity Serialization 系统
  ├── Undo.RecordObject (支持撤销)
  ├── EditorUtility.SetDirty (标记脏)
  ├── serializedObject.ApplyModifiedProperties (提交)
  └── AssetDatabase.SaveAssets / SaveAssetIfDirty (写盘)
```

### 运行时加载流程

```
GameBootstrapper / PlayerFactory 启动
  ↓
SkillEntryLoadoutSO (Inspector 引用 → 直接序列化引用)
  ├── SkillEntryDefinition[] (直接引用)
  │     └── SkillRouteDefinition[] → SkillStageDefinition[] → ActionDataSO (直接引用)
  ├── CombatGraphAsset (直接引用)
  │     └── CombatFlowGraphNode[] → SkillRouteDefinition (引用回)
  ├── AbilityMapSO (直接引用)
  └── ContextGroups[] (直接引用)
  ↓
SkillEntryService.Rebuild(loadout):
  遍历所有引用 → 创建 RouteRuntime 实例
  AttachGraph → CombatGraphRunner
  所有数据就地可用 (Unity 引用，无需异步加载)
```

---

## 编辑器与运行时的桥接

### ActionTimelinePreviewController (Editor-only)

```
ActionTimelinePreviewContext
  ├── 持有目标角色引用
  ├── 持有 ActionDataSO 引用
  ├── 模拟 PlayerActionState.OnEnter 的部分逻辑
  ├── 驱动 MotionExecutor (预览位移曲线)
  └── 驱动 Animator (预览动画)
```

### ActionTimelineSceneBridge (Editor-only)

```
场景视图预览:
  EditorApplication.update 回调
    → 逐帧推进预览
    → 绘制 Hitbox/Wireframe
    → 绘制 Motion 路径 (MotionPathGizmoDrawer)
```

### MotionPathGizmoDrawer

```
场景视图:
  OnDrawGizmos
    → 读取 MotionProfileSO.AxisCurves
    → 采样曲线点
    → 绘制位移路径 (世界空间)
```

---

## 编辑器工具依赖链

```
ClipMotionExtractor
  ├── AnimationClip (FBX)
  ├── MotionCurveGenerator (CatmullRom/移动平均拟合)
  └── → MotionProfileSO.AxisCurves

MotionProfileSO
  ├── MotionAxisCurveEditorWindow (手调)
  ├── MotionProfileEditor (Inspector)
  └── → ActionDataSO.MotionProfile

ActionDataSO
  ├── ActionDataTimelineEditor (时间轴编辑)
  └── → SkillStageDefinition.Action

SkillStageDefinition
  └── → SkillRouteDefinition.Stages[]

SkillRouteDefinition
  ├── SkillRouteDefinitionEditor (Inspector)
  └── → SkillEntryDefinition.NormalRoute / PrimaryGroup

SkillEntryDefinition
  ├── SkillEntryLoadoutEditor (Inspector)
  └── → SkillEntryLoadoutSO.Bindings[]

SkillEntryLoadoutSO
  ├── 包含 CombatGraphAsset
  └── → Player.SkillEntryLoadout (Inspector 引用)

CombatGraphAsset
  ├── CombatFlowGraphWindow (图编辑)
  ├── CombatFlowGraphCompiler (编译)
  ├── CombatFlowGraphValidator (校验)
  └── → CombatGraphRunner (运行时)
```
