# 05_ClassIndex — 类索引

> **生成时间**: 2026-06-08  
> **索引范围**: 386 C# 脚本, 按层级分组  
> **重要程度**: 🔴 High — 核心类, 修改影响全局 | 🟡 Medium — 重要但有边界 | 🟢 Low — 工具/配置/测试

---

## 1_Core (核心层)

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `GlobalEventBus` | `1_Core/EventBus/Core/` | 全局事件总线, 发布/订阅 | `Publish<T>`, `Subscribe<T>`, `Unsubscribe<T>` | 无 | 全项目 | 🔴 |
| `LocalEventBus` | `1_Core/EventBus/Core/` | 实体级事件总线 | `Publish<T>`, `Subscribe<T>` | 无 | Entity基类 | 🔴 |
| `IGameEvent` | `1_Core/EventBus/Core/` | 事件标记接口 | — | 无 | 所有事件类型 | 🔴 |
| `EventSubscriptionBinder` | `1_Core/EventBus/Lifecycle/` | 批量订阅/取消, 绑定到GameObject生命周期 | `Bind`, `UnbindAll` | GlobalEventBus | MonoBehaviour | 🟡 |
| `EventSubscriptionScope` | `1_Core/EventBus/Lifecycle/` | IDisposable 订阅作用域 | `Dispose` | GlobalEventBus | using块 | 🟢 |
| `GlobalEventSubscription` | `1_Core/EventBus/Lifecycle/` | 全局订阅句柄 | `Dispose` | GlobalEventBus | Binder | 🟢 |
| `LocalEventSubscription` | `1_Core/EventBus/Lifecycle/` | 本地订阅句柄 | `Dispose` | LocalEventBus | Binder | 🟢 |
| `Singleton<T>` | `1_Core/Utilities/` | 泛型单例 (非MonoBehaviour) | `Instance` | 无 | ServiceRegistry等 | 🟡 |
| `MonoSingleton<T>` | `1_Core/Utilities/` | 泛型MonoBehaviour单例 | `Instance` | 无 | CameraController等 | 🟡 |

---

## 2_Framework (框架层)

### Bootstrapping

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `GameBootstrapper` | `2_Framework/Bootstrapping/` | 场景启动入口, 注册系统 | `Awake`, 注册ServiceRegistry | SystemRoot | Scene | 🔴 |
| `PlayerFactory` | `2_Framework/Bootstrapping/` | 运行时生成/注入 Player | `Instantiate`, `InjectMovementContext` | SystemRoot | Bootstrapper | 🟡 |
| `SystemRoot` | `2_Framework/Bootstrapping/` | 系统根节点, 场景登记 | 持有Camera/MovementContext | 无 | GameBootstrapper | 🔴 |

### Camera

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `CameraController` | `2_Framework/Camera/Core/` | 相机控制器基类 | `UpdateCamera`, 相机参数 | 无 | ActionCamera等 | 🔴 |
| `ActionCameraController` | `2_Framework/Camera/Controllers/` | 动作相机 (战斗特化) | 镜头震动, FOV变化 | CameraController | GameModeManager | 🟡 |
| `FPSCameraController` | `2_Framework/Camera/Controllers/` | 第一人称相机 | 鼠标视角控制 | CameraController | GameModeManager | 🟢 |
| `MOBACameraController` | `2_Framework/Camera/Controllers/` | MOBA 视角相机 | 俯视跟随 | CameraController | GameModeManager | 🟢 |
| `GameModeManager` | `2_Framework/Camera/Core/` | 游戏模式管理, 相机切换 | `ActiveCameraController`, 模式切换 | CameraController | 全局 | 🔴 |
| `CameraDeadzoneProxy` | `2_Framework/Camera/Core/` | 相机死区代理 | 死区计算 | 无 | CameraController | 🟢 |
| `SwitchBlendCameraState` | `2_Framework/Camera/StateDriven/` | 状态驱动相机混合 | 状态切换 | CameraController | StateMachine | 🟢 |
| `CameraEvents` | `2_Framework/Camera/Events/` | 相机事件定义 | 事件struct | 无 | EventBus | 🟢 |
| `CameraCollisionRayVisualizer` | `2_Framework/Camera/Debug/` | 相机碰撞射线可视化 | Gizmos绘制 | 无 | Debug | 🟢 |

### Combat Framework

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `IEntity` | `2_Framework/Combat/Interfaces/` | 实体接口 | `Transform`, `Stats`, `Resources`, `Tags` | 无 | Player, Entity, TestDummy | 🔴 |
| `IDamageable` | `2_Framework/Combat/Interfaces/` | 可受伤接口 | `TakeDamage(DamageInfo)` | IEntity | Player, Entity | 🔴 |
| `IEffectReceiver` | `2_Framework/Combat/Interfaces/` | 效果接收接口 | `ReceiveDamage`, `BuffStack`, `Stats` | IEntity | Player | 🔴 |
| `ITagOwner` | `2_Framework/Combat/Interfaces/` | 标签持有者接口 | `Tags` | 无 | Player, Entity | 🔴 |
| `CombatContextSnapshot` | `2_Framework/Combat/Context/` | 战斗上下文快照 (struct) | `IsAirborne`, `MoveDirection`, `HitConfirmedThisStage` | 无 | SkillEntryService | 🟡 |
| `MoveDirection8` | `2_Framework/Combat/Context/` | 8方向枚举 + 扩展方法 | `FromDirectional` | 无 | CombatGraph条件 | 🟡 |
| `EffectSystem` | `2_Framework/Combat/` | 效果系统 (规划中) | [待验证] | IEffectReceiver | [待验证] | 🟡 |
| `ProjectileController` | `2_Framework/Combat/Projectile/` | 弹道控制器 (SphereCast CCD) | `Launch`, `Tick` | IDamageable | 技能系统 | 🟡 |

### DI

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `ServiceRegistry` | `2_Framework/DI/` | 服务注册/解析 | `Register<T>`, `Resolve<T>` | 无 | Bootstrapper | 🟡 |
| `IServiceResolver` | `2_Framework/DI/` | 服务解析接口 | `Resolve<T>` | 无 | ServiceRegistry | 🟡 |

### FSM

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `StateMachine<T>` | `2_Framework/FSM/` | 泛型状态机 | `Change<TState>`, `ForceChange<TState>`, `LogicUpdate` | State<T> | EntityStateManager | 🔴 |
| `State<T>` | `2_Framework/FSM/` | 泛型状态基类 | `OnEnter`, `OnExit`, `OnLogicUpdate`, `TryConsumeGameplayIntent` | 无 | 所有状态 | 🔴 |

### GameplayTags

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `GameplayTagContainer` | `2_Framework/GameplayTags/` | 5轨标签容器 (struct) | `State`, `Status`, `Ability`, `Mechanic`, `Faction`, `Add`, `Remove`, `Clear` | 各Tag枚举 | Player, Entity | 🔴 |
| `GameplayTagMask` | `2_Framework/GameplayTags/` | 标签掩码 | `Value` (ulong), `Add`, `Remove`, `HasAll` | 无 | TagContainer, 所有系统 | 🔴 |
| `StateTag` | `2_Framework/GameplayTags/` | State轨标签枚举 | `Grounded`, `Airborne`, `Dead`, `PhaseStartup/Active/Recovery` | 无 | GameplayTagContainer | 🔴 |
| `StatusTag` | `2_Framework/GameplayTags/` | Status轨标签枚举 | 状态异常标记 | 无 | GameplayTagContainer | 🟡 |
| `EntityCapabilityTag` | `2_Framework/GameplayTags/` | Ability轨标签枚举 | `CanAttack`, `CanJump`, `CanDodge`, `IsSilenced` | 无 | EntityAbilitySystem | 🔴 |
| `MechanicTag` | `2_Framework/GameplayTags/` | Mechanic轨标签枚举 | 机制标记 | 无 | GameplayTagContainer | 🟡 |
| `FactionTag` | `2_Framework/GameplayTags/` | Faction轨标签枚举 | 阵营标记 | 无 | GameplayTagContainer | 🟢 |
| `SkillPayloadTag` | `2_Framework/GameplayTags/` | 技能负载标签 | — | 无 | SkillRoute | 🟡 |
| `CombatEventTag` | `2_Framework/GameplayTags/` | 战斗事件标签 | — | 无 | 事件系统 | 🟢 |
| `TagCategory` | `2_Framework/GameplayTags/` | 标签轨道枚举 | `State`, `Status`, `Ability`, `Mechanic`, `Faction` | 无 | TagContainer | 🔴 |

### Input

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `InputReader` | `2_Framework/Input/` | ★ 输入读取器 (SO), 17槽SoA表 | `MoveInput`, `IsAttackHeld`, `ConsumeSkillEntryPressed`, SoA表 | PlayerInputSystem | PlayerController, PlayerStateManager | 🔴 |
| `InputSemanticResolver` | `2_Framework/Input/` | ★ 输入语义解析器, 每槽位状态机 | `OnPressEdge`, `OnReleaseEdge`, `OnDiscretePulse`, `OnHoldTick` | PerSlotConfig | PlayerController | 🔴 |
| `InputModifierBuffer` | `2_Framework/Input/` | WASD 延迟缓冲 | `Record`, `GetBufferedMove` | 无 | InputReader | 🟡 |
| `PlayerInputSystem` | `2_Framework/Input/` | InputAction 资产包装 (生成代码) | IGamePlayActions回调 | InputActionAsset | InputReader | 🔴 |
| `InputSemanticType` | `2_Framework/Input/` | 语义类型枚举 | `None`, `Tap`, `Combo`, `Charge`, `Release`, `Directional` | 无 | GameplayIntent, Resolver | 🔴 |
| `InputEvents` | `2_Framework/Input/` | 输入事件定义 | — | 无 | EventBus | 🟢 |
| `RebindManager` | `2_Framework/Input/` | 按键重绑定管理 | `Rebind` | PlayerInputSystem | UI设置 | 🟢 |

### Motion Framework

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `MotionComposer` | `2_Framework/Motion/Runtime/` | Motion+重力融合器 | `ComposeWorldVelocity` | MotionContribution, GravityContribution | PlayerKCCMotor | 🔴 |
| `MotionContribution` | `2_Framework/Motion/Runtime/` | Motion贡献数据 (struct) | `LocalDelta`, `YAxisConfig`, `IsActive` | MotionYAxisConfig | MotionExecutor | 🔴 |
| `GravityContribution` | `2_Framework/Motion/Runtime/` | 重力贡献数据 (struct) | `Vy`, `IsActive` | 无 | MotionComposer | 🟡 |
| `MotionGroundConstraint` | `2_Framework/Motion/Runtime/` | 地面约束 | `ApplyClamp` | GroundConstraintMode | MotionComposer | 🟡 |
| `MotionGroundLanding` | `2_Framework/Motion/Runtime/` | GroundTargeted 落地计算 | `TryResolveEndHeight`, `SampleTargetWorldY` | MotionProfileSO | MotionExecutor | 🟡 |
| `MotionYAxisConfig` | `2_Framework/Motion/Runtime/` | Y轴配置 (struct) | `YMotion`, `Gravity`, `GroundConstraint` | 枚举 | MotionProfileSO | 🔴 |
| `MotionYAxisLegacyMapping` | `2_Framework/Motion/Runtime/` | 旧Y轴策略→新配置映射 | `FromLegacy` | YAxisPolicy | MotionProfileSO | 🟢 |

### Skill Routes (Framework)

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `SkillEntryService` | `2_Framework/Skill/Routes/Runtime/` | ★ 技能入口总线(2000+行) | `Rebuild`, `TryResolveForIntent`, `NotifyRouteEntered`, `TickActive`, `AttachGraph` | RouteRuntime, CombatGraphRunner | Player, PlayerStateManager | 🔴 |
| `SkillRouteRuntime` | `2_Framework/Skill/Routes/Runtime/` | ★ Route运行时基类 | `Bind`, `CanCast`, `OnEnter`, `OnTick`, `OnExit`, `StartCooldown`, `EvaluateTransitions` | SkillRouteDefinition | SkillEntryService | 🔴 |
| `SkillStageRuntime` | `2_Framework/Skill/Routes/Runtime/` | Stage运行时 | `Enter`, `Tick`, `NormalizedTime`, `Completed` | SkillStageDefinition | SkillRouteRuntime | 🔴 |
| `SkillRouteContext` | `2_Framework/Skill/Routes/Runtime/` | Route上下文 (struct) | `Input`, `Stats`, `Resources`, `Self`, `HitTally` | 各系统 | SkillRouteRuntime | 🔴 |
| `SkillRouteRuntimeFactory` | `2_Framework/Skill/Routes/Runtime/` | Route运行时工厂 | `Create(RouteKind)` | RouteKind | SkillEntryService | 🟡 |
| `CombatGraphRunner` | `2_Framework/Skill/Routes/Runtime/` | ★ CombatGraph运行时 | `Attach`, `TryResolve`, `Tick`, `IsEnabled`, `CurrentNodeId` | CombatGraphAsset | SkillEntryService | 🔴 |
| `RouteResolver` | `2_Framework/Skill/Routes/Resolver/` | Route解析器 | 意图→Route匹配 | SkillEntryDefinition | SkillEntryService | 🟡 |
| `ConditionEvaluator` | `2_Framework/Skill/Routes/Resolver/` | 条件评估器 | `EvaluateAll` | SkillTransitionCondition | SkillRouteRuntime | 🟡 |
| `InputChordResolver` | `2_Framework/Skill/Routes/Resolver/` | 方向键和弦解析 | `Resolve(Vector2)` | MoveDirection8 | InputSemantic | 🟡 |
| `AbilityGateService` | `2_Framework/Skill/Routes/Runtime/` | 能力准入服务 | `Evaluate(route)` | AbilityGateRuleSO | SkillEntryService | 🟡 |
| `IRouteRegistryQuery` | `2_Framework/Skill/Routes/Runtime/` | Route注册查询接口 | — | 无 | SkillEntryService | 🟢 |
| `IRouteRuntimeHandle` | `2_Framework/Skill/Routes/Runtime/` | HUD句柄接口 | — | 无 | SkillEntryService | 🟡 |
| `RouteRuntimeHandle` | `2_Framework/Skill/Routes/Runtime/` | HUD句柄实现 | CD/图标/状态 | SkillRouteRuntime | HUD Presenter | 🟡 |

### Skill Routes (子类)

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `NormalRouteRuntime` | `2_Framework/Skill/Routes/Runtime/` | 普通单段释放 | `CanCast`, `OnEnter`, `OnTick` | SkillRouteRuntime | Factory | 🟡 |
| `ComboRouteRuntime` | `2_Framework/Skill/Routes/Runtime/` | 连招容器 | `ComboChain`, `SessionState`, `SubRoute推进` | SkillRouteRuntime | SkillEntryService | 🔴 |
| `ChargeRouteRuntime` | `2_Framework/Skill/Routes/Runtime/` | 蓄力/分段释放 | `Playback`, `FreezeNormalizedAdvance`, 蓄力状态机 | SkillRouteRuntime | PlayerActionState | 🔴 |
| `MultiStageRouteRuntime` | `2_Framework/Skill/Routes/Runtime/` | 多段自动衔接 | Auto-advance, 段切换 | SkillRouteRuntime | Factory | 🟡 |
| `DerivativeRouteRuntime` | `2_Framework/Skill/Routes/Runtime/` | 派生技 | 父Route→子Route派生 | SkillRouteRuntime | Factory | 🟢 |

### UI Framework

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `UIRoot` | `2_Framework/UI/Core/` | ★ UI根节点 | `ScreenStack`, `ModalStack`, `HUDRegistry` | UIScreenStack | 全局入口 | 🔴 |
| `UIScreenBase<T>` | `2_Framework/UI/Core/` | ★ UI屏幕泛型基类 | `Open(T)`, `Close`, 生命周期 | UIScreen | 所有Screen | 🔴 |
| `UIScreen` | `2_Framework/UI/Core/` | 屏幕实例基类 | `OnOpen`, `OnClose`, `State` | UIEmptyProps | UIScreenBase | 🔴 |
| `UIScreenStack` | `2_Framework/UI/Stack/` | 屏幕栈 | `Push`, `Pop`, `Peek` | UIScreen | UIRoot | 🔴 |
| `UIModalStack` | `2_Framework/UI/Stack/` | 模态栈 | `Push`, `Pop` | UIModal | UIRoot | 🟡 |
| `UIHUDRegistry` | `2_Framework/UI/Stack/` | HUD注册表 | `Register`, `Unregister` | UIHUD | UIRoot | 🟡 |
| `UIHUD` | `2_Framework/UI/Core/` | HUD基类 | 世界空间定位 | UIComponent | HUDRegistry | 🟡 |
| `ResourceBarView` | `2_Framework/UI/Components/` | 通用资源条 (HP/MP/Stamina) | `SetValue`, 缓冲动画 | ResourceBarBufferConfigSO | HUD | 🟡 |
| `RouteWidget` | `2_Framework/UI/Components/` | 技能路由Widget | `Bind(IRouteRuntimeHandle)` | IRouteRuntimeHandle | SkillBar | 🟡 |
| `DamageTextSystem` | `2_Framework/UI/FX/` | ★ 飘字系统 (CPU+GPU双路径) | `SpawnText`, `CPUDamageTextRenderer`, `GPUInstancedDamageTextRenderer` | DamageTextSettingsSO | DamagePipeline | 🟡 |
| `UIAnimationPlayer` | `2_Framework/UI/Animation/` | UI动画播放器 | `Play`, `Stop` | 无 | UI屏幕过渡 | 🟢 |
| `UITransitionPresetSO` | `4_Data/UI/` | UI过渡动画预设 (SO) | 动画曲线配置 | 无 | UIAnimationPlayer | 🟢 |
| `UIThemeSO` / `UIThemeService` | `4_Data/UI/` + `2_Framework/UI/Theme/` | UI主题系统 | 颜色/字体/样式 | 无 | UI组件 | 🟢 |

---

## 3_Gameplay (玩法层)

### Player

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `Player` | `3_Gameplay/Characters/Player/Core/` | ★★ 玩家实体 (500+行) | `IntentBuffer`, `InputReader`, `SkillEntries`, `States`, `MoveByLocomotionIntent`, `SetMovementIntent`, `Jump`, 所有Motor forwarder | Entity<T>, IEntity, IDamageable | 全系统 | 🔴 |
| `PlayerController` | `3_Gameplay/Characters/Player/Core/` | ★ 玩家控制器, 连续移动+离散意图 | `Update`, `ConsumeDiscreteIntents`, `TryDispatchEntry`, `ResolveWorldDirection`, 跑步粘性 | InputReader | Player | 🔴 |
| `PlayerStateManager` | `3_Gameplay/Characters/Player/Core/` | ★★ 4支柱FSM仲裁核心 | `OnPreLogicUpdate`, `BuildStateList`, 仲裁帧序 | EntityStateManager<Player> | Player | 🔴 |
| `PlayerState` | `3_Gameplay/Characters/Player/Core/` | Player状态基类 | — | EntityState<Player> | 4个状态 | 🔴 |
| `PlayerEvents` | `3_Gameplay/Characters/Player/Core/` | Player事件定义 | `PlayerJumpEvent`, `PlayerLandedEvent`, `PlayerAttackStartedEvent`等 | 无 | EventBus | 🟡 |
| `EntityAbilitySystem` | `3_Gameplay/Characters/Player/Core/` | 实体能力标签更新 | `Update(Player)` | EntityCapabilityTag | 各状态 | 🟡 |
| `TurnResolver` | `3_Gameplay/Characters/Player/Core/` | 转身解析器 | `Tick`, `ClearLock`, `TurnInfo` | TurnSettings | LocomotionState | 🟡 |

### Player States

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `PlayerActionState` | `3_Gameplay/Characters/Player/States/` | ★★ Action支柱 | `OnEnter`, `OnLogicUpdate`, `OnExit`, `SwapToStageAction`, `TryConsumeGameplayIntent` | MotionExecutor, ActionDataSO, SkillEntryService | PlayerStateManager | 🔴 |
| `PlayerLocomotionState` | `3_Gameplay/Characters/Player/States/` | ★ Locomotion支柱 (Idle/Walk/Run/Turn) | `OnEnter`, `OnLogicUpdate`, `OnExit`, `TryConsumeGameplayIntent` | TurnResolver, KCCMotor | PlayerStateManager | 🔴 |
| `PlayerAirborneState` | `3_Gameplay/Characters/Player/States/` | ★ Airborne支柱 (跳跃/下落) | `OnEnter`, `OnLogicUpdate`, `TryExitToLandOrLocomotion` | LocomotionGraphContext | PlayerStateManager | 🔴 |
| `PlayerDeadState` | `3_Gameplay/Characters/Player/States/` | Dead终态 | `OnEnter`, `OnExit`, `OnLogicUpdate` | 无 | PlayerStateManager | 🟡 |

### Action System

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `GameplayIntent` | `3_Gameplay/Combat/ActionSystem/` | ★ 意图数据 (struct, 零GC) | `Kind`, `TimeStamp`, `ExpireTime`, `ForbiddenTags`, `RequiredAllTags`, `Semantic`, `DirectionAxis`, `ComboIndex`, `MoveBuffered` | GameplayIntentKind | IntentBuffer | 🔴 |
| `GameplayIntentKind` | `3_Gameplay/Combat/ActionSystem/` | 意图种类枚举 (17 Skill_Entry + Jump + Move) | `None`, `Jump`, `Move`, `Skill_Entry_01..17` | SkillEntrySlot | GameplayIntent | 🔴 |
| `GameplayIntentBuffer` | `3_Gameplay/Combat/ActionSystem/` | 环形意图缓冲 (cap=16) | `Enqueue`, `TryPeek`, `Pop`, `FlushExpired`, `Clear` | GameplayIntent | Player, PlayerStateManager | 🔴 |
| `TransitionResolver` | `3_Gameplay/Combat/ActionSystem/` | 标签闸门仲裁 | `CanOfferIntent(ctx, intent, out reason)` | FrameContext, GameplayIntent | PlayerStateManager | 🔴 |
| `ActionInterruptResolver` | `3_Gameplay/Combat/ActionSystem/` | Action窗口内打断仲裁 | `CanInterrupt`, `IsCategoryAllowedAtWindow`, `ResolveIncomingCategory` | ActionDataSO, ActionWindow | PlayerActionState, PlayerController | 🔴 |
| `IntentRouter` | `3_Gameplay/Combat/ActionSystem/` | 意图→状态路由 | `Route`, `IsRoutable`, `PeekActionDataForRouting` | PlayerStateManager | 各State.TryConsumeGameplayIntent | 🔴 |
| `ActionTimelineRuntime` | `3_Gameplay/Combat/ActionSystem/` | 时间轴运行时 (HitFrame/Teleport/Markers) | `Tick` | ActionDataSO, ActionTimelineMarker | PlayerActionState | 🔴 |
| `ActionTimelinePlaybackState` | `3_Gameplay/Combat/ActionSystem/` | 时间轴播放状态 | `Reset`, `OnActionExit` | 无 | PlayerActionState | 🟡 |
| `FrameContext` | `3_Gameplay/Combat/ActionSystem/` | 帧上下文 (struct) | `Time`, `DeltaTime`, `IsGrounded`, `CurrentTags`, `CurrentAbilityTags`, `StaminaCurrent`等 | 各Tag | TransitionResolver | 🔴 |
| `ActionIntentRouting` | `3_Gameplay/Combat/ActionSystem/` | 意图车道解析 (A轴) | `ResolveLane`, `ResolveGraphParticipation` | ActionIntentCategory | PlayerStateManager | 🟡 |
| `GraphDualGatePolicy` | `3_Gameplay/Combat/ActionSystem/` | 双闸门策略 | `RequiresConsumeDualGate`, `ResolveParticipation` | GraphParticipation | PlayerActionState | 🟡 |

### Combat

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `DamagePipeline` | `3_Gameplay/Combat/Damage/` | ★ 伤害四阶段管线 | `Compute(CombatContext, HitContext) → DamageResult` | IDamageStage[] | Player.TakeDamage | 🔴 |
| `DamageInfo` | `3_Gameplay/Combat/Damage/` | 伤害信息 | `Amount`, `Source`, `HitPoint` | 无 | IDamageable | 🟡 |
| `DamageResult` | `3_Gameplay/Combat/Damage/` | 伤害结果 (struct) | `FinalDamage`, `IsCritical`, `Stages` | 无 | DamagePipeline | 🟡 |
| `CombatContext` | `3_Gameplay/Combat/Damage/` | 战斗上下文 (struct) | `AttackerAttackPower`, `DefenderDefense`, `DefenderCurrentHP`, `Tags` | 无 | DamagePipeline | 🟡 |
| `HitContext` | `3_Gameplay/Combat/Damage/` | 命中上下文 (struct) | `BaseDamage`, `IsCritical`, `CriticalMultiplier`, `HitPoint` | 无 | DamagePipeline | 🟡 |
| `BaseDamageStage` | `3_Gameplay/Combat/Damage/Stages/` | 基础伤害阶段 | `Compute` | IDamageStage | DamagePipeline | 🟡 |
| `DefenseReductionStage` | `3_Gameplay/Combat/Damage/Stages/` | 防御减免阶段 | `Compute` | IDamageStage | DamagePipeline | 🟡 |
| `CritStage` | `3_Gameplay/Combat/Damage/Stages/` | 暴击阶段 | `Compute` | IDamageStage | DamagePipeline | 🟡 |
| `FinalClampStage` | `3_Gameplay/Combat/Damage/Stages/` | 最终钳位阶段 | `Compute` | IDamageStage | DamagePipeline | 🟡 |
| `DamageTextEmitStage` | `3_Gameplay/Combat/Damage/Stages/` | 飘字发射阶段 | `Compute` | IDamageStage, DamageTextSystem | DamagePipeline | 🟡 |
| `IDamageStage` | `3_Gameplay/Combat/Damage/Stages/` | 伤害阶段接口 | `Execute` | 无 | DamagePipeline | 🔴 |
| `BuffStack` | `3_Gameplay/Combat/Buff/` | Buff堆叠管理 | `Apply`, `Remove`, `Tick`, 叠加策略 | BuffDefinitionSO, BuffInstance | Player, Entity | 🟡 |
| `BuffInstance` | `3_Gameplay/Combat/Buff/` | Buff实例 | `Duration`, `Stacks`, `TickInterval` | BuffDefinitionSO | BuffStack | 🟡 |
| `IBuffStack` | `3_Gameplay/Combat/Buff/` | Buff栈接口 | — | 无 | IEffectReceiver | 🟡 |

### Entities

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `Entity<T>` | `3_Gameplay/Entities/Core/` | ★ CRTP实体基类 | `Stats`, `Resources`, `Buffs`, `IsDead`, `PublishEvent` | StatSet, ResourcePool, BuffStack | Player | 🔴 |
| `EntityController` | `3_Gameplay/Entities/Core/` | 实体控制器基类 | `InjectMovementContext` | IGameModeMovementContext | PlayerController | 🟡 |
| `EntityState<T>` | `3_Gameplay/Entities/Core/` | ★ 实体状态基类 | `OnEnter`, `OnExit`, `OnLogicUpdate`, `TryConsumeGameplayIntent` | 无 | 所有PlayerState | 🔴 |
| `EntityStateManager<T>` | `3_Gameplay/Entities/Core/` | ★ 实体状态机基类 | `Change<TState>`, `ForceChange<TState>`, `OnPreLogicUpdate` | StateMachine<T> | PlayerStateManager | 🔴 |
| `ResourcePool` | `3_Gameplay/Entities/Resources/` | 资源池 (HP/MP/Stamina) | `RegisterSlot`, `Drain`, `Restore`, `GetCurrent`, `GetMax` | ResourceType | Player.Entity | 🔴 |
| `IResourcePool` | `3_Gameplay/Entities/Resources/` | 资源池接口 | — | 无 | IEntity | 🔴 |
| `StatSet` | `3_Gameplay/Entities/Stats/` | 属性计算 (Base+Modifier三阶段) | `Get`, `SetBase`, `AddModifier`, `RemoveModifier` | StatType, Modifier | Player.Entity | 🔴 |
| `IStatSet` / `IReadOnlyStatSet` | `3_Gameplay/Entities/Stats/` | 属性接口 | — | 无 | IEntity | 🔴 |
| `StatPipeline` | `3_Gameplay/Entities/Stats/` | 属性管线 | `Evaluate` | StatType, Modifier | StatSet | 🟡 |
| `RuntimeEntityStats` | `3_Gameplay/Entities/Stats/` | 运行时属性 | WalkSpeed, RunSpeed | StatSet | Player | 🟡 |

### Motion (Gameplay)

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `MotionExecutor` | `3_Gameplay/Motion/Runtime/` | ★★ 位移执行器 | `Begin`, `Tick`, `End`, `SetPlaybackContext`, `NormalizedTime`, `LastContribution` | MotionProfileSO, IMotorAdapter, IAnimSpeedControl | PlayerActionState | 🔴 |
| `PlayerKCCMotor` | `3_Gameplay/Motion/Runtime/` | ★★ KCC物理马达 (800+行) | `SetPlanarVelocity`, `SetVerticalSpeed`, `Jump`, `ApplyMotor`, `ApplyMotorFromGameplayVelocity`, `SuspendGravity`, `RefreshGroundedState` | KinematicMotorSolver, MotorSettingsSO | Player | 🔴 |
| `KinematicMotorSolver` | `3_Gameplay/Motion/Runtime/` | ★ KCC求解器 (CapsuleSweep+Collide&Slide) | `SolveDisplacementFromPivot`, `ProbeGroundBelowPivot`, `ResolveOverlapsAtPivot`, `TryStepDown`, 9道闸门 | MotorSettingsSO | PlayerKCCMotor | 🔴 |
| `MotionPlaybackContext` | `3_Gameplay/Motion/Runtime/` | Motion播放上下文 (struct) | `FreezeNormalizedAdvance`, `HasLoopWindow`, `LoopWindowStart/End`, `AnimatorSpeedOverride` | 无 | MotionExecutor, ChargeRouteRuntime | 🟡 |
| `MotionSpaceBasis` | `3_Gameplay/Motion/Runtime/` | Motion空间解析 | `ResolvePlanarForward` | MotionSpace, IGameModeMovementContext | Player | 🟡 |
| `IMotorAdapter` | `3_Gameplay/Motion/Runtime/` | 马达适配器接口 | `SetDesiredVelocity`, `SetMotionComposeContext`, `ApplyToPlayer` | 无 | MotionExecutor | 🟡 |
| `IPlayerMotor` | `3_Gameplay/Motion/Runtime/` | Player马达接口 | `SetPlanarVelocity`, `SetVerticalSpeed`, `Jump`, `ApplyMotor`等 | 无 | PlayerKCCMotor | 🔴 |
| `IAnimSpeedControl` | `3_Gameplay/Motion/Runtime/` | 动画速度控制接口 | `SetSpeed` | 无 | MotionExecutor | 🟡 |
| `PlayerMotorAdapter` | `3_Gameplay/Motion/Runtime/` | Player马达适配器实现 | `SetDesiredVelocity`, `ApplyToPlayer`, `SetMotionComposeContext` | IMotorAdapter, Player | MotionExecutor | 🟡 |

### Testing

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `TestDummy` | `3_Gameplay/Testing/Entities/` | 战斗假人 | 受击响应 | Entity, IDamageable | 场景 | 🟢 |
| `EffectTrigger` | `3_Gameplay/Testing/Triggers/` | 效果触发器 (陷阱) | 进入/离开触发 | EffectDataSO | 场景 | 🟢 |
| `CombatDebugHUD` | `3_Gameplay/Testing/UI/` | 战斗调试HUD | 实时数据显示 | 各系统 | 场景 | 🟢 |

---

## 4_Data (数据层)

### Skills Data

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `SkillEntryLoadoutSO` | `4_Data/1.Skills/Routes/` | ★ 技能装配总表 (SO) | `Bindings[]`, `CombatFlow`, `AbilityMap`, `LocomotionGraphContext`, `ContextGroups` | SkillEntryDefinition, CombatGraphAsset | Player | 🔴 |
| `SkillEntryDefinition` | `4_Data/1.Skills/Routes/` | ★ 技能入口定义 (SO) | `PrimaryGroup`, `NormalRoute`, `ComboRoute`, `ChargeRoute`, `MultiStageRoute`, `DerivativeRoute` | SkillRouteDefinition | SkillEntryLoadoutSO | 🔴 |
| `SkillRouteDefinition` | `4_Data/1.Skills/Routes/` | ★ Route基类定义 (SO) | `Stages[]`, `CooldownPolicy`, `ResourceConsumePolicy`, `Costs`, `AbilityGateRules`, `CooldownGroup` | SkillStageDefinition | SkillEntryDefinition | 🔴 |
| `SkillStageDefinition` | `4_Data/1.Skills/Routes/` | ★ Stage定义 (SO) | `Action`, `Transitions[]`, `Duration` | ActionDataSO | SkillRouteDefinition | 🔴 |
| `SkillTransition` | `4_Data/1.Skills/Routes/` | Stage过渡定义 (struct) | `Trigger`, `NextStage`, `NextRoute`, `Conditions`, `NormalizedWindow` | 无 | SkillStageDefinition | 🔴 |
| `SkillTransitionCondition` | `4_Data/1.Skills/Routes/` | 过渡条件定义 | `Type`, `FloatParam`, `BoolParam` | 无 | SkillTransition | 🟡 |
| `SkillEntrySlot` | `4_Data/1.Skills/Routes/` | ★ 17槽位枚举 | `LM`, `RM`, `Q`, `Shift`, `Space`, `R`, `Key0..9` | 无 | 全Input/Skill系统 | 🔴 |
| `SkillCostEntry` | `4_Data/1.Skills/Routes/` | 技能成本条目 | `ResourceType`, `BaseAmount`, `ConsumeOnlyOnHit` | ResourceType | SkillRouteDefinition | 🟡 |
| `NormalRouteDefinition` | `4_Data/1.Skills/Routes/` | 普通Route定义 (SO) | 继承 SkillRouteDefinition | 无 | SkillEntryDefinition | 🟡 |
| `ComboRouteDefinition` | `4_Data/1.Skills/Routes/` | 连招Route定义 (SO) | `ComboChain[]`, `ChainLength`, `ComboSessionResetTime`, `HasExtendedCombo` | 无 | SkillEntryDefinition | 🔴 |
| `ChargeRouteDefinition` | `4_Data/1.Skills/Routes/` | 蓄力Route定义 (SO) | `TapThreshold`, `HoldRelease 分档` | 无 | SkillEntryDefinition | 🔴 |
| `MultiStageRouteDefinition` | `4_Data/1.Skills/Routes/` | 多段Route定义 (SO) | 继承 SkillRouteDefinition, 多Stage | 无 | SkillEntryDefinition | 🟡 |
| `DerivativeRouteDefinition` | `4_Data/1.Skills/Routes/` | 派生Route定义 (SO) | `ParentRoute` | 无 | SkillEntryDefinition | 🟢 |
| `SkillGroupDefinition` | `4_Data/1.Skills/Routes/` | 技能组定义 (SO) | `Routes[]`, `DodgeRoutes[]`, `RollRoutes[]`, `PrimaryGroup` | SkillRouteDefinition | SkillEntryDefinition | 🟡 |
| `SkillContextGroupDefinition` | `4_Data/1.Skills/Routes/` | 上下文组定义 (SO) | `RequiredSlot`, `RequireDirectional` | SkillEntrySlot | SkillEntryLoadoutSO | 🟡 |
| `CombatGraphAsset` | `4_Data/1.Skills/Routes/` | ★ CombatFlow图资产 (SO) | `Nodes[]`, `Edges[]`, `EntryNode`, `IdleNode`, `RegisteredRoutes` | CombatFlowGraphNode | SkillEntryLoadoutSO | 🔴 |
| `CombatFlowGraphNodes` | `4_Data/1.Skills/Routes/CombatFlow/` | 图节点类型定义 | `CombatFlowNodeKind`, `CombatFlowGraphNode`, `CombatFlowGraphEdge` | 无 | CombatGraphAsset | 🔴 |
| `CombatFlowConditionDefinition` | `4_Data/1.Skills/Routes/CombatFlow/` | 图边条件定义 | `ConditionType`, 各条件参数 | 无 | CombatFlowGraphEdge | 🔴 |
| `CombatFlowConditionMerge` | `4_Data/1.Skills/Routes/CombatFlow/` | 条件合并策略 | `And/Or` | 无 | Edge | 🟢 |
| `CombatFlowData` | `4_Data/1.Skills/Routes/CombatFlow/` | 编译后运行时数据 | `CompiledNodes`, `CompiledEdges` | 无 | CombatGraphRunner | 🟡 |
| `AbilityGateRuleSO` | `4_Data/1.Skills/Routes/` | 能力准入规则 (SO) | `RequiredAll`, `Forbidden`, `Feature` | EntityCapabilityTag | AbilityMapSO | 🟡 |
| `AbilityMapSO` | `4_Data/1.Skills/Routes/` | 能力映射表 (SO) | `AbilitySemantic → Rule[]` | AbilityGateRuleSO | SkillEntryLoadoutSO | 🟡 |
| `DamageSheet` | `4_Data/1.Skills/Routes/` | 伤害表 | 伤害参数配置 | 无 | SkillRouteDefinition | 🟢 |
| `LocomotionGraphContextBinding` | `4_Data/1.Skills/Routes/` | Locomotion Graph上下文 (struct) | `JumpStart`, `JumpLoop`, `JumpLand` (ActionDataSO) | 无 | Player | 🔴 |
| `SkillRouteEnums` | `4_Data/1.Skills/Routes/` | Route枚举集 | `RouteKind`, `RouteCooldownPolicy`, `RouteResourceConsumePolicy`, `StageTransitionTrigger`, `DirectionalRouteType` | 无 | 全Skill系统 | 🔴 |
| `SkillEntryIntentFactory` | `4_Data/Catalogs/` | 意图工厂 | `ForEntry`, `ForJump`, `ForMove`, `ForEntryWithSemantic`, `ForEntryTapFallback` | GameplayIntent | InputSemantic, PlayerController | 🔴 |
| `SemanticConfigSO` | `4_Data/1.Skills/Input/` | 输入语义配置 (SO) | 每槽位阈值 | 无 | Player | 🟡 |
| `ISkillUnit` | `4_Data/1.Skills/` | 技能单元接口 | — | 无 | SkillRouteDefinition | 🟢 |

### Actions Data

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `ActionDataSO` | `4_Data/2.Actions/` | ★★ 动作数据资产 (SO) | `MainClip`, `Duration`, `AnimSpeed`, `MotionProfile`, `Windows[]`, `TimelineMarkers[]`, `TeleportTriggers[]`, `Category`, `InterruptPriority`, `InterruptStability`, `IntentCategory`, `GraphParticipation` | MotionProfileSO, AnimationClip | SkillStageDefinition | 🔴 |
| `ActionWindow` | `4_Data/2.Actions/` | ★ 动作窗口定义 (struct) | `NormalizedStart`, `NormalizedEnd`, `InterruptibleByCategories`, `MinIncomingPriority`, `AllowSelfInterrupt` | ActionCategory | ActionDataSO | 🔴 |
| `ActionTimelineMarker` | `4_Data/2.Actions/` | 时间轴标记 | `TriggerTime`, `FxA`/`Audio`/`Camera`/`TimeScale`参数 | 无 | ActionDataSO | 🟡 |
| `ActionTimeAuthority` | `4_Data/2.Actions/` | 时长权威计算 | `ResolveAuthoredLogicDurationSeconds` | 无 | ActionDataSO | 🟡 |
| `ActionCategory` | `4_Data/2.Actions/` | 动作类别比特掩码 | `Movement`, `Offense`, `Defensive`, `Utility`, `Locomotion` | 无 | ActionWindow, InterruptResolver | 🔴 |
| `ActionIntentCategory` | `4_Data/2.Actions/` | 意图车道枚举 | `Locomotion`, `Combat`, `Reaction`, `Interaction` | 无 | ActionDataSO | 🔴 |
| `GraphParticipation` | `4_Data/2.Actions/` | 图参与身份枚举 | `Auto`, `None`, `SourceOnly`, `Full` | 无 | ActionDataSO | 🟡 |
| `MotionPrincipalAxis` | `4_Data/2.Actions/` | Motion主轴枚举 | `X`, `Y`, `Z`, `PlanarXZ` | 无 | ActionDataSO | 🟢 |

### Motion Data

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `MotionProfileSO` | `4_Data/3.Motion/` | ★★ 运动曲线资产 (SO) | `AxisCurves`, `YMotion`, `Gravity`, `GroundConstraint`, `MotionSpace`, `AnimSpeedMode`, `SpeedOverTime`, `ScaleType`, `SourceClip` | MotionAxisCurves | ActionDataSO | 🔴 |
| `MotionAxisCurves` | `4_Data/3.Motion/` | ★ 三轴曲线数据 (struct) | `XCurve`, `YCurve`, `ZCurve`, `XScale`, `YScale`, `ZScale`, `HasAnyCurve`, `SampleLocalDelta`, `SampleLocalPosition` | AnimationCurve | MotionProfileSO | 🔴 |
| `MotionDurationResolver` | `4_Data/3.Motion/` | 时长解析器 | `Resolve(ActionDataSO, IStatsProvider)` | ActionDataSO | PlayerActionState | 🟡 |
| `MotionAxisExtractSource` | `4_Data/3.Motion/` | 提取来源枚举 | `Auto`, `Manual` | 无 | MotionProfileSO | 🟢 |
| `MotionCurveFitPipeline` | `4_Data/3.Motion/` | 曲线拟合管线 | 数据→曲线 | 无 | Editor | 🟢 |
| `AnimSpeedMode` | `4_Data/3.Motion/` | 动画速度模式枚举 | `Constant`, `Curve` | 无 | MotionProfileSO | 🟡 |
| `GravityMode` | `4_Data/3.Motion/` | 重力模式枚举 | `UseGravity`, `SuspendGravity`, `AdditiveGravity` | 无 | MotionProfileSO | 🔴 |
| `YMotionMode` | `4_Data/3.Motion/` | Y轴运动模式枚举 | `None`, `Curve`, `GroundTargeted` | 无 | MotionProfileSO | 🔴 |
| `GroundConstraintMode` | `4_Data/3.Motion/` | 地面约束模式枚举 | `ClampToGround`, `None` | 无 | MotionProfileSO | 🟡 |
| `MotionScaleType` | `4_Data/3.Motion/` | 属性缩放类型枚举 | `None`, `AttackSpeed`等 | 无 | MotionProfileSO | 🟡 |

### Stats / Buff / Combat Data

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `EntityStatsSO` | `4_Data/Stats/` | 属性模板基类 (SO) | 属性条目定义 | StatBaseEntry | Entity | 🟡 |
| `PlayerStatsSO` | `4_Data/Stats/` | Player属性模板 (SO) | 继承 EntityStatsSO | 无 | Player | 🟡 |
| `StatType` | `4_Data/Stats/` | 属性类型枚举 | `MaxHP`, `AttackPower`, `Defense`, `WalkSpeed`, `RunSpeed`, `CooldownReduction`等 | 无 | StatSet | 🔴 |
| `Modifier` | `4_Data/Stats/` | 属性修正 | `Type`, `Value`, `ModifierStage` | StatType | StatSet | 🟡 |
| `ResourceType` | `4_Data/Resources/` | 资源类型枚举 | `HP`, `MP`, `Stamina` | 无 | ResourcePool | 🔴 |
| `BuffDefinitionSO` | `4_Data/Buff/` | Buff定义 (SO) | `Duration`, `MaxStacks`, `TickInterval`, `Effects` | BuffEffectEntry | BuffStack | 🟡 |
| `HitShapeSO` | `4_Data/Combat/HitShape/` | 判定形状基类 (SO) | 形状参数 | 无 | ActionDataSO (Timeline) | 🟡 |
| `MotorSettingsSO` | `4_Data/Motor/` | KCC参数配置 (SO) | `Radius`, `Height`, `MaxSlopeAngle`, `StepOffset`, `SkinWidth`, `GroundSnapDistance` | 无 | PlayerKCCMotor | 🔴 |

---

## 5_Presentation (表现层)

| 类名 | 路径 | 职责 | 核心字段/方法 | 依赖 | 被引用 | 重要度 |
|------|------|------|-------------|------|--------|--------|
| `ActionTimelinePresentationPlayer` | `5_Presentation/Action/` | 动作表现播放器 | FX/Audio/Camera/TimeScale触发 | ActionTimelineMarker | ActionTimelineRuntime | 🟡 |
| `EntityAnimController` | `5_Presentation/Animation/Controllers/` | 实体动画控制器 | PlayableGraph操作 | Animator | Entity | 🔴 |
| `PlayerAnimController` | `5_Presentation/Animation/Controllers/` | Player动画控制器 | 继承EntityAnimController | 无 | Player | 🔴 |
| `PlayerAnimManagerSO` | `5_Presentation/Animation/Mappings/` | 动画映射表 (SO) | Clip→Anim Hash映射 | 无 | PlayerAnimController | 🟡 |
| `FootIKSystem` | `5_Presentation/Animation/IK/` | 脚部IK系统 | 地面适应 | Animator | PlayerAnimController | 🟢 |
| `ActionTimeScaleDriver` | `2_Framework/Presentation/` | 时停/慢动作驱动 | `Instance`, Time.timeScale控制 | 无 | ActionTimelineRuntime | 🟡 |
| `PlayerTurnBackFlowPresentation` | `5_Presentation/Turn/` | 转身表现流 | 转身动画驱动 | TurnInfo | PlayerLocomotionState | 🟢 |
| `PlayerTurnOrbPresentation` | `5_Presentation/Turn/` | 转身轨迹球 | 可视化转向 | TurnInfo | Debug | 🟢 |
| `VisualInterpolator` | `5_Presentation/Visual/` | 视觉插值器 | 平滑位置插值 | 无 | HUD/WorldUI | 🟢 |

---

## 引用热度 Top 20

| 排名 | 类名 | 被引用系统数 | 关键程度 |
|------|------|------------|---------|
| 1 | `Player` | 全系统 (StateManager, Motor, SkillEntries, Controller...) | 🔴🔴🔴 |
| 2 | `ActionDataSO` | ActionSystem, SkillSystem, MotionSystem, Editor | 🔴🔴🔴 |
| 3 | `GameplayIntent` | Input, Buffer, Arbitration, Router | 🔴🔴🔴 |
| 4 | `PlayerStateManager` | Player, Controller, AllStates | 🔴🔴🔴 |
| 5 | `SkillEntryService` | Player, StateManager, HUD | 🔴🔴🔴 |
| 6 | `PlayerKCCMotor` | Player, AllStates, MotionExecutor | 🔴🔴🔴 |
| 7 | `MotionProfileSO` | ActionDataSO, MotionExecutor, Editor | 🔴🔴🔴 |
| 8 | `InputReader` | PlayerController, PlayerStateManager | 🔴🔴🔴 |
| 9 | `MotionExecutor` | PlayerActionState | 🔴🔴 |
| 10 | `SkillRouteRuntime` | SkillEntryService, Factory | 🔴🔴 |
| 11 | `PlayerActionState` | PlayerStateManager | 🔴🔴 |
| 12 | `SkillRouteDefinition` | SkillEntryService, Factory, Editor | 🔴🔴 |
| 13 | `CombatGraphAsset` | SkillEntryLoadoutSO, CombatGraphRunner, Editor | 🔴🔴 |
| 14 | `GameplayTagContainer` | Player, TransitionResolver, Entity | 🔴🔴 |
| 15 | `IntentRouter` | AllStates, PlayerStateManager | 🔴🔴 |
| 16 | `StatSet` | Player.Entity, Stats | 🔴 |
| 17 | `ResourcePool` | Player.Entity, SkillSystem | 🔴 |
| 18 | `DamagePipeline` | Player, TestDummy | 🔴 |
| 19 | `SkillEntryLoadoutSO` | Player, SkillEntryService | 🔴 |
| 20 | `CombatGraphRunner` | SkillEntryService | 🔴 |
