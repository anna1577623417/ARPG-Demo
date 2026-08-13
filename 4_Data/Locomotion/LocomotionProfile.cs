using System;
using UnityEngine;

/// <summary>
/// 角色级 Locomotion 行为资产（158.2 §5.2）—— 与 <see cref="SkillEntryLoadoutSO"/> 平级，
/// 描述"这个角色会做哪些移动 + 用什么动画 + 哪些是 Action 化的过渡"。
///
/// ═══ 三个职责 ═══
///   1. <see cref="EnabledStates"/> 状态注册（逻辑层）—— 决定 Resolver 可以"派发"哪些状态；未勾选即一级降级。
///   2. <see cref="Bindings"/> 状态绑定（表现 + 路径）—— 每个状态对应一个 Discrete Action 或 Continuous Clip。
///   3. <see cref="Tuning"/> 引用一份 <see cref="LocomotionTuningSO"/> 提供速度系数 / 加速度 / 跳跃力。
///
/// ═══ 与 <see cref="PlayerAnimManagerSO"/>（旧 PlayerAniLibrary）的关系 ═══
///   · 本 SO 取代 PlayerAnimManagerSO 的职责。
///   · L5 落地前两者并存：新角色用 Profile，旧角色仍可读 AniLibrary（迁移期）。
///   · L5 落地后 AniLibrary 加 [Obsolete]，下一切片删除。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Locomotion/Locomotion Profile", fileName = "LocomotionProfile_")]
public class LocomotionProfile : ScriptableObject
{
    [Header("State Registry — 该角色拥有哪些 Locomotion 状态能力")]
    [SerializeField, Tooltip("勾选 = 角色拥有该状态；未勾选 = Resolver 走 LocomotionStateBinding.FallbackState 一级降级。\n基础类（Idle/Walk/AirJumpLoop/Dead）建议恒勾选。")]
    private LocomotionStateFlag enabledStates =
        LocomotionStateFlag.Idle |
        LocomotionStateFlag.Walk |
        LocomotionStateFlag.AirJumpLoop |
        LocomotionStateFlag.Dead;

    [Header("Feature Flags — 角色级模式开关（与 State 正交）")]
    [SerializeField, Tooltip("锁定 / 冲刺 / 潜行等模式；可与 State 自由组合。")]
    private LocomotionFeatureFlags features = LocomotionFeatureFlags.None;

    [Header("State Bindings — 每个状态对应的 Action/Clip + 降级链")]
    [SerializeField, Tooltip("Inspector 顺序与 Enabled States 菜单一致（Auto Fix / Sync 会重排）；运行时仍按 State 线性查找。")]
    private LocomotionStateBinding[] bindings;

    [Header("Tuning — 速度系数 / 加速度 / 跳跃 / 转身阈值")]
    [SerializeField, Tooltip("引用一份 LocomotionTuningSO；不同角色可共享同一份调优。")]
    private LocomotionTuningSO tuning;

    // ── 公有只读暴露 ──
    public LocomotionStateFlag EnabledStates => enabledStates;
    public LocomotionFeatureFlags Features => features;
    public LocomotionStateBinding[] Bindings => bindings;
    public LocomotionTuningSO Tuning => tuning;

#if UNITY_EDITOR
    [SerializeField, Tooltip("Editor 校验摘要；由 LocomotionProfileEditor 刷新，运行时不读。")]
    private string validationSummary;

    /// <summary>Editor 校验缓存（Inspector 只读）。</summary>
    public string EditorValidationSummary => validationSummary;

    public LocomotionStateBinding[] EditorGetBindingsCopy() =>
        bindings != null ? (LocomotionStateBinding[])bindings.Clone() : Array.Empty<LocomotionStateBinding>();

    public void EditorSetBindings(LocomotionStateBinding[] value) => bindings = value;

    public void EditorSetValidationSummary(string value) => validationSummary = value;

    /// <summary>剥离 EnabledStates 中的废弃位（StrafeLeft/Right/BackWalk、TurnInPlace 等）。</summary>
    public bool EditorSanitizeEnabledStates()
    {
        var sanitized = LocomotionStateFlagExtensions.SanitizeEnabledStates(enabledStates);
        if (sanitized == enabledStates)
        {
            return false;
        }

        enabledStates = sanitized;
        return true;
    }
#endif

    /// <summary>是否注册了该状态（按 Flag 查询；Resolver 内部用）。</summary>
    public bool HasState(LocomotionStateFlag state)
    {
        return state.IsEnabledIn(enabledStates);
    }

    /// <summary>是否注册了该状态（按 Id 查询；外部调用首选）。</summary>
    public bool HasState(LocomotionStateId id)
    {
        return id != LocomotionStateId.None && id.ToFlag().IsEnabledIn(enabledStates);
    }

    /// <summary>
    /// 查询某状态的 Binding；未配置则返回 default（State=None）。
    /// O(N)，N 极小，调用方应缓存结果，不在 Hot Path 重复调。
    /// </summary>
    public LocomotionStateBinding GetBinding(LocomotionStateId id)
    {
        if (bindings == null)
        {
            return default;
        }

        for (var i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].State == id)
            {
                return bindings[i];
            }
        }

        return default;
    }

    /// <summary>Flag 版本（Resolver 习惯传 Flag 时的便捷重载）。</summary>
    public LocomotionStateBinding GetBinding(LocomotionStateFlag flag)
    {
        return GetBinding(flag.ToId());
    }

    /// <summary>
    /// 159.1 L1+：方向化 + 速度档查表（Strafe / Turn 专用；起停四态走单 key <see cref="GetBinding(LocomotionStateId)"/>）。
    /// <para>三轮匹配（命中即返回）：</para>
    /// <list type="number">
    /// <item><description>State + StrafeDirection + TurnDirection + RunRequirement 全精确</description></item>
    /// <item><description>State + 方向精确 + RunRequirement = Any（任意走/跑）</description></item>
    /// <item><description>State + StrafeDirection = None + TurnDirection = None + RunRequirement = Any（兜底默认）</description></item>
    /// </list>
    /// </summary>
    public LocomotionStateBinding GetBinding(
        LocomotionStateId state,
        StrafeDirection8 strafeDir,
        TurnDirection4 turnDir,
        bool wantsRun)
    {
        if (bindings == null) return default;

        // 第 1 轮：全精确
        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b.State == state
                && b.StrafeDirection == strafeDir
                && b.TurnDirection == turnDir
                && RunMatch(b.RunRequirement, wantsRun))
            {
                return b;
            }
        }

        // 第 2 轮：方向精确 + RunReq=Any
        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b.State == state
                && b.StrafeDirection == strafeDir
                && b.TurnDirection == turnDir
                && b.RunRequirement == LocomotionRunRequirement.Any)
            {
                return b;
            }
        }

        // 第 3 轮：State 兜底（方向 None + RunReq Any）
        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b.State == state
                && b.StrafeDirection == StrafeDirection8.None
                && b.TurnDirection == TurnDirection4.None
                && b.RunRequirement == LocomotionRunRequirement.Any)
            {
                return b;
            }
        }

        return default;
    }

    static bool RunMatch(LocomotionRunRequirement req, bool wantsRun)
    {
        switch (req)
        {
            case LocomotionRunRequirement.WalkOnly: return !wantsRun;
            case LocomotionRunRequirement.RunOnly:  return wantsRun;
            default:                                return true;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 158.2 L1：Inspector 编辑时把新增 Binding 的零值字段归一化为合理默认（Speed=1，TransitionDuration=0.08）。
    /// 仅 Editor 期生效，运行时不调用。
    /// </summary>
    void OnValidate()
    {
        EditorSanitizeEnabledStates();

        // validationSummary 由 LocomotionProfileEditor 刷新；此处仅归一化 Binding 默认值。
        if (bindings == null) return;
        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
#pragma warning disable CS0618
            var changed = b.StripLegacyAssetRefsWhenLocomotionActionSet();

            if (b.Speed < 0.001f)
            {
                b.Speed = 1f;
                changed = true;
            }

            if (b.TransitionDuration < 0.0001f)
            {
                b.TransitionDuration = 0.08f;
                changed = true;
            }

            if (changed)
            {
                bindings[i] = b;
            }
#pragma warning restore CS0618
        }
    }
#endif
}

/// <summary>
/// 单个 Locomotion 状态的绑定（State → 路径 / 资源 / 降级目标）。
///
/// ═══ 164.1 / 227.5.1 单数据源 ═══
///   · <see cref="LocomotionAction"/> 权威；State 决定连续槽/离散段，Action flag 决定是否接管连续槽。
///   · Obsolete 字段（DiscreteAction / ContinuousClip 等）仅只读回落，勿再写入。
///   · 二者均空 —— 二级降级（表现层）：Anim 层回退到 Idle 的 Clip。
///
/// ═══ Fallback 链 ═══
///   · <see cref="FallbackState"/> 仅用于"逻辑层降级"：当本状态未在 Profile 注册时，
///     Resolver 沿 Fallback 链回退（典型链：TurnInPlaceDirected → Idle；WalkEnd → Idle）。
///   · 表现层降级（Clip 缺失）由 Anim 层处理，不在本字段表达。
/// </summary>
[Serializable]
public struct LocomotionStateBinding
{
    [Tooltip("本绑定对应的状态（单选）。")]
    public LocomotionStateId State;

    [Tooltip("逻辑层降级目标 —— 当本状态未在 Profile.EnabledStates 注册时，Resolver 沿此链回退。\n建议链：WalkEnd→Idle、TurnInPlaceDirected→Idle、JumpStart→Air、JumpLand→Idle。")]
    public LocomotionStateId FallbackState;

    [Tooltip("164.1 / 227.5.1 统一 Locomotion Action 入口。State 决定连续槽/离散段；连续槽中的 Action 必须勾选 Is Continuous 才接管表现。")]
    public ActionDataSO LocomotionAction;

    [Obsolete("164.1 L8：改用 LocomotionAction。运行时仅作只读回落，勿再写入。")]
    [Tooltip("已废弃 —— 请改 LocomotionAction。")]
    public ActionDataSO DiscreteAction;

    [Obsolete("164.1 L8：包成 ActionDataSO 并设 IsContinuousLocomotion。运行时仅作只读回落。")]
    [Tooltip("已废弃 —— 请改 LocomotionAction（Continuous）。")]
    public AnimationClip ContinuousClip;

    [Obsolete("164.1 L8：改读 ActionDataSO.CrossfadeTime。")]
    [Range(0f, 1f)] public float TransitionDuration;

    [Obsolete("164.1 L8：改读 ActionDataSO.AnimSpeed。")]
    [Range(0.1f, 20f)] public float Speed;

    [Obsolete("164.1 L8：改读 ActionDataSO.UseClipRootMotion / Locomotion 行为字段。")]
    public bool UseRootMotion;

    [Obsolete("164.1 L8：改读 ActionDataSO.CanRotateDuringLocomotion。")]
    public bool CanRotateDuring;

    [Obsolete("164.1 L8：改读 ActionDataSO.CanMoveDuringLocomotion。")]
    public bool CanMoveDuring;

    [Obsolete("164.1 L8：改放 Action SO 或 Tuning。")]
    [Min(0f)] public float ReferenceLocomotionSpeed;

    // ═══ 159.1 L1+：方向化 + 速度档字段（仅 StrafeLocomotion / TurnInPlaceDirected 有意义） ═══

    [Tooltip("【仅 State=StrafeLocomotion】8 向锁定方向匹配键。None = 默认兜底 Binding。")]
    public StrafeDirection8 StrafeDirection;

    [Tooltip("【仅 State=TurnInPlaceDirected】4 向转身匹配键。None = 默认兜底 Binding。")]
    public TurnDirection4 TurnDirection;

    [Tooltip("Walk/Run 速度档过滤（主要用于 StrafeLocomotion 同方向区分走/跑两条 Binding）。\nAny = 不区分；WalkOnly = 仅 !WantsRun；RunOnly = 仅 WantsRun。\n起停四态 (WalkEnd/RunEnd/WalkStart/RunStart) 已通过独立 State 区分，本字段在那些 State 上保持 Any 即可。")]
    public LocomotionRunRequirement RunRequirement;
}
