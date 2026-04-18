using UnityEngine;

#if CINEMACHINE_3
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

/// <summary>
/// 相机控制器基类。
///
/// 每种游戏模式有一个独立的 CameraController 子类：
///   ActionCameraController  — 第三人称 FreeLook 轨道相机（类魂）
///   FPSCameraController     — 第一人称 POV 相机
///   MOBACameraController    — 俯视 TopDown 相机（边缘滚屏 + 滚轮缩放）
///
/// 职责划分：
///   GameModeManager → 切换哪个 CameraController 激活
///   CameraController → 控制相机行为（旋转/缩放/边缘滚屏/锁定等）
///   InputReader → 提供 LookInput 等原始输入数据
///
/// 设计原则：
///   - 基类持有 VirtualCamera 引用和 InputReader 引用
///   - 子类实现具体的 UpdateCamera 逻辑
///   - Enable/Disable 自动提升/降低 VCam Priority
///   - 不处理角色移动逻辑（那是 PlayerController 的职责）
///   - 相机相对移动的平面 Forward/Right 由 <see cref="RefreshPlanarMovementAxesFromBrainOutput"/> 统一写入，
///     供 <see cref="ICameraDirectionProvider"/> 消费（子类如 MOBA 可覆盖为世界观）。
///   - 各具体相机控制器应使用 <c>[DefaultExecutionOrder(-100)]</c>，早于 <see cref="PlayerController"/> 的 <c>LateUpdate</c>。
/// </summary>
public abstract class CameraController : MonoBehaviour, ICameraDirectionProvider
{
    [Header("Camera")]
    [SerializeField] protected CinemachineVirtualCameraBase virtualCamera;

    [Header("Input")]
    [SerializeField] protected InputReader inputReader;

    [Header("Priority")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    /// <summary>此控制器对应的游戏模式。</summary>
    public abstract GameModeType Mode { get; }

    protected Vector3 _planarMovementForward = Vector3.forward;
    protected Vector3 _planarMovementRight = Vector3.right;

    /// <inheritdoc />
    public Vector3 Forward => _planarMovementForward;

    /// <inheritdoc />
    public Vector3 Right => _planarMovementRight;

    /// <summary>
    /// 获取实际渲染相机的 Transform（供 PlayerController 计算移动方向）。
    ///
    /// ❗ 不能用 virtualCamera.transform：
    ///   Cinemachine 的 VirtualCamera.transform 是它在 Scene 中的摆放位置，
    ///   不会随 Body（如 3rdPersonFollow）计算结果而旋转。
    ///   实际的相机朝向由 CinemachineBrain 写入 Camera.main.transform。
    ///
    /// 因此这里返回 Camera.main.transform，它才是玩家屏幕上看到的真实朝向。
    /// </summary>
    public Transform CameraTransform
    {
        get
        {
            var mainCam = Camera.main;
            return mainCam != null ? mainCam.transform : null;
        }
    }

    // ─── 生命周期 ───

    protected virtual void OnEnable()
    {
        if (virtualCamera != null)
        {
            virtualCamera.Priority = activePriority;
        }
    }

    protected virtual void OnDisable()
    {
        if (virtualCamera != null)
        {
            virtualCamera.Priority = inactivePriority;
        }
    }

    /// <summary>单帧顺序：先由子类 <see cref="UpdateCamera"/> 写轨道/轴，再刷新平面方向供玩家本帧 <c>LateUpdate</c> 消费。</summary>
    protected virtual void LateUpdate()
    {
        UpdateCamera();
        RefreshPlanarMovementAxesFromBrainOutput();
    }

    /// <summary>
    /// 在 <c>LateUpdate</c> 末尾根据 Brain 控制的输出相机刷新 XZ 平面轴（与 <see cref="PlayerController"/> 的 <c>LateUpdate</c> 配合，见该类执行顺序）。
    /// </summary>
    protected virtual void RefreshPlanarMovementAxesFromBrainOutput()
    {
        var outputCam = ResolveOutputCameraForMovementAxes();
        if (outputCam == null)
        {
            _planarMovementForward = Vector3.forward;
            _planarMovementRight = Vector3.right;
            return;
        }

        var f = Vector3.ProjectOnPlane(outputCam.transform.forward, Vector3.up);
        var r = Vector3.ProjectOnPlane(outputCam.transform.right, Vector3.up);
        if (f.sqrMagnitude < 1e-8f || r.sqrMagnitude < 1e-8f)
        {
            _planarMovementForward = Vector3.forward;
            _planarMovementRight = Vector3.right;
            return;
        }

        f.Normalize();
        r.Normalize();
        _planarMovementForward = f;
        _planarMovementRight = r;
    }

    /// <summary>优先使用挂在输出机上的 <c>CinemachineBrain.OutputCamera</c>，避免多 Camera 时误用非 Brain 目标。</summary>
    private static Camera ResolveOutputCameraForMovementAxes()
    {
        var main = Camera.main;
        if (main == null)
        {
            return null;
        }

#if !CINEMACHINE_3
        if (main.TryGetComponent<CinemachineBrain>(out var brain) && brain.OutputCamera != null)
        {
            return brain.OutputCamera;
        }
#endif
        return main;
    }

#if !CINEMACHINE_3
    /// <summary>CM2：读写虚拟相机镜头 FOV（供换人呼吸等）。<c>m_Lens</c> 在具体类型上，不在 <see cref="CinemachineVirtualCameraBase"/>。</summary>
    public bool TryGetLensFieldOfView(out float fieldOfView)
    {
        switch (virtualCamera)
        {
            case CinemachineFreeLook freeLook:
                fieldOfView = freeLook.m_Lens.FieldOfView;
                return true;
            case CinemachineVirtualCamera vcam:
                fieldOfView = vcam.m_Lens.FieldOfView;
                return true;
            default:
                fieldOfView = 60f;
                return false;
        }
    }

    public void SetLensFieldOfView(float fieldOfView)
    {
        switch (virtualCamera)
        {
            case CinemachineFreeLook freeLook:
            {
                var lens = freeLook.m_Lens;
                lens.FieldOfView = fieldOfView;
                freeLook.m_Lens = lens;
                break;
            }
            case CinemachineVirtualCamera vcam:
            {
                var lens = vcam.m_Lens;
                lens.FieldOfView = fieldOfView;
                vcam.m_Lens = lens;
                break;
            }
        }
    }
#else
    public bool TryGetLensFieldOfView(out float fieldOfView)
    {
        fieldOfView = 60f;
        return false;
    }

    public void SetLensFieldOfView(float fieldOfView)
    {
    }
#endif

    /// <summary>子类实现具体的相机控制逻辑。</summary>
    protected abstract void UpdateCamera();
}
