using UnityEngine;

/// <summary>
/// 178.1 W4 — PhaseMatch 足相位辅助。
/// <para>Locomotion Walk↔Run 切换时把目标 Clip 起点对齐到当前足相位，避免"刹车感"。</para>
/// <para>项目接入：PlayerAnimController 在 PlayWithPhaseMatch 中调用 EstimateTargetStart。</para>
/// </summary>
public static class FootPhaseHelper
{
    /// <summary>
    /// 由 Animator 当前 normalizedTime 估算足相位（0~1 周期内的相对位置）。
    /// 简化模型：假设走/跑 Clip 一个周期对应 1.0 normalizedTime，0/0.5 是左/右支撑切换点。
    /// </summary>
    public static float EstimateCurrentFootPhase(float normalizedTime)
    {
        var t = Mathf.Repeat(normalizedTime, 1f);
        return t;
    }

    /// <summary>
    /// 178.1 §6.1 — 把目标 Clip 起点对齐到当前足相位，返回 CrossFadeInFixedTime 用的 normalizedTimeOffset。
    /// <para>若数据库已提供 toFootPhase 则按差值对齐；否则返回 0（普通 CrossFade）。</para>
    /// </summary>
    public static float AlignToFootPhase(float currentFootPhase, float toClipDefaultFootPhase)
    {
        if (toClipDefaultFootPhase < 0f) return 0f;
        var offset = currentFootPhase - toClipDefaultFootPhase;
        return Mathf.Repeat(offset, 1f);
    }
}
