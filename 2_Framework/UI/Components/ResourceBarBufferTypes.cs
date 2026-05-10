using UnityEngine;

/// <summary>
/// 双层资源条：Buffer（Ghost）跟随主条（Fill）的策略。
/// </summary>
public enum ResourceBarBufferFollowStrategy
{
    /// <summary>
    /// Cur 变动后，在 <see cref="ResourceBarBufferConfigSO.bufferDelay"/> 这段时长内，
    /// Buffer 从当前位置按缓动曲线平滑插值到主条目标（总时长 = Delay，可调）。
    /// </summary>
    SmoothDuringDelay = 0,

    /// <summary>
    /// Cur 变动后 Buffer 先保持不动，Delay 结束后瞬时对齐主条（经典「先停再啪」）。
    /// </summary>
    SnapAfterDelay = 1,

    /// <summary>
    /// Delay 结束后按速度 <see cref="ResourceBarBufferConfigSO.moveTowardsSpeed"/> MoveTowards 追上（兼容旧版）。
    /// </summary>
    MoveTowardsAfterDelay = 2,
}

/// <summary>
/// 标准化缓动：输入 t∈[0,1]，输出 ∈[0,1]。
/// </summary>
public enum ResourceBarBufferEase
{
    Linear = 0,
    OutQuad = 1,
    InQuad = 2,
    InOutQuad = 3,
    OutCubic = 4,
    InCubic = 5,
    InOutCubic = 6,
}

/// <summary>无 DOTween 时的轻量 Ease 实现。</summary>
public static class ResourceBarBufferEasing
{
    public static float Evaluate(float t01, ResourceBarBufferEase ease)
    {
        var t = Mathf.Clamp01(t01);
        switch (ease)
        {
            case ResourceBarBufferEase.Linear:
                return t;
            case ResourceBarBufferEase.OutQuad:
                return 1f - (1f - t) * (1f - t);
            case ResourceBarBufferEase.InQuad:
                return t * t;
            case ResourceBarBufferEase.InOutQuad:
                return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
            case ResourceBarBufferEase.OutCubic:
                return 1f - Mathf.Pow(1f - t, 3f);
            case ResourceBarBufferEase.InCubic:
                return t * t * t;
            case ResourceBarBufferEase.InOutCubic:
                return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            default:
                return t;
        }
    }
}
