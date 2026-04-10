using UnityEngine;

/// <summary>
/// MOBA 模式相机控制器（俯视 / 上帝视角）。
///
/// ═══ Cinemachine 搭建指南 ═══
///
/// 1. 创建 CinemachineVirtualCamera
/// 2. Body = CinemachineFramingTransposer 或 CinemachineTransposer
/// 3. Aim = CinemachineComposer 或 Do Nothing（固定朝下看）
/// 4. Follow = 玩家 Transform（或 CameraAnchor）
/// 5. 相机旋转固定为 (55°, 0°, 0°) 左右
///
/// ═══ 核心特性 ═══
///
/// - 边缘滚屏：鼠标移到屏幕边缘，相机平移
/// - 滚轮缩放：调整相机高度/FOV
/// - 移动方向为世界坐标（W = 世界北），不跟随相机旋转
/// - 预留：点击移动（ClickToMove）由 MOBAPlayerController 处理
/// </summary>
[AddComponentMenu("GameMain/Camera/MOBA Camera Controller")]
public class MOBACameraController : CameraController
{
    [Header("Edge Scroll")]
    [SerializeField] private bool enableEdgeScroll = true;
    [SerializeField, Range(0f, 100f)] private float edgeScrollThreshold = 30f;
    [SerializeField] private float edgeScrollSpeed = 20f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoomHeight = 8f;
    [SerializeField] private float maxZoomHeight = 30f;

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool lockToTarget = true;
    [SerializeField] private float followSmoothTime = 0.15f;

    private Vector3 _cameraOffset;
    private Vector3 _followVelocity;
    private float _currentZoomHeight;

    public override GameModeType Mode => GameModeType.MOBA;
    public override bool IsCameraRelativeMovement => false;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (virtualCamera != null)
        {
            _cameraOffset = virtualCamera.transform.position -
                (followTarget != null ? followTarget.position : Vector3.zero);
            _currentZoomHeight = _cameraOffset.y;
        }

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Cursor.lockState = CursorLockMode.None;
    }

    protected override void UpdateCamera()
    {
        HandleZoom();
        HandleEdgeScroll();
        HandleFollow();
    }

    private void HandleZoom()
    {
        // 使用鼠标滚轮（LookInput.y 在 MOBA 模式可复用为滚轮，或单独绑定）
        var scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            _currentZoomHeight -= scrollDelta * zoomSpeed;
            _currentZoomHeight = Mathf.Clamp(_currentZoomHeight, minZoomHeight, maxZoomHeight);
            _cameraOffset.y = _currentZoomHeight;
        }
    }

    private void HandleEdgeScroll()
    {
        if (!enableEdgeScroll || virtualCamera == null) return;

        var mousePos = Input.mousePosition;
        var scrollDir = Vector3.zero;

        if (mousePos.x <= edgeScrollThreshold)
            scrollDir.x = -1f;
        else if (mousePos.x >= Screen.width - edgeScrollThreshold)
            scrollDir.x = 1f;

        if (mousePos.y <= edgeScrollThreshold)
            scrollDir.z = -1f;
        else if (mousePos.y >= Screen.height - edgeScrollThreshold)
            scrollDir.z = 1f;

        if (scrollDir.sqrMagnitude > 0.01f && !lockToTarget)
        {
            virtualCamera.transform.position += scrollDir.normalized * edgeScrollSpeed * Time.deltaTime;
        }
    }

    private void HandleFollow()
    {
        if (virtualCamera == null || followTarget == null) return;

        if (lockToTarget)
        {
            var targetPos = followTarget.position + _cameraOffset;
            virtualCamera.transform.position = Vector3.SmoothDamp(
                virtualCamera.transform.position,
                targetPos,
                ref _followVelocity,
                followSmoothTime);
        }
    }
}
