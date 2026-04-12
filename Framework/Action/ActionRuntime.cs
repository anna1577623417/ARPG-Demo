using UnityEngine;

/// <summary>
/// 行为运行时实例（非 MonoBehaviour，纯 C#）。
///
/// ActionData = "图纸"（不可变，ScriptableObject 资产）
/// ActionRuntime = "正在建造中的建筑"（每次执行创建一个新实例）
///
/// 职责：
///   - 维护时间轴推进（Timer / NormalizedTime）
///   - 追踪各窗口的进入/退出状态
///   - 不直接操作角色 —— 状态查询由 ActionRunner 读取后执行
/// </summary>
public class ActionRuntime
{
    public ActionData Data { get; }

    /// <summary>行为已运行的时间（秒）。</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>归一化进度 [0, 1]。</summary>
    public float NormalizedTime => Data.duration > 0f ? Mathf.Clamp01(ElapsedTime / Data.duration) : 1f;

    /// <summary>行为是否已完成（时间到了）。</summary>
    public bool IsFinished => ElapsedTime >= Data.duration;

    /// <summary>当前是否处于无敌帧窗口。</summary>
    public bool IsInIFrame => Data.HasIFrame
        && NormalizedTime >= Data.iFrameStart
        && NormalizedTime <= Data.iFrameEnd;

    /// <summary>当前是否处于伤害判定窗口。</summary>
    public bool IsInHitFrame => Data.HasHitFrame
        && NormalizedTime >= Data.hitFrameStart
        && NormalizedTime <= Data.hitFrameEnd;

    /// <summary>当前是否处于打断锁定中（不可被打断）。</summary>
    public bool IsInterruptLocked => NormalizedTime < Data.interruptLockEnd;

    /// <summary>当前是否可被打断。</summary>
    public bool CanBeInterrupted => Data.canBeInterrupted && !IsInterruptLocked;

    /// <summary>进入时锁定的位移方向（ForwardLocked 模式用）。</summary>
    public Vector3 LockedDirection { get; }

    // 窗口边缘追踪（用于发布进入/退出事件，避免重复发布）
    private bool _wasInIFrame;
    private bool _wasInHitFrame;

    public ActionRuntime(ActionData data, Vector3 forwardDirection)
    {
        Data = data;
        ElapsedTime = 0f;
        LockedDirection = forwardDirection.sqrMagnitude > 0.0001f
            ? forwardDirection.normalized
            : Vector3.forward;
    }

    /// <summary>
    /// 推进时间轴。返回本帧的窗口边缘事件。
    /// </summary>
    public TickResult Tick(float deltaTime)
    {
        ElapsedTime += deltaTime;

        var result = new TickResult();

        // 无敌帧边缘检测
        bool inIFrame = IsInIFrame;
        if (inIFrame && !_wasInIFrame) result.IFrameEntered = true;
        if (!inIFrame && _wasInIFrame) result.IFrameExited = true;
        _wasInIFrame = inIFrame;

        // 伤害判定边缘检测
        bool inHitFrame = IsInHitFrame;
        if (inHitFrame && !_wasInHitFrame) result.HitFrameEntered = true;
        if (!inHitFrame && _wasInHitFrame) result.HitFrameExited = true;
        _wasInHitFrame = inHitFrame;

        // 完成检测
        if (IsFinished) result.Finished = true;

        return result;
    }

    /// <summary>查询当前帧的位移速度系数。</summary>
    public float GetMoveSpeedFactor()
    {
        return Data.moveSpeedCurve.Evaluate(NormalizedTime);
    }
}

/// <summary>
/// 单帧 Tick 结果。ActionRunner 读取后决定发布哪些事件/执行哪些操作。
/// </summary>
public struct TickResult
{
    public bool IFrameEntered;
    public bool IFrameExited;
    public bool HitFrameEntered;
    public bool HitFrameExited;
    public bool Finished;
}
