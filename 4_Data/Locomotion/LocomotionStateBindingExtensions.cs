using UnityEngine;

/// <summary>
/// 164.1：LocomotionStateBinding 统一 Action 解析（双源兼容期）。
/// 227.5.1：连续表现采用双重门禁：State 声明该槽位是否允许连续表现，
/// <see cref="ActionDataSO.IsContinuousLocomotion"/> 声明该 Action 是否接管该连续槽位。
/// </summary>
public static class LocomotionStateBindingExtensions
{
    /// <summary>
    /// 单轨读取：LocomotionAction 权威；未填时只读回落 Obsolete 的 DiscreteAction（不迁移资产）。
    /// </summary>
    public static ActionDataSO ResolveLocomotionAction(in this LocomotionStateBinding binding)
    {
        if (binding.LocomotionAction != null)
        {
            return binding.LocomotionAction;
        }

#pragma warning disable CS0618
        return binding.DiscreteAction;
#pragma warning restore CS0618
    }

    public static bool HasLocomotionAction(in this LocomotionStateBinding binding)
    {
        if (binding.LocomotionAction != null)
        {
            return true;
        }

#pragma warning disable CS0618
        return binding.DiscreteAction != null || binding.ContinuousClip != null;
#pragma warning restore CS0618
    }

    /// <summary>
    /// 连续 Locomotion 表现：State 必须是连续槽；Action 只有显式勾选 IsContinuousLocomotion 才能接管。
    /// 未接管时只读回落 Obsolete ContinuousClip，最终表现层仍可回落 AnimLibrary。
    /// </summary>
    public static bool TryGetContinuousPresentation(
        in this LocomotionStateBinding binding,
        out AnimationClip clip,
        out float transitionDuration,
        out float clipSpeed,
        out float referenceLocomotionSpeed,
        out ActionDataSO continuousAction)
    {
        continuousAction = null;
        if (!binding.State.IsContinuous())
        {
            clip = null;
            transitionDuration = 0.08f;
            clipSpeed = 1f;
            referenceLocomotionSpeed = 0f;
            return false;
        }

        var action = binding.ResolveLocomotionAction();
        if (action != null
            && action.MainClip != null
            && (action.IsContinuousLocomotion || AllowsFinitePresentationAction(binding.State)))
        {
            continuousAction = action;
            clip = action.MainClip;
            transitionDuration = action.CrossfadeTime;
            clipSpeed = action.AnimSpeed > 0.001f ? action.AnimSpeed : 1f;
            referenceLocomotionSpeed = 0f;
            return true;
        }

#pragma warning disable CS0618
        if (binding.ContinuousClip != null)
        {
            clip = binding.ContinuousClip;
            transitionDuration = binding.TransitionDuration > 0.0001f ? binding.TransitionDuration : 0.08f;
            clipSpeed = binding.Speed > 0.001f ? binding.Speed : 1f;
            referenceLocomotionSpeed = binding.ReferenceLocomotionSpeed;
            return true;
        }
#pragma warning restore CS0618

        clip = null;
        transitionDuration = 0.08f;
        clipSpeed = 1f;
        referenceLocomotionSpeed = 0f;
        return false;
    }

    /// <summary>
    /// TurnInPlaceDirected 当前仍走表现层直接播放，但其切片是有限 one-shot，不应被 Is Continuous 强制成 Loop。
    /// 这是现有 State 分类的兼容边界；后续 Presentation Graph 应显式拆成 FinitePresentationPolicy。
    /// </summary>
    public static bool AllowsFinitePresentationAction(LocomotionStateId state) =>
        state == LocomotionStateId.TurnInPlaceDirected;

    /// <summary>表现过渡时长：Action.CrossfadeTime 或 Obsolete Binding 回落。</summary>
    public static float ResolvePresentationTransition(in this LocomotionStateBinding binding)
    {
        return binding.TryGetContinuousPresentation(
            out _,
            out var transition,
            out _,
            out _,
            out _)
            ? transition
            : 0.08f;
    }

    /// <summary>
    /// 164.1 单数据源：已填 <see cref="LocomotionStateBinding.LocomotionAction"/> 时清 Obsolete 资产引用，避免 Inspector 双写警告。
    /// </summary>
    public static bool StripLegacyAssetRefsWhenLocomotionActionSet(ref this LocomotionStateBinding binding)
    {
        if (binding.LocomotionAction == null)
        {
            return false;
        }

        var changed = false;
#pragma warning disable CS0618
        if (binding.DiscreteAction != null)
        {
            binding.DiscreteAction = null;
            changed = true;
        }

        if (binding.ContinuousClip != null)
        {
            binding.ContinuousClip = null;
            changed = true;
        }
#pragma warning restore CS0618

        return changed;
    }
}
