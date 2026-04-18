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
/// <b>Follow</b> 与 <b>Look At</b> 由 <see cref="SetFollowAndLookAt"/> 统一写入 Cinemachine（多角色、换人、锁定唯一入口）。
/// 轨道枢轴 <see cref="_orbitPivot"/> 仍由 Follow 语义解析（非角色根上的俯仰等）。
///
/// <b>Cinemachine FreeLook（CM2）</b>：不在此写 Follow 旋转；水平/垂直视角<strong>仅</strong>由本脚本在 <see cref="UpdateCamera"/> 内
/// 根据 <see cref="InputReader.LookInput"/> 写入 <c>m_XAxis</c> / <c>m_YAxis</c>。
/// 启用时清除轴上内置 Input 名并禁用同物体上的 <c>CinemachineInputProvider</c>，避免与自定义输入双写导致轴漂移。
/// 鼠标 delta 按「每帧位移」累加，<b>不</b>乘 <c>Time.deltaTime</c>；手柄右摇杆为连续量，乘 <c>Time.deltaTime</c>（度/秒）。
/// 角色相机相对移动方向由基类根据 Brain 输出刷新，见 <see cref="ICameraDirectionProvider"/>。
///
/// <b>PlayerCharacter</b> 提供分离的 Follow / LookAt 锚点，供 Composer 与纵深构图。
/// </summary>
[DefaultExecutionOrder(-100)]
[AddComponentMenu("GameMain/Camera/Action Camera Controller")]
public class ActionCameraController : CameraController
{
    [Header("Follow Target")]
    [Tooltip("颈部/相机枢轴。可拖 Player 根 — 运行时若根上有 Player 会自动挂子物体承担俯仰。")]
    [SerializeField] private Transform followTarget;

    [Tooltip("自动创建 CameraFollowTarget 时相对 Player 根的高度。")]
    [SerializeField] private float autoOrbitPivotHeight = 1.65f;

    [Header("Orbit Settings — Gamepad")]
    [Tooltip("右摇杆水平：FreeLook m_XAxis 偏航角速度（度/秒），乘 Time.deltaTime。")]
    [SerializeField] private float horizontalSensitivity = 220f;

    [Tooltip("右摇杆垂直：仅用于 <b>手写轨道</b> 俯仰（度/秒）。FreeLook 的 m_YAxis 为 0..1，见下方专用字段。")]
    [SerializeField] private float verticalSensitivity = 140f;

    [Tooltip("右摇杆垂直：FreeLook m_YAxis（0..1 插值）变化速度，满偏约每秒移动量，乘 Time.deltaTime。")]
    [SerializeField] private float freeLookGamepadVerticalAxisSpeed = 1.25f;

    [Header("Orbit Settings — Mouse")]
    [Tooltip("鼠标水平：每单位 LookInput（Mouse delta）写入 FreeLook m_XAxis 的增量（度），不乘 deltaTime。")]
    [SerializeField] private float mouseYawDegreesPerUnit = 0.12f;

    [Tooltip("鼠标垂直：FreeLook m_YAxis（0..1）每单位鼠标 delta 的增量；不乘 deltaTime。")]
    [SerializeField] private float freeLookMouseVerticalAxisPerUnit = 0.002f;

    [Tooltip("鼠标垂直：仅用于手写轨道俯仰（度/单位 delta）。")]
    [SerializeField] private float mousePitchDegreesPerUnit = 0.10f;

    [Header("Debug — Movement diagnostics")]
    [Tooltip("[MoveDiag:2] 每帧打印 FreeLook m_XAxis / m_YAxis；勿动鼠标，观察轴是否仍被隐式驱动。")]
    [SerializeField] private bool debugLogFreeLookAxisValues;

    [SerializeField] private float verticalMinAngle = -30f;
    [SerializeField] private float verticalMaxAngle = 70f;

    private float _yaw;
    private float _pitch;

    private bool _orbitInputSuppressed;

    private Transform _orbitPivot;

    /// <summary>Cinemachine LookAt 目标（可与 Follow 轨道枢轴不同）。</summary>
    private Transform _cinemachineLookAt;

    public override GameModeType Mode => GameModeType.Action;

    /// <summary>当前虚拟相机是否为 CM2 <see cref="CinemachineFreeLook"/>（由本脚本驱动轴向）。</summary>
    public bool IsFreeLookVirtualCamera
    {
        get
        {
#if CINEMACHINE_3
            return false;
#else
            return virtualCamera is CinemachineFreeLook;
#endif
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

#if !CINEMACHINE_3
        DisconnectBuiltinFreeLookAxisDrivers();
#endif
        ResolveOrbitPivot();
        ApplyVirtualCameraFollow();

        // ★ 初始化 _yaw：FreeLook 优先从 fl.m_XAxis.Value 读取初始偏航角（通常设计师会将
        //   初始值配置为 180°，使相机开始于角色身后）；非 FreeLook 则从 OrbitPivot 读取。
        // 必须在 DisconnectBuiltinFreeLookAxisDrivers + ApplyVirtualCameraFollow 之后执行。
#if !CINEMACHINE_3
        if (virtualCamera is CinemachineFreeLook flInit)
        {
            _yaw = flInit.m_XAxis.Value;
            _pitch = 0f;
            // 立即将 orbitPivot 旋转锁定到 _yaw，断开玩家旋转对 Follow Target 的污染
            if (_orbitPivot != null)
            {
                _orbitPivot.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }
        }
        else
#endif
        if (_orbitPivot != null)
        {
            SyncYawPitchFromOrbitPivot();
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

    public void SetOrbitInputSuppressed(bool suppressed)
    {
        _orbitInputSuppressed = suppressed;
    }

    public void ForceYaw(float yawDegrees)
    {
        _yaw = yawDegrees;
        if (!FreeLookHandlesOrbit() && _orbitPivot != null)
        {
            _orbitPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }

    /// <summary>仅更新 LookAt（锁定目标每帧移动时使用）。</summary>
    public void SetLookAtTarget(Transform lookAt)
    {
        _cinemachineLookAt = lookAt != null ? lookAt : _orbitPivot;
        if (virtualCamera != null)
        {
            virtualCamera.LookAt = _cinemachineLookAt;
        }
    }

    /// <summary>
    /// 运行时绑定 Follow 与 LookAt。<paramref name="lookAt"/> 为 null 时从 <see cref="PlayerCharacter"/> 推断。
    /// </summary>
    public void SetFollowAndLookAt(Transform newFollow, Transform lookAt)
    {
        followTarget = newFollow;

        if (newFollow == null)
        {
            _orbitPivot = null;
            _cinemachineLookAt = null;
            if (virtualCamera != null)
            {
                virtualCamera.Follow = null;
                virtualCamera.LookAt = null;
            }

            return;
        }

        _cinemachineLookAt = lookAt != null ? lookAt : ResolveLookAtFromFollow(newFollow);
        ResolveOrbitPivot();
        ApplyVirtualCameraFollow();

        if (newFollow.GetComponent<Player>() != null)
        {
            _yaw = newFollow.eulerAngles.y;
            _pitch = 0f;
        }
        else
        {
            SyncYawPitchFromOrbitPivot();
        }
    }

    /// <summary>与 <see cref="SetFollowAndLookAt"/>（lookAt=null）等价。</summary>
    public void SetFollowTarget(Transform newTarget)
    {
        SetFollowAndLookAt(newTarget, null);
    }

    public void SetFollowTargetSmooth(Transform newTarget)
    {
        SetFollowAndLookAt(newTarget, null);
    }

    private static Transform ResolveLookAtFromFollow(Transform followTransform)
    {
        if (followTransform == null)
        {
            return null;
        }

        var pc = followTransform.GetComponentInParent<PlayerCharacter>() ??
                 followTransform.GetComponent<PlayerCharacter>();
        return pc != null ? pc.CameraLookAtOrFollow : followTransform;
    }

#if !CINEMACHINE_3
    /// <summary>
    /// 切断 CM 内置轴输入与 CinemachineInputProvider，仅保留本脚本的 <c>m_XAxis/m_YAxis.Value</c> 增量写入。
    /// 同时清零加速/减速时间与自动回中，防止窗口移动残留惯性漂移。
    /// </summary>
    private void DisconnectBuiltinFreeLookAxisDrivers()
    {
        if (!(virtualCamera is CinemachineFreeLook fl))
        {
            return;
        }

        var x = fl.m_XAxis;
        x.m_InputAxisName = string.Empty;
        x.m_InputAxisValue = 0f;
        x.m_AccelTime = 0f;
        x.m_DecelTime = 0f;
        x.m_Recentering.m_enabled = false;
        x.Reset();
        fl.m_XAxis = x;

        var y = fl.m_YAxis;
        y.m_InputAxisName = string.Empty;
        y.m_InputAxisValue = 0f;
        y.m_AccelTime = 0f;
        y.m_DecelTime = 0f;
        y.m_Recentering.m_enabled = false;
        y.Reset();
        fl.m_YAxis = y;

        foreach (var behaviour in fl.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour != null && behaviour.GetType().Name == "CinemachineInputProvider")
            {
                behaviour.enabled = false;
            }
        }
    }

    private void ApplyFreeLookLookInput(CinemachineFreeLook fl)
    {
        if (inputReader == null)
        {
            return;
        }

        var look = inputReader.LookInput;
        if (look.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // ★ _yaw 与 fl.m_XAxis.Value 同步增量更新：
        //   fl.m_XAxis.Value 供 Cinemachine 驱动相机视觉位置；
        //   _yaw 是独立维护的世界空间偏航角权威值，供 MovementReferenceRotation 使用。
        //   两者必须用相同的 delta，否则移动方向与相机视角会逐渐错位。
        if (inputReader.LookActuatedByGamepad)
        {
            var yawDelta = look.x * horizontalSensitivity * Time.deltaTime;
            fl.m_XAxis.Value += yawDelta;
            _yaw += yawDelta;
            fl.m_YAxis.Value -= look.y * freeLookGamepadVerticalAxisSpeed * Time.deltaTime;
        }
        else
        {
            var yawDelta = look.x * mouseYawDegreesPerUnit;
            fl.m_XAxis.Value += yawDelta;
            _yaw += yawDelta;
            fl.m_YAxis.Value -= look.y * freeLookMouseVerticalAxisPerUnit;
        }
    }
#endif

    protected override void UpdateCamera()
    {
        if (followTarget == null)
        {
            return;
        }

        if (_orbitPivot == null)
        {
            ResolveOrbitPivot();
        }

        if (_orbitPivot == null)
        {
            return;
        }

        if (FreeLookHandlesOrbit())
        {
#if !CINEMACHINE_3
            if (!_orbitInputSuppressed && inputReader != null && virtualCamera is CinemachineFreeLook fl)
            {
                ApplyFreeLookLookInput(fl);
            }

            if (debugLogFreeLookAxisValues && virtualCamera is CinemachineFreeLook flDiag)
            {
                var look = inputReader != null ? inputReader.LookInput : Vector2.zero;
                Debug.Log(
                    $"[MoveDiag:2 FreeLookAxis] m_XAxis.Value={flDiag.m_XAxis.Value:F6} _yaw={_yaw:F6} " +
                    $"LookInput={look} suppress={_orbitInputSuppressed} frame={Time.frameCount}");
            }
#endif
            // ★ 反馈环切断：每帧强制覆写 OrbitPivot 的世界旋转为 _yaw。
            //   CameraFollowTarget 是 Player 的子物体，若不覆写，玩家旋转（LookAtDirection）
            //   会带动 CameraFollowTarget 世界旋转，导致 FreeLook 相对 heading 模式下
            //   轨道角度隐式偏移 → 移动方向与预期偏差 → 玩家再次旋转 → 正反馈振荡（左右摇摆）。
            if (_orbitPivot != null)
            {
                _orbitPivot.rotation = Quaternion.Euler(0f, _yaw, 0f);
            }

            return;
        }

        var lookManual = inputReader != null ? inputReader.LookInput : Vector2.zero;
        if (!_orbitInputSuppressed && lookManual.sqrMagnitude > 0.0001f && inputReader != null)
        {
            if (inputReader.LookActuatedByGamepad)
            {
                _yaw += lookManual.x * horizontalSensitivity * Time.deltaTime;
                _pitch -= lookManual.y * verticalSensitivity * Time.deltaTime;
            }
            else
            {
                _yaw += lookManual.x * mouseYawDegreesPerUnit;
                _pitch -= lookManual.y * mousePitchDegreesPerUnit;
            }

            _pitch = Mathf.Clamp(_pitch, verticalMinAngle, verticalMaxAngle);
        }

        _orbitPivot.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    /// <summary>
    /// ★ MovementReferenceRotation：始终用本脚本自维护的 <see cref="_yaw"/>（世界空间绝对偏航角）
    /// 合成平面移动轴，彻底解决以下两类问题：
    ///
    /// 问题 A — Brain 帧序：CinemachineBrain 执行序 -5，本脚本 -100；
    ///   基类若读 Camera.main.transform.forward，该帧 Brain 还未移动相机 → 方向滞后 → 正反馈转圈。
    ///
    /// 问题 B — FreeLook target-relative heading：
    ///   若用 fl.m_XAxis.Value 作为世界偏航，当玩家旋转（LookAtDirection）后，
    ///   FreeLook 在相对 heading 模式下 m_XAxis.Value 并不对应世界偏航 → 移动方向随玩家旋转偏移。
    ///
    /// _yaw 由 ApplyFreeLookLookInput / UpdateCamera 在同一 LateUpdate 内同步增量维护，
    /// 始终是纯世界偏航角，不受 Player.transform.rotation 或 CinemachineBrain 延迟影响。
    /// </summary>
    protected override void RefreshPlanarMovementAxesFromBrainOutput()
    {
        // 两条路径（FreeLook / 普通 VCam）统一使用 _yaw，不再区分
        var yawRot = Quaternion.Euler(0f, _yaw, 0f);
        _planarMovementForward = yawRot * Vector3.forward;
        _planarMovementRight   = yawRot * Vector3.right;
    }

    private bool FreeLookHandlesOrbit()
    {
#if CINEMACHINE_3
        return false;
#else
        return virtualCamera is CinemachineFreeLook;
#endif
    }

    private void ResolveOrbitPivot()
    {
        _orbitPivot = null;
        if (followTarget == null)
        {
            return;
        }

        if (followTarget.GetComponent<Player>() != null)
        {
            var existing = followTarget.Find("CameraFollowTarget");
            if (existing != null)
            {
                _orbitPivot = existing;
                return;
            }

            var go = new GameObject("CameraFollowTarget");
            var t = go.transform;
            t.SetParent(followTarget, false);
            t.localPosition = new Vector3(0f, autoOrbitPivotHeight, 0f);
            t.localRotation = Quaternion.identity;
            _orbitPivot = t;
            return;
        }

        _orbitPivot = followTarget;
    }

    private void ApplyVirtualCameraFollow()
    {
        if (virtualCamera == null || _orbitPivot == null)
        {
            return;
        }

        virtualCamera.Follow = _orbitPivot;
        var look = _cinemachineLookAt != null ? _cinemachineLookAt : _orbitPivot;
        virtualCamera.LookAt = look;
    }

    public bool TryGetFollowWorldSamplePosition(out Vector3 worldPosition)
    {
        if (virtualCamera != null && virtualCamera.Follow != null)
        {
            worldPosition = virtualCamera.Follow.position;
            return true;
        }

        if (_orbitPivot != null)
        {
            worldPosition = _orbitPivot.position;
            return true;
        }

        worldPosition = default;
        return false;
    }

    private void SyncYawPitchFromOrbitPivot()
    {
        if (_orbitPivot == null)
        {
            return;
        }

        var euler = _orbitPivot.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;
        if (_pitch > 180f)
        {
            _pitch -= 360f;
        }
    }
}
