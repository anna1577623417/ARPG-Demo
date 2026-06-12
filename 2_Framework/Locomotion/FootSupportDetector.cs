using UnityEngine;

/// <summary>
/// 164.1 L10 设施：急停相位脚支撑检测（默认未通电，需 Tuning.EnableFootPhasedStopVariants）。
/// </summary>
public enum FootSupportPhase
{
    Unknown = 0,
    Left = 1,
    Right = 2,
}

public static class FootSupportDetector
{
    /// <summary>
    /// 从 Animator 人形骨骼推断当前支撑脚；无 Animator 或无法判定时返回 Unknown。
    /// </summary>
    public static FootSupportPhase Detect(Animator animator)
    {
        if (animator == null || !animator.isHuman)
        {
            return FootSupportPhase.Unknown;
        }

        var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (left == null || right == null)
        {
            return FootSupportPhase.Unknown;
        }

        const float tolerance = 0.03f;
        var dy = left.position.y - right.position.y;
        if (dy < -tolerance)
        {
            return FootSupportPhase.Left;
        }

        if (dy > tolerance)
        {
            return FootSupportPhase.Right;
        }

        return FootSupportPhase.Unknown;
    }

    /// <summary>按支撑相位选 WalkEnd/RunEnd 变体 Clip；无变体时回落 MainClip。</summary>
    public static AnimationClip ResolveStopVariantClip(ActionDataSO action, FootSupportPhase phase)
    {
        if (action == null)
        {
            return null;
        }

        switch (phase)
        {
            case FootSupportPhase.Left when action.LeftFootSupportClip != null:
                return action.LeftFootSupportClip;
            case FootSupportPhase.Right when action.RightFootSupportClip != null:
                return action.RightFootSupportClip;
            default:
                return action.MainClip;
        }
    }
}
