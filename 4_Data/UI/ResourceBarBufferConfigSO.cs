using UnityEngine;

/// <summary>
/// 资源条 Buffer（Ghost）跟随行为的数据驱动配置；可挂在 <see cref="ResourceBarView"/> 上覆盖 Inspector 默认值。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/UI/Resource Bar Buffer Config", fileName = "ResourceBarBufferConfig")]
public sealed class ResourceBarBufferConfigSO : ScriptableObject
{
    [Header("Strategy")]
    [Tooltip("SmoothDuringDelay：延迟时长内平滑插值到主条；SnapAfterDelay：延迟结束后瞬时对齐；MoveTowardsAfterDelay：延迟后按速度追上。")]
    public ResourceBarBufferFollowStrategy strategy = ResourceBarBufferFollowStrategy.MoveTowardsAfterDelay;

    [Header("Timing")]
    [Tooltip("延迟窗口（秒）。SmoothDuringDelay 下亦为平滑插值总时长。")]
    [Min(0f)]
    public float bufferDelay = 0.2f;

    [Tooltip("Drain 时若归一化变化量小于此值，不触发 Buffer 演出（直接对齐，避免抖动）。")]
    [Min(0f)]
    public float minChangeThreshold = 0.001f;

    [Header("Smooth During Delay")]
    public ResourceBarBufferEase interpolationEase = ResourceBarBufferEase.OutQuad;

    [Header("Move Towards After Delay")]
    [Tooltip("仅 MoveTowardsAfterDelay：延迟结束后每秒移动的归一化量。")]
    [Min(0.01f)]
    public float moveTowardsSpeed = 6f;

    [Header("Drain / Refill")]
    [Tooltip("仅 Drain 时走 Buffer 动画；Refill 时 Buffer 立即对齐主条（常见血条反馈）。")]
    public bool useBufferOnDrainOnly = true;
}
