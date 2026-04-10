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
/// </summary>
public abstract class CameraController : MonoBehaviour
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

    /// <summary>此模式是否使用相机相对移动（Action/FPS = true, MOBA = false）。</summary>
    public abstract bool IsCameraRelativeMovement { get; }

    /// <summary>
    /// 移动参考旋转（仅水平偏航角，无俯仰）。
    /// PlayerController 用这个计算"虚拟指北"，而不是读 Camera.main.transform。
    ///
    /// 为什么不用 Camera.main.transform？
    ///   followTarget 是 Player 子物体 → Player.LookAtDirection 旋转角色时
    ///   会拖动 followTarget 的世界朝向 → Cinemachine 写入 Camera.main 的值被污染
    ///   → 如果 PlayerController 读 Camera.main → 拿到被污染的方向 → 反馈死循环。
    ///
    ///   MovementReferenceRotation 直接从 _yaw（纯鼠标驱动）构造，
    ///   不经过 followTarget → Cinemachine → Camera.main 这条链路，
    ///   完全免疫帧序问题和父子物体旋转耦合。
    /// </summary>
    public virtual Quaternion MovementReferenceRotation => Quaternion.identity;

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

    protected virtual void LateUpdate()
    {
        if (inputReader == null) return;
        UpdateCamera();
    }

    /// <summary>子类实现具体的相机控制逻辑。</summary>
    protected abstract void UpdateCamera();
}
