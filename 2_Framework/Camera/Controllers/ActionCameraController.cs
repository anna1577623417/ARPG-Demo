using Cinemachine;
using UnityEngine;

/// <summary>
/// 动作模式相机控制器（类魂 / 第三人称跟随）。
/// 目标环境：Unity 2022 LTS，Cinemachine 2.x（<c>CinemachineVirtualCamera</c>），无条件编译分支。
///
/// ═══ 核心原理 ═══
///
/// followTarget 是挂在玩家 根节点 下的空物体（不是骨骼子物体）：
///   - 位置跟随玩家移动（因为是 Player 子物体）
///   - 旋转由本脚本驱动（鼠标/右摇杆 → yaw/pitch）
///   - VCam Follow = followTarget → Cinemachine3rdPersonFollow 自动计算偏移轨道
///
/// ❗ 不要把 followTarget 挂在骨骼下面，否则：
///   1. 动画每帧覆盖旋转 → 脚本写的旋转被冲掉
///   2. 跑步/攻击时骨骼晃动 → 相机也跟着晃（TPS 不需要这种抖动）
///
/// ═══ 搭建步骤 ═══
///
/// Player (根节点)
///   └── CameraFollowTarget (空物体, localPosition = (0, 1.5, 0))
///         ↑ 拖到本脚本的 followTarget 字段
///         ↑ 同时拖到 VCam 的 Follow 字段
///
/// VCam 配置:
///   Body = Cinemachine3rdPersonFollow
///     - Camera Distance: 3~5
///     - Shoulder Offset: (0.5, 0, 0) — 右肩视角
///   Aim = Do Nothing
///   Follow = CameraFollowTarget
/// </summary>
[AddComponentMenu("GameMain/Camera/Action Camera Controller")]
public class ActionCameraController : CameraController
{
    [Header("Deadzone Proxy (scene)")]
    [Tooltip("场景中预置的 CameraDeadzoneProxy 节点；VCam.Follow / LookAt 将指向它。\n玩家运行时生成后通过 RebindFollowAndLookAt 动态注入目标。")]
    [SerializeField] private CameraDeadzoneProxy deadzoneProxy;

    [Header("Follow Target (runtime)")]
    [Tooltip("运行时由 RebindFollowAndLookAt 填入（PlayerCameraAnchor.FollowTarget）。\n仅用于每帧写入轨道旋转，不直接传给 VCam。")]
    [SerializeField] private Transform followTarget;

    [Header("Orbit Settings")]
    [SerializeField] private float horizontalSensitivity = 200f;
    [SerializeField] private float verticalSensitivity = 120f;
    [SerializeField] private float verticalMinAngle = -30f;
    [SerializeField] private float verticalMaxAngle = 70f;

    [Header("Self-occlusion guard")]
    [Tooltip("从相机防遮挡 LayerMask 中【剔除】的层（玩家自身 / 武器 / Hitbox / IgnoreRaycast 等）。\n空旷场景跳跃时若相机贴脸，把'Player' 层勾入此处即可——相机的防穿墙射线将忽略玩家自身碰撞体，恢复正常距离。\nRebindFollowAndLookAt 时一次性注入到 VCam 的 Cinemachine3rdPersonFollow.CameraCollisionFilter。")]
    [SerializeField] private LayerMask cameraIgnoreLayers;

    [Tooltip("自动把 followTarget 所在层加入 cameraIgnoreLayers（即便 Inspector 漏勾也能兜底）。")]
    [SerializeField] private bool autoIgnoreFollowTargetLayer = true;

    private float _yaw;
    private float _pitch;

    public override GameModeType Mode => GameModeType.Action;
    public override bool IsCameraRelativeMovement => true;

    private void Awake()
    {
        // 若 Inspector 未手动指派（运行时动态场景），自动在场景中查找唯一的 Proxy。
        // 此处在 Start/PlayerManager.TrySpawnInitial 之前执行，可保证 Rebind 时 Proxy 已就绪。
        if (deadzoneProxy == null)
        {
            deadzoneProxy = FindObjectOfType<CameraDeadzoneProxy>();
            if (deadzoneProxy == null)
            {
                Debug.LogWarning("[ActionCamera] 场景中未找到 CameraDeadzoneProxy，相机死区无效。", this);
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (followTarget != null)
        {
            var euler = followTarget.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
            if (_pitch > 180f) _pitch -= 360f;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 返回仅包含水平偏航角的旋转，作为 PlayerController 计算"虚拟指北"的权威来源。
    /// 只受鼠标/右摇杆驱动，不受角色旋转影响，不依赖 Camera.main，无帧序问题。
    /// </summary>
    public override Quaternion MovementReferenceRotation => Quaternion.Euler(0f, _yaw, 0f);

    protected override void UpdateCamera()
    {
        if (followTarget == null) return;

        // 只在有鼠标/摇杆输入时更新偏航和俯仰
        var look = inputReader.LookInput;
        if (look.sqrMagnitude > 0.0001f)
        {
            _yaw += look.x * horizontalSensitivity * Time.deltaTime;
            _pitch -= look.y * verticalSensitivity * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch, verticalMinAngle, verticalMaxAngle);
        }

        // ❗ 每帧都必须写 followTarget.rotation（世界旋转），
        // 否则 Player.LookAtDirection 旋转父物体时，
        // 子物体 followTarget 的世界朝向会被"拖走"，
        // 导致 Cinemachine 读到被污染的旋转 → 反馈死循环 → 原地转圈。
        followTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    /// <summary>
    /// 动态绑定跟随与注视目标（角色运行时生成 / 队伍切换时由 PlayerManager 调用）。
    ///
    /// 绑定路径：
    ///   PlayerCameraAnchor.FollowTarget → CameraDeadzoneProxy（死区过滤）→ VCam.Follow
    ///   PlayerCameraAnchor.LookAtTarget → CameraDeadzoneProxy.LookAtTransform（精准镜像）→ VCam.LookAt
    ///
    /// ActionCameraController 本身仍持有 <paramref name="follow"/> 引用，
    /// 用于 UpdateCamera() 每帧写入轨道偏航 / 俯仰旋转。
    /// </summary>
    /// <param name="follow">跟随锚点（PlayerCameraAnchor.FollowTarget，Player 子物体）。</param>
    /// <param name="lookAt">注视锚点；为 null 时与 <paramref name="follow"/> 相同。</param>
    public void RebindFollowAndLookAt(Transform follow, Transform lookAt = null)
    {
        if (follow == null)
        {
            Debug.LogError("[ActionCamera] RebindFollowAndLookAt: follow 不能为 null。", this);
            return;
        }

        if (virtualCamera == null)
        {
            Debug.LogError("[ActionCamera] RebindFollowAndLookAt: VirtualCamera 未指派。", this);
            return;
        }

        // 存储原始锚点：UpdateCamera() 每帧写旋转用
        followTarget = follow;

        // ── 优先路由到 Proxy（动态绑定的核心路径）──
        if (deadzoneProxy != null)
        {
            deadzoneProxy.Rebind(follow, lookAt);
            virtualCamera.Follow = deadzoneProxy.transform;
            virtualCamera.LookAt = deadzoneProxy.LookAtTransform;
        }
        else
        {
            // 降级：无 Proxy 时直接绑定（兼容未配置 Proxy 的场景）
            Debug.LogWarning("[ActionCamera] deadzoneProxy 未指派，直接绑定 VCam。相机可能出现物理微动。", this);
            var targetLook = lookAt != null ? lookAt : follow;
            virtualCamera.Follow = follow;
            virtualCamera.LookAt = targetLook;
        }

        // 从锚点当前朝向初始化偏航 / 俯仰，防止激活瞬间相机跳变
        var euler = follow.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;
        if (_pitch > 180f)
        {
            _pitch -= 360f;
        }

        // 将"自遮挡 LayerMask"注入到 VCam 的 3rdPersonFollow 防穿墙过滤器：
        //   病因：默认 CameraCollisionFilter = Everything，跳跃时玩家自身的碰撞体（武器/Hitbox/胶囊体等）
        //         会被 3rdPersonFollow 的肩部射线击中，相机被误判为"被遮挡"而拉近到玩家面前。
        //   解药：把玩家所在层从过滤器里剔除——相机只会被【真实环境】阻挡，不再被自身遮挡。
        ApplyCameraSelfOcclusionFilter(follow);
    }

    /// <summary>
    /// 将 cameraIgnoreLayers 注入到 VCam 的 Cinemachine3rdPersonFollow.CameraCollisionFilter。
    /// 注：3rdPersonFollow.CameraCollisionFilter 接受"会阻挡相机的层" — 因此用 ~ignore 取反。
    /// </summary>
    private void ApplyCameraSelfOcclusionFilter(Transform follow)
    {
        if (virtualCamera == null) return;

        // 兜底：把 followTarget（PlayerCameraAnchor 子物体）所在层也算入忽略集
        var ignoreMask = cameraIgnoreLayers.value;
        if (autoIgnoreFollowTargetLayer && follow != null)
        {
            ignoreMask |= 1 << follow.gameObject.layer;
        }

        // Cinemachine 2.x：CinemachineVirtualCamera.GetCinemachineComponent<T>()
        if (virtualCamera is CinemachineVirtualCamera vcam)
        {
            var thirdPerson = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
            if (thirdPerson != null)
            {
                // CameraCollisionFilter = "可阻挡相机的层"，所以把忽略层取反
                thirdPerson.CameraCollisionFilter = ~ignoreMask;
            }
        }
    }
}
