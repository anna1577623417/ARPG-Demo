using UnityEngine;

/// <summary>
/// 5_Presentation 层 — 逻辑/视觉位置解耦器（Visual Proxy）。
///
/// 商业级 ARPG（《战神》《原神》）的相机绝不直接采样物理位置：
/// PhysX 离散解算在钝角接缝、墙根、低台阶等场景难免产生 0.0001 量级的微动，
/// 直接挂 Camera 会被无限放大成肉眼可见的高频抖动。
///
/// 本组件挂在 Visual Root（包含模型 / 相机 Anchor）上，每帧用极短时间常数的
/// SmoothDamp 平滑追踪 <see cref="logicalRoot"/>（Player Logic Transform）：
///   • 平滑时间默认 0.02s — 足够过滤亚像素抖动，玩家不会感知到延迟；
///   • 同时支持 Yaw 平滑（用于解耦 KCC 旋转的微跳）；
///   • 大跨度位移（瞬移、翻滚、复活）会自动 Snap，不被平滑拖尾。
///
/// 架构约束：
///   • 仅依赖 Unity 原生 Transform（不引用 Gameplay 类型），符合 5_Presentation 层规范；
///   • 该 GameObject 必须脱离 logicalRoot 的层级（否则会被父变换抵消平滑效果）。
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class VisualInterpolator : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("【逻辑根 / Logical Root】KCC 求解输出位置的 Transform（通常是 Player GameObject）。\n" +
             "本组件每帧通过 SmoothDamp 追踪此 Transform，自身位置即视觉/相机锚点。\n" +
             "EN: The Transform driven by physics (KCC). This component smoothly follows it.")]
    [SerializeField] private Transform logicalRoot;

    [Header("Position smoothing / 位置平滑")]
    [Tooltip("【启用位置平滑 / Enable Position Smoothing】关闭则硬同步位置（用于对照测试）。\n" +
             "EN: Disable to hard-snap each frame (for A/B testing).")]
    [SerializeField] private bool enablePositionSmoothing = true;

    [Tooltip("【位置平滑时间（秒） / Position Smooth Time】SmoothDamp 时间常数。\n" +
             "增大（→ 0.08）：抖动更被吞没，但角色「拖影感」明显，相机有滞后；\n" +
             "减小（→ 0.005）：响应更快，但抖动过滤减弱；推荐 0.015 ~ 0.030。\n" +
             "EN: SmoothDamp time constant. Larger = smoother but laggier. Recommended 0.015–0.030.")]
    [Range(0.001f, 0.1f)]
    [SerializeField] private float positionSmoothTime = 0.02f;

    [Tooltip("【位置追赶最大速度（米/秒） / Max Catch-up Speed】SmoothDamp 速度上限；\n" +
             "增大（→ 100）：跨段位移追赶更快但可能拖尾；\n" +
             "减小（→ 20）：上限低，正常移动也会被截断（不推荐）。0 = Mathf.Infinity。\n" +
             "EN: SmoothDamp max speed cap. 0 = unlimited. Set lower only if you want a leashy follow feel.")]
    [SerializeField] private float maxCatchupSpeed;

    [Tooltip("【位移瞬移阈值（米） / Position Teleport Snap Threshold】\n" +
             "当 |logical - visual| 超过此阈值时强制 Snap（瞬移、翻滚、复活），不再平滑追赶。\n" +
             "增大（→ 5.0）：仅极远跨段触发 Snap；过大会让翻滚出现 0.5s 延迟。\n" +
             "减小（→ 0.5）：剧烈位移误判为瞬移，正常冲刺手感生硬。推荐 1.5 ~ 3.0。\n" +
             "EN: If logical-visual distance exceeds this, snap to the logical pos (for teleports/dodges). Recommended 1.5–3.0 m.")]
    [Range(0.2f, 10f)]
    [SerializeField] private float teleportSnapDistance = 2f;

    [Header("Rotation smoothing / 旋转平滑（可选）")]
    [Tooltip("【启用旋转平滑 / Enable Rotation Smoothing】打开后用 SLerp 平滑追踪 logicalRoot.rotation。\n" +
             "通常关闭（角色本身已有 RotateTowards），仅在视觉根脱离 Player 层级时启用。\n" +
             "EN: Off by default; enable only if the visual root is detached from the player and needs to mirror rotation.")]
    [SerializeField] private bool enableRotationSmoothing;

    [Tooltip("【旋转平滑速率（度/秒） / Rotation Smooth Rate】仅在启用旋转平滑时生效。\n" +
             "增大：旋转跟随更紧；减小：明显的旋转拖尾。EN: Only used when rotation smoothing is enabled.")]
    [Range(180f, 7200f)]
    [SerializeField] private float rotationSmoothDegPerSec = 1440f;

    [Header("Debug")]
    [Tooltip("【绘制调试 / Draw Debug】Scene 视图绘制黄色线段连接 logicalRoot 与本 Transform，\n" +
             "线越长 = 平滑滞后越大，可肉眼观察平滑量级。\n" +
             "EN: Draw a yellow line from logicalRoot to this transform; longer line = more smoothing lag.")]
    [SerializeField] private bool drawFollowLag = true;

    [Tooltip("【日志：瞬移 Snap 触发 / Log Teleport Snaps】超过 teleportSnapDistance 触发硬同步时输出一行日志。\n" +
             "EN: Log when a teleport snap is triggered.")]
    [SerializeField] private bool logTeleportSnaps;

    private Vector3 m_followVelocity;
    private bool m_initialized;

    public Transform LogicalRoot
    {
        get => logicalRoot;
        set
        {
            logicalRoot = value;
            ForceSnapToLogical();
        }
    }

    /// <summary>外部强制视觉立刻贴合逻辑（瞬移 / 关卡切换时使用）。</summary>
    public void ForceSnapToLogical()
    {
        if (logicalRoot == null) return;
        transform.position = logicalRoot.position;
        if (enableRotationSmoothing) transform.rotation = logicalRoot.rotation;
        m_followVelocity = Vector3.zero;
    }

    private void OnEnable()
    {
        if (logicalRoot != null && !m_initialized)
        {
            ForceSnapToLogical();
            m_initialized = true;
        }
    }

    private void LateUpdate()
    {
        if (logicalRoot == null) return;

        var target = logicalRoot.position;
        var current = transform.position;

        // 大跨度位移（瞬移 / 翻滚冲刺）→ 直接硬同步，避免 SmoothDamp 拖尾出几帧"残影"
        var deltaSq = (target - current).sqrMagnitude;
        if (deltaSq > teleportSnapDistance * teleportSnapDistance)
        {
            if (logTeleportSnaps)
            {
                Debug.Log($"[VisualInterpolator] Teleport snap | dist={Mathf.Sqrt(deltaSq):F2} m " +
                          $"> threshold={teleportSnapDistance:F2} m", this);
            }

            transform.position = target;
            m_followVelocity = Vector3.zero;
        }
        else if (enablePositionSmoothing && positionSmoothTime > 0.0001f)
        {
            var maxSpeed = maxCatchupSpeed > 0.001f ? maxCatchupSpeed : Mathf.Infinity;
            transform.position = Vector3.SmoothDamp(
                current,
                target,
                ref m_followVelocity,
                positionSmoothTime,
                maxSpeed,
                Time.deltaTime);
        }
        else
        {
            transform.position = target;
            m_followVelocity = Vector3.zero;
        }

        if (enableRotationSmoothing)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                logicalRoot.rotation,
                rotationSmoothDegPerSec * Time.deltaTime);
        }

        if (drawFollowLag)
        {
            Debug.DrawLine(target, transform.position,
                new Color(1f, 0.92f, 0.15f, 0.85f));
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (logicalRoot == null) return;
        Gizmos.color = new Color(1f, 0.92f, 0.15f, 0.55f);
        Gizmos.DrawWireSphere(logicalRoot.position, 0.12f);
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, 0.10f);
        Gizmos.DrawLine(logicalRoot.position, transform.position);

        // teleport snap 半径（红色虚线）
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.25f);
        Gizmos.DrawWireSphere(logicalRoot.position, teleportSnapDistance);
    }
#endif
}
