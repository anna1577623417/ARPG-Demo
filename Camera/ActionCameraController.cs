using UnityEngine;

#if CINEMACHINE_3
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

/// <summary>
/// 动作模式相机控制器（类魂 / 第三人称跟随）。
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
    [Header("Follow Target")]
    [Tooltip("玩家根节点下的空物体（不是骨骼子物体）。位置 ≈ 颈部高度。")]
    [SerializeField] private Transform followTarget;

    [Header("Orbit Settings")]
    [SerializeField] private float horizontalSensitivity = 200f;
    [SerializeField] private float verticalSensitivity = 120f;
    [SerializeField] private float verticalMinAngle = -30f;
    [SerializeField] private float verticalMaxAngle = 70f;

    private float _yaw;
    private float _pitch;

    public override GameModeType Mode => GameModeType.Action;
    public override bool IsCameraRelativeMovement => true;

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
}
