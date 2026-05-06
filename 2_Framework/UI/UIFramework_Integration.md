# UIFramework Integration

本文件说明当前 UI 框架在场景中的挂载顺序、输入桥接、DamageText 配置与常见故障排查。

## 1) Scene 挂载顺序

推荐在一个常驻节点（例如 `UI_RuntimeRoot`）下按顺序挂：

1. `UIRoot`
2. `UIInputReaderBridge`
3. `DamageTextSystem`
4. `DamageTextEventBridge`
5. `UIDebugOverlay`（可选）
6. `UIFrameworkHealthCheck`（推荐：引入阶段快速校验）
7. `UINavigationTrace`（可选：显示路由路径）
8. `UIThemeHealthCheck`（可选：主题链路专项校验）

`UIRoot` 需要配置：
- `hudLayer`
- `screenLayer`
- `modalLayer`
- `unblockedIdleInputMode`（默认 `Mixed`）
- `inputBlocker`（可选，推荐：挂在全屏透明 Image 上）
- `defaultTheme`（可选：开局主题）

### 1.1 UIInputBlocker 预期结构

- 节点建议挂在 `Canvas_Root` 下，层级在 `ModalLayer` 上方
- `RectTransform` 全屏拉伸：
  - `anchorMin = (0,0)`
  - `anchorMax = (1,1)`
  - `offsetMin/offsetMax = 0`
- 组件：
  - `Image`（可透明）
  - `UIInputBlocker.raycastGraphic` 绑定该 Image
- 行为：
  - 阻挡时自动 `raycastTarget=true` 且 `GameObject.SetActive(true)`
  - 不阻挡时自动隐藏

## 2) InputReader 桥接

`UIInputReaderBridge` 需要拖同一份 `InputReader` ScriptableObject（玩家控制使用的那份）。

映射规则：
- `UIOnly` -> `InputFocusMode.UI`
- `GameOnly` -> `InputFocusMode.Gameplay`
- `Mixed` -> `InputFocusMode.Mixed`（调用 `EnableGameplayAndUiMaps()`）

注意：
- `Mixed` 下请避免 UI ActionMap 与 Gameplay ActionMap 绑定完全相同的冲突热键。

## 2.1 UI Theme（注册/取消注册机制）

框架内置：
- `UIThemeSO`：主题资产
- `UIThemeService`：注册、取消注册、切换、批量应用
- `UIThemeBinder`：自动注册/取消注册（`OnEnable` / `OnDisable`）

使用方式：
1. 在 `UIRoot.defaultTheme` 指定默认主题（可空）
2. 需要主题化的 UI 节点挂 `UIThemeBinder`
3. 运行中切换：`UIRoot.Instance.SetTheme(themeAsset, forceReapply: true)`

注册机制：
- `UIThemeBinder.OnEnable` -> `UIRoot.RegisterThemeTarget(this, applyNow:true)`
- `UIThemeBinder.OnDisable` -> `UIRoot.UnregisterThemeTarget(this)`

说明：
- 对外统一通过 `UIRoot`，业务层无需直接访问 `UIThemeService`。
- 常用组件 `DamageTextView / UIHealthBar / UIItemSlot` 已支持可选 `IUIThemeable`。
- 可挂 `UIThemePreview`，运行时按 `F7/F8` 循环主题并验证全链路刷新。
- 主题变更事件：`UIRoot.ThemeChanged(oldTheme, newTheme)`。

## 3) DamageText 配置

### 3.1 CPU 路径（必备）

在 `DamageTextSystem` 上配置：
- `settings` -> `DamageTextSettingsSO`
- `cpuViewPrefab` -> 含 `DamageTextView` 的预制体
- `cpuLayerRoot` -> FX Canvas 下容器

### 3.2 GPU 路径（可选）

在 `DamageTextSettingsSO`：
- `enableGpuPath = true`
- `digitsAtlas`（0-9 图集）
- `atlasColumns/atlasRows`
- `spaceMode`（`ScreenSpace` 或 `WorldSpace`）
- `critSizeMultiplier / critRiseMultiplier / critTint`

在 `DamageTextSystem`：
- `gpuMaterial` -> 使用 `GameMain/UI/DamageTextInstanced` shader

若 GPU 条件不满足，会自动 warning 并回退 CPU：
- `gpuMaterial` 为空
- `digitsAtlas` 为空
- 设备不支持 instancing / compute

### 3.3 桥接事件

默认伤害链中 `DamageTextEmitStage` 会在最终伤害后发布 `DamageTextRequestedEvent`。
`DamageTextEventBridge` 订阅该事件并调用 `DamageTextSystem.Spawn(...)`。

## 4) WorldUI 配置

### 4.1 跟随

`WorldUIBinder.Bind(target, cam, offset)`：
- `target`：跟随对象
- `cam`：世界相机
- `offset`：头顶偏移

### 4.2 LOD

`WorldUILOD` 设置：
- `showDistance`
- `hideDistance`（应大于 showDistance，形成滞后）

### 4.3 对象池

使用 `WorldUIPool<T>` 管理血条/名字牌等高频对象：
- `Get()`
- `Release(instance)`

## 5) 虚拟列表（池化复用）

继承 `UIVirtualListBase`：
- 实现 `GetItemCount()`
- 实现 `BindRow(RectTransform row, int index)`

并在数据就绪后 `Refresh()`。

当前实现已采用 row 池复用，不再在滚动中频繁 `Destroy/Instantiate`。

## 5.1 常用组件

- `UIHealthBar`：绑定 `UIHealthBarData`（Current/Max）
- `UIItemSlot`：绑定 `UIItemSlotData`（Icon/Count/IsSelected/OnClicked）

## 6) 常见故障排查

### 6.1 打开菜单后角色仍可移动

检查：
- `UIRoot` 当前模式是否进 `UIOnly`
- `UIInputReaderBridge` 是否绑定了正确 `InputReader`
- `InputReader.CurrentFocus` 是否切换成功

### 6.2 DamageText 不显示

检查：
- `DamageTextSystem.cpuViewPrefab` 是否绑定
- `DamageTextEventBridge.damageTextSystem` 是否绑定
- 伤害是否经过 `DamagePipeline.Compute()`
- `DamageTextEmitStage` 是否在默认 stage 链中

### 6.3 GPU 打不开

检查 warning：
- 缺 `gpuMaterial` / `digitsAtlas`
- 设备不支持 instancing 或 compute

确认材质 shader：
- `GameMain/UI/DamageTextInstanced`

### 6.4 WorldSpace 飘字位置异常

检查：
- `spaceMode` 是否为 `WorldSpace`
- `DamageTextEventBridge.worldOffset` 是否过大
- 相机与 Canvas（World Space）缩放是否一致

### 6.5 列表滚动仍抖动

检查：
- `rowHeight` 与实际行高一致
- `content` / `viewport` / `scrollRect` 引用正确
- 数据变更后是否调用 `Refresh()`

### 6.6 引入前自检

可在场景里挂 `UIFrameworkHealthCheck`，点击组件菜单 `Run UI Health Check`：
- 检查 `UIRoot` 是否存在
- 提示 `UIInputReaderBridge` / `DamageTextSystem` 是否遗漏
- 检查并输出 `UIInputBlocker` 结构建议日志
- 打印当前栈摘要与 DamageText 运行摘要

### 6.7 虚拟列表快速冒烟

可用 `DemoVirtualList` + `DemoVirtualListRow` 先验证池化滚动：
- `DemoVirtualList.sampleCount` 设为 500~3000
- 快速上下拖动，确认无明显卡顿、无大量 Instantiate/Destroy 峰值
- 若数据显示异常，检查 `rowPrefab` 是否挂 `DemoVirtualListRow`

### 6.8 主题专项自检

可在场景里挂 `UIThemeHealthCheck`：
- 校验 `UIRoot.CurrentTheme` 是否为空
- 统计 `UIThemeBinder` 启用/禁用数量
- 可选在主题缺失时自动应用 `fallbackTheme`

