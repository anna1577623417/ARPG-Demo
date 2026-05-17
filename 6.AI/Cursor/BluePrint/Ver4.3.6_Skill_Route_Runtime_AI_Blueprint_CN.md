# Ver 4.3.6 → Ver 4.6+ 技能路由运行时系统 — AI 落地蓝图

> 基于 `103.2 / 103.3(2) / 103.3(3)` 三份重构讨论文档汇总而成的**可执行**施工蓝图。
> 目标：将现仓库「单 SkillData 模型」演进为 **Skill Route Runtime System**（输入上下文驱动的技能路由运行时网络）。
> 本蓝图分 7 个 Phase，每个 Phase 都给出：**入手文件清单 / 改造清单 / 新增类型 / 验收准则 / 回滚锚点**。
> 全过程**保持 Inspector 兼容**与**旧资产可读**——任何旧字段统一 `[Obsolete]` 而非删除，由 `OnValidate` 桥接迁移。

---

## 0. 顶层设计契约（贯穿全 Phase 的不变量）

下列契约一旦违反即视为重构失败，必须立即回滚：

| # | 不变量 | 检查方式 |
|---|--------|---------|
| C1 | **输入层去技能语义化**：InputReader / Intent 只有「玩家按了哪个入口」，没有 Tap/Charge/Combo | grep `LightAttack/ChargeAttack/ComboAttack` 应仅剩 `[Obsolete]` |
| C2 | **Motion 永远不知道 Charge**：MotionExecutor 不再读 ChargeConfig | grep `_baseAnimSpeed` 中不混入 Charge 状态 |
| C3 | **最小技能单位 = Route**，不是 SkillData | `SkillRouteDefinition` 拥有 Icon/CD/Cost/Damage |
| C4 | **HUD 数据源 = RouteRuntime**，不是 Slot | HUD 不再持有 `SkillSlotType`，只持有 `IRouteRuntimeHandle` |
| C5 | **Stage 推进必须经 Transition Condition**，不再有"窗口自动开" | Q1→Q2 必须命中 `RequireHit/TargetFilter` |
| C6 | **方向 / 修饰键不进 Intent**，只进 `InputContext.MoveModifierBuffer` | grep `Intent_Dodge_Forward` 应为 0 |
| C7 | **0-GC 路径不变**：IntentBuffer / RouteRuntime / StageRuntime 全部结构体或对象池化 | 抽帧 Profiler，攻击循环 GC = 0 |

---

## 1. 总体目标架构（最终态）

```
Physical Input
    ↓
InputReader                  (硬件 → Slot 双写表，不变)
    ↓
InputInteractionResolver     (新增：Tap / Hold / DoubleTap / Lock)
    ↓
InputChordResolver           (新增：WASD modifier + Trigger → Direction)
    ↓
GameplayIntent.Skill_Entry_N (重命名：去掉 LightAttack/ChargeAttack 等)
    ↓
SkillEntryRuntime            (新增：入口注册表)
    ↓
RouteResolver                (新增：Charge > Combo > Normal 优先级解析)
    ↓
SkillRouteRuntime            (新增：Normal / Combo / Charge / Derivative / MultiStage)
    ↓
SkillStageRuntime            (新增：Stage cursor + Transition Window)
    ↓
Transition                   (新增：RequireHit / TargetFilter / OpenTime / CloseTime)
    ↓
Next Route / Next Stage / Exit (→ Cooldown 结算)
```

---

## 2. Phase 0：基线冻结与命名隔离（半天）

**目的**：建立可回滚的 git 锚点 + 让新旧系统在同一仓库内并行可编译。

### 入手文件
- 现有：`4_Data/1.Skills/*`、`2_Framework/Skill/**/*.cs`、`3_Gameplay/Combat/ActionSystem/GameplayIntent.cs`、`UI/Presenters/SkillBarPresenter.cs`

### 步骤
1. 打 git tag `pre-skill-route-refactor`。
2. 新建命名空间目录：
   - `4_Data/1.Skills/Routes/`（放新的 `SkillEntryDefinition` / `SkillRouteDefinition` 子类）
   - `2_Framework/Skill/Routes/Runtime/`（新 Route/Stage Runtime）
   - `2_Framework/Skill/Routes/Resolver/`（RouteResolver / InputChordResolver）
3. `SkillDataSO`、`SkillStageSO`、`SkillLoadoutSO`：在头部加 `[System.Obsolete("Ver4.6+ migrated to SkillEntryDefinition/SkillRouteDefinition. Read-only legacy.", error:false)]`，**保留**以便资产仍可读。
4. 在 `SkillEnums.cs` 内**新增**枚举 `SkillEntrySlot`，与旧 `SkillSlotType` 一对一对齐数值（强制 byte 同步），方便平滑迁移：
   ```csharp
   public enum SkillEntrySlot : byte {
       Skill_01_LM = 0, Skill_02_RM = 1, Skill_03_Q = 2, Skill_04_E = 3,
       Skill_05_R = 4,  Skill_06_Shift = 5, Skill_07_Space = 6, Skill_08_Mouse4 = 7,
       Skill_09_Key1 = 8, /* …17 */
   }
   ```

### 验收
- 编译通过；运行旧场景行为 100% 不变（旧 Slot 路径仍跑）。
- `git log --oneline pre-skill-route-refactor..HEAD` 仅显示「新增文件 + Obsolete 标注」。

### 回滚锚点
- `git reset --hard pre-skill-route-refactor`。

---

## 3. Phase 1：Runtime 收口（1～2 天）— **不动表现，只起骨架**

**目的**：把"`Route Runtime` / `Stage Runtime` / `Transition`"三件套搭出来，新管线**可空跑**。

### 新增类型

```csharp
// 4_Data/1.Skills/Routes/SkillEntryDefinition.cs
public class SkillEntryDefinition : ScriptableObject {
    public SkillEntrySlot Slot;
    public NormalRouteDefinition  NormalRoute;
    public ComboRouteDefinition   ComboRoute;
    public ChargeRouteDefinition  ChargeRoute;
    public List<DerivativeRouteDefinition> DerivativeRoutes;
}

// 4_Data/1.Skills/Routes/SkillRouteDefinition.cs
public abstract class SkillRouteDefinition : ScriptableObject {
    [Header("Identity")] public string RouteId; public Sprite Icon; public bool ShowOnHUD = true;
    [Header("Cooldown")] public CooldownPolicy CdPolicy; public float Cooldown;
                         public string CooldownGroup; public bool ShareCooldown;
    [Header("Cost")]     public SkillCost[] Costs;
    [Header("Damage")]   public DamageSheet Damage;
    [Header("Stages")]   public SkillStageDefinition[] Stages;
}

// 子类
public class NormalRouteDefinition     : SkillRouteDefinition {}
public class ComboRouteDefinition      : SkillRouteDefinition {
    public SkillRouteDefinition[] ComboChain;
    public float ComboResetTime = 1.2f;
    public bool  FallbackToNormal = true;
}
public class ChargeRouteDefinition     : SkillRouteDefinition {
    public ChargePresentationMode PresentationMode;   // SingleAction / MultiAction
    public SkillStageDefinition Startup, HoldLoop, Release, Cancel;
    public ChargeConfig Charge;
}
public class DerivativeRouteDefinition : SkillRouteDefinition {
    public SkillRouteDefinition Parent;
    public SkillTransitionCondition[] Unlock;
}
public class MultiStageRouteDefinition : SkillRouteDefinition {}   // Stages[] 即多段

// 4_Data/1.Skills/Routes/SkillStageDefinition.cs
public class SkillStageDefinition : ScriptableObject {
    public ActionDataSO Action;
    public float Cooldown;                    // 段独立 CD（可叠 Route CD）
    public SkillCost[] Costs;                 // 段独立资源
    public SkillTransition[] Transitions;
}

// 4_Data/1.Skills/Routes/SkillTransition.cs  (SO 或 struct）
public class SkillTransition {
    public SkillStageDefinition NextStage;
    public SkillRouteDefinition NextRoute;    // 可跨 Route（派生）
    public float OpenTime;     // Action 归一化或秒数（用一个 enum 切换）
    public float CloseTime;
    public SkillTransitionCondition[] Conditions;
}

public class SkillTransitionCondition {
    public bool RequireHit;
    public TransitionTargetRule TargetRule;   // Any/HeroOnly/NonMinion…
    public ulong RequiredTags;                // GameplayTag 5 轨
    public bool RequireInput;                 // 是否需要再次输入
    public SkillEntrySlot ExpectedInput;
}
```

### Runtime 类型

```csharp
// 2_Framework/Skill/Routes/Runtime
public sealed class SkillEntryRuntime { /* slot, attached entry, current route */ }
public abstract class SkillRouteRuntime {
    public SkillRouteDefinition Definition;
    public SkillStageRuntime Stage;
    public bool   IsActive;
    public float  CdRemaining;
    public float  CdScaledTotal;          // 给 HUD 进度条用，单调 0→1
    public abstract void OnEnter(in SkillRouteContext ctx);
    public abstract void OnTick (in SkillRouteContext ctx, float dt);
    public abstract void OnExit (in SkillRouteContext ctx);
}
public sealed class NormalRouteRuntime  : SkillRouteRuntime {}
public sealed class ComboRouteRuntime   : SkillRouteRuntime { int _comboIdx; float _lastInputTime; }
public sealed class ChargeRouteRuntime  : SkillRouteRuntime { ChargeCastHandler _handler; MotionPlaybackContext _playback; }
public sealed class MultiStageRouteRuntime : SkillRouteRuntime { StageTransitionWindow _window; }
public sealed class DerivativeRouteRuntime : SkillRouteRuntime {}

public sealed class SkillStageRuntime {
    public int    CurrentStageIndex;
    public SkillStageDefinition Current;
    public float  Elapsed;
    public bool   WaitingInput;
    public StageTransitionWindow Window;     // 可为 null
}
public struct StageTransitionWindow {
    public bool IsOpen; public float RemainingTime; public SkillStageDefinition NextStage;
}
```

### 步骤
1. 创建上述所有类型（**先空实现**），保证编译。
2. `Player.cs` 新增 **`SkillRouteService` 字段**（与现 `SkillRuntime[]` **并存**），暂不接入仲裁。
3. 在 `PlayerStateManager` 加一行 Debug：`if (debugRouteRuntime) Debug.Log(routeService.Snapshot)`，用于核查空跑数据流。

### 验收
- 新代码全部编译。
- 旧管线行为 100% 不变（双系统并存，新系统未通电）。

### 回滚锚点
- `git tag phase1-runtime-skeleton`。

---

## 4. Phase 2：RouteResolver + 优先级（2～3 天）— **第一次接电**

**目的**：让 `SkillEntryRuntime` 实际跑起来，按 **Charge > Combo > Normal** 解析为 RouteRuntime。

### 新增

```csharp
// 2_Framework/Skill/Routes/Resolver/RouteResolver.cs
public static class RouteResolver {
    public static SkillRouteDefinition Resolve(
        SkillEntryDefinition entry,
        in InputContext ctx,
        SkillEntryRuntime runtime,
        out RouteResolveReason reason)
    {
        // 1. 若 entry.ChargeRoute != null 且 ctx.HoldSeconds >= chargeTapThreshold → Charge
        // 2. 若 entry.ComboRoute  != null 且 runtime.ComboWindowOpen          → Combo
        // 3. 否则                                                              → Normal
    }
}
```

### 改造步骤
1. **`SkillSystem.TryPrepareIntentForSkills`** 内：
   - 若 `player.UseRouteRuntime`（开关默认 false，逐角色灰度），走新分支：
     ```
     entry = loadout.ResolveEntry(intent.Slot)
     route = RouteResolver.Resolve(entry, ctx, entryRuntime, out reason)
     runtime = routeService.GetOrCreate(route)
     if (!runtime.CanCast(stats, resources, ref tags, player)) return false
     intent.Action = runtime.Stage.Current.Action
     ```
   - 否则继续走旧 `SkillSegmentResolver` 路径（不动）。
2. `RoutePriority` 改为可在 `SkillEntryDefinition` 配置（默认 Charge > Combo > Normal）。
3. **`ComboRouteRuntime`**：
   - 维护 `_comboIdx / _lastInputTime`；超时 → ResetCombo → Fallback Normal（若开启）。
4. **`ChargeRouteRuntime`**：
   - 不再调 `SkillChargeCommit.TryApplyChargeOverride`；改为运行时进段：`Startup → HoldLoop → Release/Cancel`。
   - 内部 `ChargeCastHandler` 维持 v4.3.5 现状（已成熟）。
5. **CD 模型迁移**：
   - 旧 `SkillRuntime.StartCooldown` 复制实现到 `SkillRouteRuntime.StartCooldown`，按 `CooldownPolicy` 在 `OnRouteStart/OnStageStart/OnLastStageEnd/OnSkillExit` 触发。
   - `CooldownGroup` 非空时，由 `SkillGcd` 的扩展 `GroupGcdRegistry` 联动写入同组所有 Route 的 `CdRemaining`。

### 验收
- 灰度角色（`Player.UseRouteRuntime=true`）的 LM 普攻：Tap → Normal，Hold → Charge，多次点击 → Combo。
- 灰度角色 CD 显示与旧管线一致（误差 < 1 帧）。
- 关闭灰度开关，行为完全回退到旧路径。

### 回滚锚点
- `git tag phase2-resolver-online`。

---

## 5. Phase 3：Charge / Motion 解耦（2 天）— **拔掉 ChargeMicroPhase**

**目的**：兑现契约 C2，把 PlayerActionState 的 `ChargeMicroPhase` 全删，改由 `MotionPlaybackContext` 接管。

### 新增

```csharp
// 2_Framework/Motion/Runtime/MotionPlaybackContext.cs
public sealed class MotionPlaybackContext {
    public float BaseSpeed = 1f;
    public float RuntimeSpeedMultiplier = 1f;
    public bool  Pause;                       // ChargeRuntime 持有，Hold 满时 Pause=true
    public LoopWindow LoopWindow;             // Hold 循环窗：[normalizedStart, normalizedEnd]
}
public struct LoopWindow { public float Start, End; public bool Active; }
```

### 改造步骤
1. **`MotionExecutor.Tick`**：
   - 接受 `MotionPlaybackContext` 而非 baseAnimSpeed 标量；
   - `finalClipSpeed = playback.BaseSpeed × playback.RuntimeSpeedMultiplier × profileFactor`；
   - `playback.Pause==true` 时 `_elapsed` 不前进，只持续写 `desiredVelocity = 0`；
   - `LoopWindow.Active==true` 时 `_elapsed` 在 `[Start*duration, End*duration]` 内循环。
2. **`ChargeRouteRuntime.OnTick`**：
   - 蓄满 → `_playback.LoopWindow.Active = true`（进入 HoldLoop）；
   - 玩家松手 → `_playback.LoopWindow.Active = false`，推进 Stage 到 `Release`。
3. **`PlayerActionState`**：
   - 删除全部 `ChargeMicroPhase / ApproachingHold / HoldingAtPoint / Executing` 字段与分支；
   - `OnEnter` 改为：
     ```csharp
     m_motionExecutor.Begin(action.MotionProfile, duration, motionDir, pos,
         playback: player.ActiveRouteRuntime?.Playback ?? MotionPlaybackContext.Default);
     ```
4. **`ChargeConfig`**：
   - `holdAnimSpeedAtFull` 字段标 `[Obsolete]`，由 `MotionPlaybackContext.Pause/RuntimeSpeedMultiplier` 完全替代。
   - `OnValidate` 桥接：旧资产 `holdAnimSpeedAtFull = 0` → 新 `Pause=true`；非 0 → `RuntimeSpeedMultiplier=value`。

### 验收
- PlayerActionState 单文件行数 ≥ -150 行（删除微相位）。
- 蓄满定格 / 抖循环 / 松手出招表现与重构前**像素级一致**（录屏对比）。
- `MotionExecutor` 不再 `using` 任何 Charge 类型。

### 回滚锚点
- `git tag phase3-motion-decoupled`。

---

## 6. Phase 4：HUD 动态加载（2～3 天）— **从 Slot 切到 Route**

**目的**：兑现契约 C4。一个 Entry 渲染多个 RouteWidget（LM_Normal / LM_Combo / LM_Charge 并排）。

### 新增

```csharp
// 2_Framework/UI/Components/RouteWidget.cs
public sealed class RouteWidget : MonoBehaviour {
    public Image IconImg;
    public Image CooldownMask;        // FillAmount = 1 - CdRemaining/CdScaledTotal
    public Image ChargeBar;           // ChargeRoute 时显示
    public TMP_Text KeyLabel;
    public ComboOverlay ComboOverlay; // ComboRoute 时显示
    public MultiStageOverlay StageOverlay; // MultiStage 时显示 Q1/Q2/Q3

    public void Bind(IRouteRuntimeHandle handle) { _handle = handle; }
    void LateUpdate() {
        if (_handle == null) return;
        IconImg.sprite = _handle.Icon;
        CooldownMask.fillAmount = 1f - _handle.CdProgress01;
        if (_handle.HasChargeBar) ChargeBar.fillAmount = _handle.ChargeProgress01;
        // ComboOverlay / StageOverlay 同理
    }
}

// 2_Framework/UI/IRouteRuntimeHandle.cs  (HUD 只读接口)
public interface IRouteRuntimeHandle {
    Sprite Icon { get; }
    float  CdProgress01 { get; }     // 0..1 单调
    bool   HasChargeBar { get; }
    float  ChargeProgress01 { get; }
    int    ComboStep { get; }        // -1 = N/A
    int    MultiStageIndex { get; }  // -1 = N/A
    float  TransitionWindowRemain { get; }
}
```

### 改造步骤
1. **`SkillBarPresenter`**：
   - 移除 `InspectorSlots` 模式；保留 `InstantiateFromPlayerLoadout`。
   - 新逻辑：
     ```
     foreach (entry in loadout.Entries)
         foreach (route in entry.AllVisibleRoutes())
             if (route.ShowOnHUD)
                 var widget = pool.Spawn(routeWidgetPrefab, parent: gridLayout);
                 widget.Bind(routeService.GetHandle(route));
     ```
2. **`GridLayout`**：保留现状；新增 `entryGroup` 子容器（同 Entry 的 Route 横向并排，多 Entry 纵向堆叠）。
3. **`SkillLoadoutSO`** 双轨：
   - 新字段 `SkillEntryDefinition[] entries;`
   - 旧 `bindings[]` 标 `[Obsolete]`，`OnValidate` 内若 `entries==null` 自动从旧 bindings 合成 Entry（每个 Slot 一个 Entry，仅 NormalRoute）。
4. **HUD Debug Overlay**（必做，调试期开关）：
   ```
   ┌ Entry: Skill_01_LM
   │  Route: Combo (idx=2/4, window=0.31s)
   │  Stage: Q2 (elapsed=0.42s)
   │  CD: 1.2/3.0
   │  Charge: 0.75 (Pause=false)
   └ Transition: Next=Q3 (open in 0.18s, RequireHit=true)
   ```

### 验收
- 不修改任何 ScriptableObject 旧资产 → HUD 仍正确显示（OnValidate 自动桥接）。
- 切换到新 Entry 资产 → HUD 自动多出 ChargeBar / ComboOverlay。
- HUD 不出现 `SkillSlotType` 直接引用（grep 验证）。

### 回滚锚点
- `git tag phase4-hud-route-widgets`。

---

## 7. Phase 5：Transition System（2 天）— **Stage 推进条件化**

**目的**：兑现契约 C5。MultiStage / 派生技能必须经 `SkillTransitionCondition` 显式开启。

### 改造步骤
1. **`MultiStageRouteRuntime.OnTick`**：
   - 当前 Stage `Action` 到尾时：
     ```
     foreach (t in stage.Transitions)
         if (t.OpenTime <= elapsed <= t.CloseTime
             && ConditionEvaluator.Evaluate(t.Conditions, ctx, lastHit))
             OpenWindow(t.NextStage, t.CloseTime - elapsed);
     ```
2. **`ConditionEvaluator`**：
   - `RequireHit` → 读 `ctx.LastFrameHits` 集合；
   - `TargetRule` → 配合 `HitInfo.TargetTag`；
   - `RequiredTags` → 与 `Player.Tags` 五轨做掩码 AND。
3. **派生技解锁**：
   - `DerivativeRouteRuntime` 在 `OnEnter` 时检查 `Definition.Unlock[]`，未满足则 `IsActive=false`，HUD 同步置灰。
4. **Q1→Q2 盲僧 Q 范式**（LOL 经典）测试用例：
   - Q1 命中英雄 → 开 Q2 窗 3s；
   - Q1 命中小兵 → 不开窗；
   - 3s 内未再输入 → 直接退出，CD 按 `OnLastStageEnd` 结算。

### 验收
- 撰写 `Tests/SkillRouteTransitionTests.cs`：
  - 命中英雄开窗 / 命中小兵不开窗 / 超时未输入 / 窗内输入 / 窗外输入 5 个用例全绿。
- `Player.DebugInterruptFlow=true` 时控制台日志含 `[Transition] OPEN Q2 (cond=RequireHit ✓ TargetRule=HeroOnly ✓)`。

### 回滚锚点
- `git tag phase5-transition-conditions`。

---

## 8. Phase 6：Chord / Direction 输入解释层（2 天）— **方向闪避**

**目的**：兑现契约 C6。WASD 永远只做 modifier，闪避方向由 RuntimeContext 决定。

### 新增

```csharp
// 2_Framework/Input/InputContext.cs
public struct InputContext {
    public Vector2 MoveInput;
    public Vector2 MoveBuffered;          // 最近 0.15s 内的有效 Move（防抖）
    public bool    SkillPressed;
    public bool    SkillReleased;
    public bool    SkillHolding;
    public SkillEntrySlot TriggerSlot;
    public float   HoldSeconds;
}

// 2_Framework/Input/InputModifierBuffer.cs
public sealed class InputModifierBuffer {
    public Vector2 LastMoveDirection;
    public float   LastMoveTime;
    public const float BufferSeconds = 0.15f;
    public Vector2 GetBufferedMove(float now) =>
        (now - LastMoveTime) <= BufferSeconds ? LastMoveDirection : Vector2.zero;
}

// 2_Framework/Skill/Routes/Resolver/InputChordResolver.cs
public static class InputChordResolver {
    public static DirectionalRouteType Resolve(in InputContext ctx) {
        var m = ctx.MoveBuffered;
        if (m.y >  0.5f) return DirectionalRouteType.Forward;
        if (m.y < -0.5f) return DirectionalRouteType.Backward;
        if (m.x >  0.5f) return DirectionalRouteType.Right;
        if (m.x < -0.5f) return DirectionalRouteType.Left;
        return DirectionalRouteType.Forward;   // 默认前向
    }
}

// 4_Data/1.Skills/Routes/DirectionalRouteSet.cs
public class DirectionalRouteSet : SkillRouteDefinition {
    public SkillRouteDefinition Forward, Backward, Left, Right;
}
```

### 改造步骤
1. **`InputReader.OnMove` Hook**：
   - 每次 MoveInput 改变时写 `InputModifierBuffer.LastMoveDirection / LastMoveTime`。
2. **`PlayerController.ConsumeDiscreteIntents`**：
   - 在 `TryDispatchSlot(SkillEntrySlot.Skill_07_Space)`（闪避）时不再调用 `ResolveDodgeActionFromMoveset`，改：
     ```
     var ictx = BuildInputContext(slot, holdSeconds);
     intent = PlayerIntentCatalog.ForSlot(slot, time, inputCtx: ictx);
     ```
3. **`RouteResolver.Resolve`** 增加方向分支：
   ```
   if (route is DirectionalRouteSet ds) {
       var dir = InputChordResolver.Resolve(ctx);
       return ds.SelectByDirection(dir);
   }
   ```
4. **Intent 命名清理**：
   - `GameplayIntentKind.LightAttack/ChargeAttack/ComboAttack/Dodge/SwordDash` 全部 `[Obsolete]`，新增 `Skill_Entry_01..17`。
   - `IntentRouter` 把 Obsolete Kind 自动桥接到 `Skill_Entry_NN`（一次性 LUT）。

### 验收
- `W + Space` = 前闪 / `S + Space` = 后翻 / `A+Space` = 左闪 / `D+Space` = 右闪，**4 个用例全部命中**。
- 先松 W 0.1s 再按 Space 仍判定为前闪（缓冲窗生效）。
- 锁定状态下 `S + Space` 自动选 `BackwardLockedRoute`（如存在 LockedSet）。

### 回滚锚点
- `git tag phase6-chord-direction`。

---

## 9. Phase 7：旧 Slot/Intent 全面下线 + Debug Overlay（1～2 天）

**目的**：清掉 `[Obsolete]` 的腐肉，对外发布 Ver 4.6.0。

### 步骤
1. 删除 / 内联：
   - `SkillSegmentResolver`（合并到 `RouteResolver`）；
   - `SkillChargeCommit`（其能力被 `ChargeRouteRuntime` + `Transition` 接管）；
   - `PrimaryAttackPressTracker` 的 Tap/Hold 判定 **移到 `InputInteractionResolver`**；
   - `GameplayIntentKind` 中所有 `[Obsolete]` 值。
2. `SkillLoadoutSO.bindings[]` 彻底删除；保留 `entries[]`。
3. `SkillDataSO` 完全 Obsolete，但保留只读 ScriptableObject 以兼容 Asset Bundle 旧版本；提供编辑器迁移工具：
   - `Tools/Migrate Legacy SkillData → SkillRoute` 菜单（一次性扫工程所有 `SkillDataSO`，生成对应 Entry+Route+Stage 资产）。
4. **Runtime Debug Overlay**（IMGUI / UI Toolkit 二选一）：
   ```
   Current Entry:    Skill_01_LM
   Current Route:    ChargeRoute  (priority=0)
   Current Stage:    HoldLoop  (elapsed=0.85s, loop=[0.32, 0.68])
   Charge Ratio:     0.95
   Combo Timer:      —
   CD Group:         LM_Main (rem 0.0s)
   Current Trans:    Release  (RequireInput=Released, RequireHit=false)
   Playback:         Pause=false, Speed×=0.05, BaseSpeed=1.0
   ```

### 验收
- `grep "SkillSlotType" Assets/GameMain/Scripts/**/*.cs` 仅在 Editor 迁移工具内出现。
- `grep "SkillDataSO " Assets/GameMain/Scripts/**/*.cs` 仅在 Editor 迁移工具内出现。
- 端到端冒烟：5 个角色 × 6 个 Entry × Tap/Hold/Combo/Dir 四种输入 = 120 路径全绿。
- 0-GC：Profiler 1 分钟连续战斗 GC = 0。

### 回滚锚点
- `git tag v4.6.0-skill-route-runtime`。

---

## 10. Phase 8（可选 / Runtime 稳定后）：Graph Editor

> 文档明确要求："Runtime 稳定前不要做。" Phase 0-7 稳定 ≥ 2 周后才启动。

- 节点类型：Entry / Route / Stage / Transition / Condition。
- 用 Unity GraphView API（与 ShaderGraph 同栈）。
- 数据反序列化复用 SO 即可，**不引入新数据格式**。

---

## 11. Inspector / HelpBox 重构（贯穿）

- **Inspector** 改为 `[Foldout("Normal Route")] [Foldout("Combo Route")] [Foldout("Charge Route")]`（Odin 或自写 PropertyDrawer）。
- **HelpBox** 改按需展开：每个 Foldout 顶部一个 `[?]` 按钮，弹出该 Route 类型的引导式 HelpBox（含图示 GIF 路径占位）。

---

## 12. 总进度甘特（理想节奏）

| Day | Phase | 关键产出 |
|-----|-------|---------|
| D1 | 0 | 命名隔离 + git tag |
| D2-D3 | 1 | Runtime 骨架 |
| D4-D6 | 2 | RouteResolver 接电（灰度） |
| D7-D8 | 3 | Charge/Motion 解耦 |
| D9-D11 | 4 | HUD Route Widget |
| D12-D13 | 5 | Transition 条件化 |
| D14-D15 | 6 | Chord + 方向闪避 |
| D16-D17 | 7 | 老系统下线 + Debug Overlay |
| D18+ | (稳定 2 周) | (可选) Graph Editor |

---

## 13. AI 执行守则（每个 Phase 都必须遵守）

1. **每个 Phase 起手**：先读 `Ver4.3.5_Input_To_Skill_TechDoc_CN.md` 对应章节确认基线，再读本蓝图对应 Phase。
2. **不允许跨 Phase 改文件**：例如 Phase 1 不许碰 HUD，Phase 4 不许碰 Charge/Motion。
3. **每个 Phase 结束**：
   - 跑 `Tests/` 下所有 `SkillRoute*Tests.cs`；
   - 跑冒烟脚本 `Tools/SmokeTest_AllRoutes.cs`；
   - 打 `git tag phaseN-XXX`；
   - 写 `Docs/CHANGELOG/Ver4.3.6_PhaseN.md`（≤ 100 行）。
4. **回滚红线**：契约 C1-C7 任一违反、Profiler GC > 0、120 路径冒烟失败 → 立即 `git reset --hard phase(N-1)-XXX`。
5. **不创建文档文件**（除非用户明确要求）：本蓝图与 CHANGELOG 是已批准的例外。

---

## 14. 关键术语对照表（旧 → 新）

| Ver 4.3.5（旧） | Ver 4.6+（新） | 备注 |
|---------------|---------------|------|
| `SkillSlotType` | `SkillEntrySlot` | 一对一数值对齐 |
| `SkillDataSO` | `SkillRouteDefinition` 子类 | 最小技能单位由"技能数据"变为"路由" |
| `SkillStageSO` | `SkillStageDefinition` | 加 Cooldown / Costs / Transitions[] |
| `SkillRuntime` | `SkillRouteRuntime` + `SkillStageRuntime` | 拆分 |
| `SkillLoadoutSO.bindings` | `SkillLoadoutSO.entries` | Entry 替代 Slot 绑定 |
| `GameplayIntentKind.LightAttack/Charge/Combo` | `GameplayIntent.Skill_Entry_01` | 去技能语义化 |
| `ChargeMicroPhase` (in PlayerActionState) | `ChargeRouteRuntime.Stages` | 微相位下沉到 RouteRuntime |
| `SkillSegmentResolver` | `RouteResolver` | 合并 |
| `SkillChargeCommit.TryApplyChargeOverride` | `ChargeRouteRuntime.OnTick` + `ChargeLevel` 自动选档 | 内化 |
| `MotionExecutor.Begin(baseAnimSpeed=…)` | `MotionExecutor.Begin(MotionPlaybackContext)` | Motion 不再知 Charge |
| `SlotBinding[]` 在 HUD | `RouteWidget[]` 在 HUD | 一对多 |

---

## 15. 结语

本蓝图最重要的一句话：

> **重构终点不是"更复杂的技能系统"，而是"更简单的输入到表现"路径——输入只表达入口，Runtime 解释一切。**

任何 Phase 结束后若发现新分支带来的复杂度超过旧路径，**必须暂停并回滚**，重新设计该 Phase 的步骤拆分。

*本文档版本：v1.0；基线提交：当前工作树；上游讨论：103.2 / 103.3(2) / 103.3(3)。*
