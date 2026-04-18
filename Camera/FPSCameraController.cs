using UnityEngine;

/// <summary>
/// 第一人称相机控制器（FPS / 沉浸模式）。
///
/// ═══ 核心原理 ═══
///
/// eyePoint（挂在玩家头部骨骼下的空物体）作为视角的旋转支点：
///   - eyePoint 跟随玩家头部移动（因为是骨骼子物体）
///   - eyePoint 的旋转由本脚本驱动（鼠标/右摇杆）
///   - Cinemachine VCam 的 Follow = eyePoint
///   - 激活时锁定光标 + 隐藏光标
///
/// ═══ Cinemachine 搭建步骤 ═══
///
/// 1. 在 Player 模型的 Head 骨骼下创建空物体 EyePoint，位置微调到两眼之间
/// 2. 创建 CinemachineVirtualCamera
///    - Body = CinemachineHardLockToTarget（完全跟随 EyePoint）
///    - Aim = Do Nothing（旋转由 EyePoint 驱动）
///    - Follow = EyePoint
/// 3. 将 VCam 拖到 virtualCamera 字段
/// 4. 将 EyePoint 拖到 eyePoint 字段
///
/// ═══ 注意事项 ═══
///
/// - 激活时应隐藏玩家模型（或只隐藏头部），防止穿模
/// - 角色朝向应跟随相机 yaw（由 PlayerController 在 FPS 模式下处理）
/// </summary>
[DefaultExecutionOrder(-100)]
[AddComponentMenu("GameMain/Camera/FPS Camera Controller")]
public class FPSCameraController : CameraController
{
    [Header("Eye Point")]
    [Tooltip("玩家头部骨骼下的 EyePoint 空物体（视角支点）")]
    [SerializeField] private Transform eyePoint;

    [Header("Look Settings — Gamepad (deg/s)")]
    [SerializeField] private float horizontalSensitivity = 300f;
    [SerializeField] private float verticalSensitivity = 200f;

    [Header("Look Settings — Mouse (deg per LookInput unit, no deltaTime)")]
    [SerializeField] private float mouseYawDegreesPerUnit = 0.15f;
    [SerializeField] private float mousePitchDegreesPerUnit = 0.12f;
    [SerializeField] private float verticalMinAngle = -80f;
    [SerializeField] private float verticalMaxAngle = 80f;

    private float _yaw;
    private float _pitch;

    public override GameModeType Mode => GameModeType.FPS;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (eyePoint != null)
        {
            var euler = eyePoint.eulerAngles;
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

    protected override void UpdateCamera()
    {
        if (eyePoint == null) return;

        if (inputReader == null)
        {
            return;
        }

        var look = inputReader.LookInput;
        if (look.sqrMagnitude > 0.0001f)
        {
            if (inputReader.LookActuatedByGamepad)
            {
                _yaw += look.x * horizontalSensitivity * Time.deltaTime;
                _pitch -= look.y * verticalSensitivity * Time.deltaTime;
            }
            else
            {
                _yaw += look.x * mouseYawDegreesPerUnit;
                _pitch -= look.y * mousePitchDegreesPerUnit;
            }

            _pitch = Mathf.Clamp(_pitch, verticalMinAngle, verticalMaxAngle);
        }

        // 每帧都写世界旋转，防止父物体旋转污染 eyePoint 朝向
        eyePoint.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }
}
