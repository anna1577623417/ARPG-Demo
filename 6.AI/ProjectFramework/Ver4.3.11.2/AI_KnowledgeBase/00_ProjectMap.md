# 00_ProjectMap — 项目全景地图

> **生成时间**: 2026-06-08  
> **代码基线**: Ver 4.6+ (~386 C# 脚本, ~28,000 行)  
> **引擎**: Unity 2022 LTS · URP · Input System (New) · TextMeshPro  
> **物理**: 自研 KCC (Kinematic Character Controller) — CapsuleSweep + Collide&Slide  
> **动画**: PlayableGraph + Animator Controller + Animation Event

---

## 目录结构树

```
Assets/
├── AI Agent/                          # AI 知识库输出
│   ├── Output/
│   │   ├── Architecture/
│   │   ├── Blueprints/
│   │   ├── DataFlow/
│   │   ├── Interview/
│   │   ├── Modules/
│   │   ├── Refactor/
│   │   ├── Reviews/
│   │   └── Versions/
│   └── .obsidian/
│
├── Bundles/                           # 资源包 (Art / Audio / Config / Entities / Lua / Scenes / VFX)
│   ├── Art/Material_Shader/UI/        # UI Shader 材质
│   ├── Art/UI_Raw_Texture/            # UI 原始纹理 (General / Skill)
│   ├── Audio/                         # 音频资源
│   ├── Config/                        # 配置文件
│   ├── Entities/                      # 实体资源
│   │   ├── Anims/Animator_Controller/ # Animator Controller 资产
│   │   ├── Anims/FBX/                 # FBX 动画文件
│   │   ├── Material/                  # 实体材质
│   │   ├── Models/                    # 模型资源
│   │   └── Prefabs/                   # 预制体 (Character / Weapon)
│   ├── Environment/                   # 环境资源
│   │   ├── Map_Terrain/               # 地图/地形
│   │   └── Prefabs/Mesh_Env/          # 环境网格
│   ├── Lua/                           # Lua 脚本
│   ├── Scenes/PlayGround/             # 场景 (Map_01.unity)
│   └── VFX/                           # 视觉特效
│
├── GameMain/
│   ├── Launcher/UI/                   # 启动器 UI (Atlas / Fonts / Prefabs)
│   ├── Scripts/                       # ★ 核心代码 ★
│   │   ├── 1_Core/                    # 核心层
│   │   ├── 2_Framework/               # 框架层
│   │   ├── 3_Gameplay/                # 玩法层
│   │   ├── 4_Data/                    # 数据层
│   │   ├── 5_Presentation/            # 表现层
│   │   ├── 6.AI/                      # AI 配置 (Cursor / OpenClaw)
│   │   ├── Docs/                      # 文档
│   │   ├── Editor/                    # ★ 编辑器工具 ★
│   │   └── Tests/                     # 测试 (Editor / PlayMode)
│   └── Shaders/                       # HLSL Shader 文件
│
├── Pugins/                            # 原生插件
│
├── Settings/                          # 项目设置资产
│
├── TextMesh Pro/                      # TMP 资源
│
└── Third_Party_Assets/                # 第三方资产
    ├── Combat_VFX/                    # 战斗特效 (FXVille_BloodPack / Hit & Slashes / MasterMagicFX / Red_clue / VFX_Klaus)
    ├── Dynamic Sword Animset/         # 动态剑术动画集
    ├── GhostSamurai_Animset/          # 幽灵武士动画集
    ├── hero02_pushplayart/            # 角色动画 (attack/dash/jump/dead/idle...)
    ├── katana/                        # 武士刀模型
    ├── Starter Assets/                # Unity Starter Assets (含 ThirdPersonController)
    ├── swords_pack/                   # 剑模型包
    ├── TwinBladesAnimsetBase_V2/      # 双刃动画集
    └── WUXIA_Sword_sheath_AnimSet/    # 武侠收剑动画集
```

---

## 五层螺旋架构 — 层级地图

```
┌─────────────────────────────────────────────────────────────┐
│                     5_Presentation                          │
│   Animation · Shaders · Visual · UI · Turn                  │
│   → 只读表现，绝不反写逻辑层                                 │
├─────────────────────────────────────────────────────────────┤
│                     4_Data                                  │
│   Skills · Actions · Motion · Buff · Stats · Motor · UI    │
│   → ScriptableObject 不可变模板                             │
├─────────────────────────────────────────────────────────────┤
│                     3_Gameplay                              │
│   Characters · Combat · Entities · Motion · Party · Testing│
│   → Runtime 可变状态                                        │
├─────────────────────────────────────────────────────────────┤
│                     2_Framework                             │
│   Bootstrapping · Camera · Combat · DI · FSM · Tags        │
│   Input · Motion · Skill · UI · Presentation               │
│   → 通用框架、接口定义                                      │
├─────────────────────────────────────────────────────────────┤
│                     1_Core                                  │
│   EventBus · Utilities (Singleton / MonoSingleton)         │
│   → 零依赖底层设施                                          │
└─────────────────────────────────────────────────────────────┘
```

**依赖方向**: 只允许外层引用内层，绝不反向。

---

## 各层级核心目录职责

### 1_Core (核心层) — 9 文件
| 目录 | 职责 | 核心类 |
|------|------|--------|
| `EventBus/Core/` | 全局/本地事件总线 | `GlobalEventBus`, `LocalEventBus`, `IGameEvent` |
| `EventBus/Lifecycle/` | 事件订阅生命周期管理 | `EventSubscriptionBinder`, `EventSubscriptionScope` |
| `Utilities/` | 单例基类 | `Singleton<T>`, `MonoSingleton<T>` |

### 2_Framework (框架层) — ~90 文件
| 目录 | 职责 | 核心类 |
|------|------|--------|
| `Bootstrapping/` | 游戏启动/角色工厂 | `GameBootstrapper`, `PlayerFactory`, `SystemRoot` |
| `Camera/` | 相机控制系统 | `CameraController`, `ActionCameraController`, `MOBACameraController`, `GameModeManager` |
| `Combat/` | 战斗框架接口/效果系统 | `EffectSystem`, `IDamageable`, `IEffectReceiver`, `IEntity`, `ProjectileController` |
| `Contracts/` | 跨模块接口 | `IGameModeMovementContext` |
| `DI/` | 依赖注入 | `ServiceRegistry`, `IServiceResolver` |
| `FSM/` | 泛型状态机框架 | `StateMachine<T>`, `State<T>` |
| `GameplayTags/` | 5轨标签系统 | `GameplayTagContainer`, `GameplayTagMask`, `StateTag`, `StatusTag`, `AbilityTag`, `MechanicTag`, `FactionTag` |
| `Input/` | 输入读取/语义解析 | `InputReader`, `InputSemanticResolver`, `InputModifierBuffer`, `PlayerInputSystem` |
| `Motion/Runtime/` | 运动框架层 | `MotionComposer`, `MotionContribution`, `MotionGroundConstraint`, `GravityContribution` |
| `Skill/Routes/` | 技能路由框架 | `SkillRouteRuntime`, `SkillEntryService`, `CombatGraphRunner`, `RouteResolver`, `AbilityGateService` |
| `UI/` | UI 框架 | `UIRoot`, `UIScreenBase`, `UIScreenStack`, `UIModalStack`, `UIHUDRegistry`, `DamageTextSystem` |

### 3_Gameplay (玩法层) — ~55 文件
| 目录 | 职责 | 核心类 |
|------|------|--------|
| `Characters/Player/` | 玩家实体/控制器/状态机 | `Player`, `PlayerController`, `PlayerStateManager`, `PlayerActionState`, `PlayerLocomotionState`, `PlayerAirborneState`, `PlayerDeadState` |
| `Combat/ActionSystem/` | 意图/仲裁/路由/打断 | `GameplayIntent`, `GameplayIntentBuffer`, `IntentRouter`, `TransitionResolver`, `ActionInterruptResolver`, `ActionTimelineRuntime` |
| `Combat/Buff/` | Buff 堆叠系统 | `BuffStack`, `BuffInstance`, `IBuffStack` |
| `Combat/Damage/` | 伤害管线 | `DamagePipeline`, `DamageInfo`, `DamageResult`, `CombatContext`, 5个 `IDamageStage` 链路 |
| `Entities/` | 泛型实体基类 | `Entity<T>`, `EntityController`, `EntityState<T>`, `EntityStateManager<T>` |
| `Entities/Resources/` | 资源池 (HP/MP/Stamina) | `ResourcePool`, `IResourcePool` |
| `Entities/Stats/` | 属性系统 | `StatSet`, `StatPipeline`, `RuntimeEntityStats` |
| `Motion/Runtime/` | KCC 运动执行 | `MotionExecutor`, `PlayerKCCMotor`, `KinematicMotorSolver`, `MotionPlaybackContext` |
| `Party/` | 队伍/切换系统 | `InputRouter`, `TeamMemberRuntimeStub` |
| `Testing/` | 测试工具 | `TestDummy`, `EffectTrigger`, `CombatDebugHUD` |

### 4_Data (数据层) — ~60 文件
| 目录 | 职责 | 核心类 |
|------|------|--------|
| `1.Skills/` | 技能入口/路由/阶段定义 | `SkillEntryDefinition`, `SkillEntryLoadoutSO`, `SkillRouteDefinition`, `SkillStageDefinition`, `SkillEntrySlot`, `SkillTransition`, `CombatGraphAsset` |
| `1.Skills/Routes/` | 各路由类型定义 | `NormalRouteDefinition`, `ComboRouteDefinition`, `ChargeRouteDefinition`, `MultiStageRouteDefinition`, `DerivativeRouteDefinition`, `CombatFlowGraphNodes` |
| `2.Actions/` | 动作数据资产 | `ActionDataSO`, `ActionWindow`, `ActionTimelineMarker`, `ActionTimeAuthority` |
| `3.Motion/` | 运动曲线资产 | `MotionProfileSO`, `MotionAxisCurves`, `MotionDurationResolver`, `AnimSpeedMode`, `GravityMode`, `YMotionMode` |
| `Buff/` | Buff 定义 | `BuffDefinitionSO`, `BuffEffectEntry` |
| `Combat/HitShape/` | 判定形状 | `HitShapeSO`, `BoxShapeSO`, `CapsuleShapeSO`, `ConeShapeSO`, `SphereShapeSO` |
| `Motor/` | 马达参数 | `MotorSettingsSO` |
| `Resources/` | 资源类型 | `ResourceType` |
| `Stats/` | 属性模板 | `EntityStatsSO`, `PlayerStatsSO`, `MonsterStatsSO`, `StatType`, `Modifier` |
| `UI/` | UI 配置 | `UIThemeSO`, `DamageTextSettingsSO`, `ResourceBarBufferConfigSO` |

### 5_Presentation (表现层) — ~10 文件
| 目录 | 职责 | 核心类 |
|------|------|--------|
| `Action/` | 动作表现播放 | `ActionTimelinePresentationPlayer` |
| `Animation/` | 动画控制器/IK | `EntityAnimController`, `PlayerAnimController`, `FootIKSystem`, `PlayerAnimManagerSO` |
| `Shaders/` | 自定义 Shader | (HLSL 文件) |
| `Turn/` | 转身表现 | `PlayerTurnBackFlowPresentation`, `PlayerTurnOrbPresentation` |
| `UI/` | UI 输入表现 | `KeyItem`, `UIShortcutPanel` |
| `Visual/` | 视觉插值 | `VisualInterpolator` |

### Editor (编辑器层) — ~50 文件
| 目录 | 职责 | 核心类 |
|------|------|--------|
| `Authoring/` | ★ 资产创作工具 ★ | `ActionDataInspector`, `ActionDataTimelineEditor`, `MotionProfileEditor`, `MotionCurveGenerator`, `MotionAxisCurveEditorWindow`, `CombatFlowGraphCompiler`, `CombatFlowGraphValidator` |
| `CombatFlow/` | CombatFlow 图编辑器 | `CombatFlowGraphWindow`, `CombatFlowGraphView`, `CombatFlowGraphSelectionController`, `CombatFlowGraphNodeInspector` |
| `Gizmos/` | 场景 Gizmos | `MotionPathGizmoDrawer` |
| `Inspectors/` | 自定义 Inspector | `ActionCategoryPropertyDrawer`, `ActionWindowDrawer`, `NormalRouteDefinitionEditor`, `SkillRouteDefinitionEditor`, `CombatGraphAssetEditor` |
| `PropertyDrawers/` | 属性绘制器 | `MotionAxisCurvesDrawer` |

### Tests (测试层) — ~12 文件
| 目录 | 职责 |
|------|------|
| `Editor/` | EditMode 测试: ActionTimeAuthority, MotionComposer, MotionCurveFit, MotionGroundLanding, MotionYAxis, MultiStageRoute, SkillEntryRoute, StatSet |
| `PlayMode/` | PlayMode 测试: GameMath |

---

## 关键依赖图 (顶层)

```
1_Core (EventBus, Utilities)
   ↑
2_Framework (FSM, Tags, Input, Motion-Runtime, Skill-Routes, UI-Framework, Camera)
   ↑
3_Gameplay (Player, States, ActionSystem, Combat, Entities, Motion-Executor, KCC)
   ↑
4_Data (SO Assets: Skills, Actions, Motion, Stats, Buff, Motor)
   ↑
5_Presentation (Animation, Visual, Shaders, Turn)

Editor ← reads → 4_Data (Inspectors, Authoring tools)
Editor ← reads → 3_Gameplay (Gizmos, SceneBridge)
```

---

## 项目命名约定

| 领域 | 命名模式 | 示例 |
|------|---------|------|
| 实体 | `XEntity` / `Player` | `Player`, `Entity<T>` |
| 状态 | `XState` | `PlayerActionState`, `PlayerLocomotionState` |
| 状态机 | `XStateManager` | `PlayerStateManager`, `EntityStateManager<T>` |
| 意图 | `GameplayIntent` / `XIntent` | `GameplayIntent`, `GameplayIntentKind` |
| 路由/解析 | `XResolver` | `TransitionResolver`, `ActionInterruptResolver`, `IntentRouter`, `RouteResolver` |
| 执行器 | `XExecutor` | `MotionExecutor`, `TaskExecutor` |
| 马达 | `XMotor` | `PlayerKCCMotor` |
| 控制器 | `XController` | `PlayerController`, `CameraController` |
| 服务 | `XService` | `SkillEntryService`, `UIThemeService` |
| 数据定义 | `XDefinition` / `XDataSO` | `SkillRouteDefinition`, `ActionDataSO`, `MotionProfileSO` |
| 配置 | `XConfigSO` / `XSettingsSO` | `MotorSettingsSO`, `DamageTextSettingsSO` |
| 呈现器 | `XPresenter` | `PlayerHUDPresenter`, `SkillBarRoutePresenter` |
| 工厂 | `XFactory` | `PlayerFactory`, `SkillRouteRuntimeFactory` |

---

## 场景入口

- **PlayGround/Map_01.unity** — 当前唯一场景，测试/开发用
- `GameBootstrapper` + `SystemRoot` 负责场景初始化
- `PlayerFactory` 负责运行时角色生成
