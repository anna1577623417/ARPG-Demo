using UnityEngine;

/// <summary>
/// ARPG 相机死区代理（动态绑定版）。
///
/// ═══ 架构定位 ═══
///
///   Input → ActionController → MotionController (KCC 物理位移)
///        → <b>CameraDeadzoneProxy</b>（死区过滤 · 旋转镜像）
///        → Cinemachine VirtualCamera.Follow / LookAt
///
/// ═══ 生命周期 ═══
///
///   1. 场景根目录放置空物体 "CameraTargetProxy"（<b>不作为 Player 子物体</b>）。
///   2. 挂载本脚本；Inspector 无需手动填目标——目标由代码动态绑定。
///   3. PlayerManager.BindActionCamera → ActionCameraController.RebindFollowAndLookAt
///      → 本脚本 Rebind(follow, lookAt) —— 每次角色生成/切换时自动调用。
///   4. Cinemachine VirtualCamera.Follow = proxy.transform
///      Cinemachine VirtualCamera.LookAt = proxy.LookAtTransform
///
/// ═══ 执行序 ═══
///
///   [DefaultExecutionOrder(10)] 确保晚于 CameraController.LateUpdate()，
///   从而读到 ActionCameraController.UpdateCamera() 刚写入的最新 followTarget.rotation。
///
/// ═══ 死区逻辑 ═══
///
///   · 位置（XZ / Y 分轴）：只在目标超出死区半径时才牵引代理，吸收 KCC 墙角震荡与楼梯颠簸。
///   · 旋转：每帧直接镜像 followTarget.rotation，不加任何过滤（Cinemachine 需要实时旋转驱动轨道）。
///   · LookAt 代理：XZ 直接复用 Follow 代理的已过滤坐标，Y 轴独立 SmoothDamp 追踪目标高度。
///     这样 LookAt 的 XZ 与 Follow 完全同步（相机不会因水平抖动而旋转），
///     而高度变化（上坡 / 楼梯）仍能平滑响应。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10)]
[AddComponentMenu("GameMain/Camera/Camera Deadzone Proxy")]
public class CameraDeadzoneProxy : MonoBehaviour
{
    [Header("死区参数 (Deadzone)")]
    [Tooltip("水平死区半径：吸收墙角打乒乓、攻击微小滑步。ARPG 推荐 0.10 ~ 0.20。")]
    public float radiusXZ = 0.15f;

    [Tooltip("垂直死区半径：吸收楼梯 / 斜坡颠簸。推荐 0.25 ~ 0.40。")]
    public float radiusY = 0.3f;

    [Header("平滑追赶 (SmoothDamp)")]
    [Tooltip("越小越跟手，越大越有延迟感。ARPG 推荐 0.05 ~ 0.10。")]
    public float smoothTime = 0.08f;

    [Tooltip("Y 轴平滑时间相对 XZ 的倍率，> 1 让垂直更稳。")]
    public float verticalSmoothMultiplier = 1.5f;

    // ─── 运行时绑定（由 ActionCameraController.RebindFollowAndLookAt 注入）───
    // [SerializeField] 让这两个字段在 Inspector 里可见，方便运行时确认绑定是否成功。

    [SerializeField] private Transform _followTarget;
    [SerializeField] private Transform _lookAtTarget;

    // LookAt 子代理由 Awake 自动创建，不序列化（每次运行重建）。
    private Transform _lookAtProxy;

    // ─── 平滑计算状态 ───

    private Vector3 _currentProxyPos;
    private Vector2 _velocityXZ;
    private float _velocityY;

    // LookAt 代理的 Y 轴独立平滑状态
    private float _lookAtProxyY;
    private float _lookAtVelocityY;

    // ─── 公开访问 ───

    /// <summary>
    /// 供 Cinemachine VirtualCamera.LookAt 使用的注视点 Transform（不经过死区，保持精准）。
    /// 在 <see cref="Rebind"/> 之前返回代理自身 transform。
    /// </summary>
    public Transform LookAtTransform => _lookAtProxy != null ? _lookAtProxy : transform;

    // ─── 生命周期 ───

    private void Awake()
    {
        // 创建 LookAt 子代理（不可见，不挂任何 Renderer）
        var go = new GameObject("_LookAtProxy") { hideFlags = HideFlags.HideInHierarchy };
        go.transform.SetParent(transform, false);
        _lookAtProxy = go.transform;
    }

    // ─── 公开 API ───

    /// <summary>
    /// 动态绑定跟随与注视目标（由 ActionCameraController 在角色生成 / 切换时调用）。
    /// 调用后立即 Snap，防止新角色位置与旧代理位置产生长距离平滑拉扯。
    /// </summary>
    /// <param name="follow">相机轨道旋转锚点（PlayerCameraAnchor.FollowTarget）。</param>
    /// <param name="lookAt">注视锚点；为 null 时与 follow 相同。</param>
    public void Rebind(Transform follow, Transform lookAt = null)
    {
        if (follow == null)
        {
            Debug.LogError("[CameraDeadzoneProxy] Rebind: follow 不能为 null。", this);
            return;
        }

        _followTarget = follow;
        _lookAtTarget = lookAt != null ? lookAt : follow;
        SnapToTarget();
    }

    /// <summary>
    /// 立即将代理对齐到目标（传送 / 复活 / 场景切换时调用，避免相机长距离滑动）。
    /// </summary>
    public void SnapToTarget()
    {
        if (_followTarget == null) return;

        _currentProxyPos = _followTarget.position;
        _velocityXZ = Vector2.zero;
        _velocityY = 0f;

        transform.SetPositionAndRotation(_currentProxyPos, _followTarget.rotation);

        if (_lookAtProxy != null && _lookAtTarget != null)
        {
            _lookAtProxyY = _lookAtTarget.position.y;
            _lookAtVelocityY = 0f;
            _lookAtProxy.position = new Vector3(_currentProxyPos.x, _lookAtProxyY, _currentProxyPos.z);
        }
    }

    // ─── 每帧更新（晚于 CameraController.LateUpdate，由 [DefaultExecutionOrder(10)] 保证）───

    private void LateUpdate()
    {
        if (_followTarget == null) return;

        // ── 1. 旋转：仅镜像 yaw（水平偏航），不传 pitch / roll。 ─────────────────
        // 角色被边缘脱困位移、动画 root motion 等场景下 followTarget 的 pitch/roll 会瞬时跳变，
        // 直接全量镜像会让 3rdPersonFollow 轨道俯仰被拉扯，相机插入角色体内。
        var fwdEuler = _followTarget.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, fwdEuler.y, 0f);

        // ── 2. XZ 死区牵引 ────────────────────────────────────────────────────────
        Vector3 targetPos = _followTarget.position;
        Vector3 desiredProxyPos = _currentProxyPos;

        Vector2 offsetXZ = new Vector2(
            targetPos.x - _currentProxyPos.x,
            targetPos.z - _currentProxyPos.z);
        float distXZ = offsetXZ.magnitude;

        if (distXZ > radiusXZ)
        {
            Vector2 pull = offsetXZ.normalized * (distXZ - radiusXZ);
            desiredProxyPos.x += pull.x;
            desiredProxyPos.z += pull.y;
        }

        // ── 3. Y 死区牵引（防楼梯颠簸核心）─────────────────────────────────────
        float offsetY = targetPos.y - _currentProxyPos.y;
        float distY = Mathf.Abs(offsetY);

        if (distY > radiusY)
        {
            desiredProxyPos.y += Mathf.Sign(offsetY) * (distY - radiusY);
        }

        // ── 4. SmoothDamp 消除突破死区瞬间的顿挫 ─────────────────────────────────
        Vector2 curXZ = new Vector2(_currentProxyPos.x, _currentProxyPos.z);
        Vector2 dstXZ = new Vector2(desiredProxyPos.x, desiredProxyPos.z);

        Vector2 smoothedXZ = Vector2.SmoothDamp(curXZ, dstXZ, ref _velocityXZ, smoothTime);
        float smoothedY = Mathf.SmoothDamp(
            _currentProxyPos.y, desiredProxyPos.y,
            ref _velocityY, smoothTime * Mathf.Max(0.01f, verticalSmoothMultiplier));

        _currentProxyPos = new Vector3(smoothedXZ.x, smoothedY, smoothedXZ.y);
        transform.position = _currentProxyPos;

        // ── 5. LookAt 子代理：XZ 复用已过滤坐标，Y 走与 Follow 同等的死区逻辑 ─────
        // 旧版仅 SmoothDamp，瞬时大幅 Y 跳（如 depenetration 把角色顶起 0.3m）会直接被
        // 镜头看到 → 镜头插进角色身体。新版先过死区再 SmoothDamp，与 Follow Y 保持一致。
        if (_lookAtProxy != null && _lookAtTarget != null)
        {
            float lookTargetY = _lookAtTarget.position.y;
            float dy = lookTargetY - _lookAtProxyY;
            float adY = Mathf.Abs(dy);
            float lookDesiredY = _lookAtProxyY;
            if (adY > radiusY)
            {
                lookDesiredY += Mathf.Sign(dy) * (adY - radiusY);
            }

            _lookAtProxyY = Mathf.SmoothDamp(
                _lookAtProxyY,
                lookDesiredY,
                ref _lookAtVelocityY,
                smoothTime * Mathf.Max(0.01f, verticalSmoothMultiplier));

            _lookAtProxy.position = new Vector3(_currentProxyPos.x, _lookAtProxyY, _currentProxyPos.z);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 水平死区圆
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.15f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, radiusXZ);
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.8f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, radiusXZ);

        // 垂直死区线段
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position + Vector3.up * radiusY,
                        transform.position + Vector3.down * radiusY);

        // Proxy → FollowTarget 连线（可视化拉扯距离）
        if (_followTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _followTarget.position);
            Gizmos.DrawSphere(_followTarget.position, 0.05f);
        }
    }
#endif
}
