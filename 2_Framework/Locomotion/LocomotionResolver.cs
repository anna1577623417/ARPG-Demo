using UnityEngine;

/// <summary>
/// Locomotion 决策解析器（158.2 §6.1）—— 纯函数，零状态。
///
/// ═══ 职责 ═══
///   输入：玩家当前 LocomotionIntent（想做什么）+ LocomotionContext（环境）+ LocomotionProfile（角色能力）
///   输出：LocomotionDecision（应该走哪个 State / 走 Discrete Action 还是 Continuous Clip / 谁主导控制权）
///
/// ═══ 两级降级 ═══
///   · 一级降级（逻辑层，本 Resolver 负责）：
///     RequestedState 未在 Profile.EnabledStates 注册 → 沿 Binding.FallbackState 链回退，直至 Idle。
///   · 二级降级（表现层，<see cref="PlayerAnimController"/> 负责）：
///     Binding.DiscreteAction / ContinuousClip 均为空 → Anim 层回退到 Idle 的 Clip。
///   · 本 Resolver <strong>不</strong>触及表现层降级 —— 关注点分离。
///
/// ═══ 类型契约（158.2 § Q1 修正）═══
///   · "当前是哪个状态" 一律用 <see cref="LocomotionStateId"/>（单值 byte 枚举）。
///   · "拥有哪些状态能力" 用 <see cref="LocomotionStateFlag"/>（[Flags] int 集合）。
///   · 两者通过 <see cref="LocomotionStateIdExtensions.ToFlag"/> / <see cref="LocomotionStateIdExtensions.ToId"/> 转换。
/// </summary>
public static class LocomotionResolver
{
    /// <summary>主入口：根据意图 + 上下文 + Profile 产出决策。0-GC。</summary>
    public static LocomotionDecision Resolve(
        in LocomotionIntent intent,
        in LocomotionContext ctx,
        LocomotionProfile profile)
    {
        // Profile 缺失 —— 安全降级到 Idle（与"没有 Profile 等同于只注册基础 4 类"语义一致）。
        if (profile == null)
        {
            return new LocomotionDecision
            {
                ResolvedState = intent.RequestedState == LocomotionStateId.None
                    ? LocomotionStateId.Idle
                    : intent.RequestedState,
                DiscreteAction = null,
                ContinuousClip = null,
                ControlOwnerHint = ControlOwner.Locomotion,
                DowngradedFromLogicLayer = false,
            };
        }

        // ─── 一级降级（逻辑层）─── 沿 Fallback 链回退，直至命中 EnabledStates 或到达 Idle。
        var state = intent.RequestedState == LocomotionStateId.None
            ? LocomotionStateId.Idle
            : intent.RequestedState;

        var downgraded = false;
        var safetyHop = 0;
        while (!profile.HasState(state) && state != LocomotionStateId.Idle && safetyHop++ < 8)
        {
            var binding = profile.GetBinding(state);
            var next = binding.FallbackState == LocomotionStateId.None
                ? LocomotionStateId.Idle
                : binding.FallbackState;

            if (next == state)
            {
                state = LocomotionStateId.Idle;
                break;
            }

            state = next;
            downgraded = true;
        }

        // 取最终绑定（命中或 Idle 兜底）。
        // 159.1 L1+：方向化 State 走二维查表（StrafeLocomotion / TurnInPlaceDirected）；其余 State 走单 key。
        LocomotionStateBinding resolvedBinding;
        if (state == LocomotionStateId.StrafeLocomotion || state == LocomotionStateId.TurnInPlaceDirected)
        {
            resolvedBinding = profile.GetBinding(state, intent.StrafeDirection, intent.TurnDirection, intent.WantsRun);
        }
        else
        {
            resolvedBinding = profile.GetBinding(state);
        }

        // 164.1：统一 Action 解析 —— IsContinuousLocomotion → Clip 路径；否则离散 Action。
        var locomotionAction = resolvedBinding.ResolveLocomotionAction();
        ActionDataSO discreteAction = null;
        AnimationClip continuousClip = null;
        var transitionDuration = 0.08f;
        var clipSpeed = 1f;
        var isContinuous = false;
        ControlOwner ownerHint;

        if (locomotionAction != null)
        {
            if (locomotionAction.IsContinuousLocomotion)
            {
                isContinuous = true;
                continuousClip = locomotionAction.MainClip;
                transitionDuration = locomotionAction.CrossfadeTime;
                clipSpeed = locomotionAction.AnimSpeed > 0.001f ? locomotionAction.AnimSpeed : 1f;
                ownerHint = ControlOwner.Locomotion;
            }
            else
            {
                discreteAction = locomotionAction;
                ownerHint = ControlOwner.Action;
            }
        }
        else if (resolvedBinding.TryGetContinuousPresentation(
                     out continuousClip,
                     out transitionDuration,
                     out clipSpeed,
                     out _,
                     out _))
        {
            isContinuous = true;
            ownerHint = ControlOwner.Locomotion;
        }
        else
        {
            ownerHint = ControlOwner.Locomotion;
        }

        return new LocomotionDecision
        {
            ResolvedState = state,
            LocomotionAction = locomotionAction,
            DiscreteAction = discreteAction,
            IsContinuousLocomotion = isContinuous,
            ContinuousClip = continuousClip,
            TransitionDuration = transitionDuration,
            ClipSpeed = clipSpeed,
            ControlOwnerHint = ownerHint,
            DowngradedFromLogicLayer = downgraded,
        };
    }
}

/// <summary>
/// Locomotion 输入意图（158.2 §6.1）—— 玩家"想做什么"的离散表达。
/// </summary>
public readonly struct LocomotionIntent
{
    /// <summary>玩家本帧想进入的状态（Idle/Walk/Run/WalkEnd/RunStart/JumpStart...）。转身不在此表达。</summary>
    public readonly LocomotionStateId RequestedState;

    /// <summary>原始输入向量（世界空间，水平平面）。</summary>
    public readonly Vector3 RawInput;

    /// <summary>是否按住 Run。</summary>
    public readonly bool WantsRun;

    /// <summary>转身意图角度差（度，无符号）—— 诊断用；转身表现由 <see cref="TurnResolver"/> 单轨驱动。</summary>
    public readonly float TurnAngleDeg;

    /// <summary>159.1 L1+：8 向锁定方向（仅 RequestedState=StrafeLocomotion 有意义）。</summary>
    public readonly StrafeDirection8 StrafeDirection;

    /// <summary>159.1 L1+：4 向转身（仅 RequestedState=TurnInPlaceDirected 有意义）。</summary>
    public readonly TurnDirection4 TurnDirection;

    public LocomotionIntent(
        LocomotionStateId requestedState,
        Vector3 rawInput,
        bool wantsRun,
        float turnAngleDeg,
        StrafeDirection8 strafeDirection = StrafeDirection8.None,
        TurnDirection4 turnDirection = TurnDirection4.None)
    {
        RequestedState = requestedState;
        RawInput = rawInput;
        WantsRun = wantsRun;
        TurnAngleDeg = turnAngleDeg;
        StrafeDirection = strafeDirection;
        TurnDirection = turnDirection;
    }
}

/// <summary>Locomotion 求值上下文 —— 环境/能力快照。</summary>
public readonly struct LocomotionContext
{
    public readonly bool IsGrounded;
    public readonly bool IsLockedOn;
    public readonly float PlanarSpeed;

    public LocomotionContext(bool isGrounded, bool isLockedOn, float planarSpeed)
    {
        IsGrounded = isGrounded;
        IsLockedOn = isLockedOn;
        PlanarSpeed = planarSpeed;
    }
}

/// <summary>Resolver 输出 —— 调用方据此决定走 Action 路径还是 Clip 路径。</summary>
public struct LocomotionDecision
{
    /// <summary>最终解析到的状态（经一级降级后）。</summary>
    public LocomotionStateId ResolvedState;

    /// <summary>164.1：Binding 解析到的统一 Locomotion Action（可为连续或离散）。</summary>
    public ActionDataSO LocomotionAction;

    /// <summary>离散 Action 路径（非空时调用方应 ArmPendingAction + Change&lt;ActionState&gt;）。</summary>
    public ActionDataSO DiscreteAction;

    /// <summary>连续 Locomotion 路径（由 LocomotionAction.MainClip 或 Binding.ContinuousClip 填充）。</summary>
    public bool IsContinuousLocomotion;

    /// <summary>连续 Clip 路径（非空时由 PlayerAnimController CrossFade）。</summary>
    public AnimationClip ContinuousClip;

    /// <summary>动画过渡时长（秒，连续 Clip 路径专用）。</summary>
    public float TransitionDuration;

    /// <summary>动画播放速度倍率。</summary>
    public float ClipSpeed;

    /// <summary>本决策对应的 ControlOwner 提示（仅观测，调用方不据此切控制权）。</summary>
    public ControlOwner ControlOwnerHint;

    /// <summary>是否经过了一级降级（用于 DryRun 阶段比对调试）。</summary>
    public bool DowngradedFromLogicLayer;
}

/// <summary>
/// 159.1 L2+：Resolver 连续 Clip 路径的表现层快照（由 <see cref="PlayerLocomotionState"/> 写入，<see cref="PlayerAnimController"/> 读取）。
/// </summary>
public struct LocomotionPresentationSnapshot
{
    public LocomotionStateId ResolvedState;
    public StrafeDirection8 StrafeDirection;
    public AnimationClip ContinuousClip;
    public float TransitionDuration;
    public float ClipSpeed;
    public float ReferenceLocomotionSpeed;

    /// <summary>164.1：连续 Locomotion Action（Anim 层可读 CanRotate/CanMove）。</summary>
    public ActionDataSO ContinuousAction;

    public static LocomotionPresentationSnapshot FromDecision(in LocomotionDecision decision, in LocomotionIntent intent)
    {
        return new LocomotionPresentationSnapshot
        {
            ResolvedState = decision.ResolvedState,
            StrafeDirection = intent.StrafeDirection,
            ContinuousClip = decision.ContinuousClip,
            ContinuousAction = decision.IsContinuousLocomotion ? decision.LocomotionAction : null,
            TransitionDuration = decision.TransitionDuration,
            ClipSpeed = decision.ClipSpeed,
            ReferenceLocomotionSpeed = 0f,
        };
    }

    public static LocomotionPresentationSnapshot FromBinding(
        LocomotionStateId state,
        StrafeDirection8 strafeDir,
        in LocomotionStateBinding binding)
    {
        if (!binding.TryGetContinuousPresentation(
                out var clip,
                out var transition,
                out var speed,
                out var refSpeed,
                out var action))
        {
            return new LocomotionPresentationSnapshot
            {
                ResolvedState = state,
                StrafeDirection = strafeDir,
            };
        }

        return new LocomotionPresentationSnapshot
        {
            ResolvedState = state,
            StrafeDirection = strafeDir,
            ContinuousClip = clip,
            ContinuousAction = action,
            TransitionDuration = transition,
            ClipSpeed = speed,
            ReferenceLocomotionSpeed = refSpeed,
        };
    }
}
