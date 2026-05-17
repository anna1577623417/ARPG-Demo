using UnityEngine;

/// <summary>
/// Motion 与表现层共享的播放覆盖（蓝图 C2 Phase 3）：由蓄力桥等写入，
/// <see cref="MotionExecutor"/> 在 Tick 内统一应用「时间轴冻结 + 绝对播放速率覆盖 + 子区间循环采样」。<br/>
/// 三种覆盖语义互斥优先级（同帧写入时实际行为）：<br/>
/// • <see cref="FreezeNormalizedAdvance"/> = true → 时钟暂停（采样位置不变）；<br/>
/// • 否则 <see cref="HasLoopWindow"/> = true → 进入 [<see cref="LoopWindowStart"/>, <see cref="LoopWindowEnd"/>] 子区间循环；<br/>
/// • <see cref="HasAnimatorSpeedOverride"/> 始终独立生效，用于覆盖 v4.5 三层组合公式算出的 finalSpeed。
/// </summary>
public struct MotionPlaybackContext
{
    /// <summary>为 true 时本帧不推进 MotionExecutor 内部归一化时钟（蓄力锚点挂起）。</summary>
    public bool FreezeNormalizedAdvance;

    public bool HasAnimatorSpeedOverride;

    /// <summary>为 true 时覆盖 <see cref="MotionExecutor"/> 算出的 profile 合成速率（蓄力慢动作等）。</summary>
    public float AnimatorSpeedOverride;

    /// <summary>
    /// 为 true 时 <see cref="MotionExecutor"/> 在 _elapsed 越过 <see cref="LoopWindowEnd"/> 后卷回
    /// <see cref="LoopWindowStart"/>，并把窗口段的等价平面位移叠到 <c>_startPos</c> 保证位置连续。
    /// 仅对装载 MotionProfile 的 Action 生效。
    /// </summary>
    public bool HasLoopWindow;

    public float LoopWindowStart;

    public float LoopWindowEnd;

    public static MotionPlaybackContext Default => default;

    /// <summary>合并：后写入的非默认值覆盖先写入（多来源时由调用方顺序控制）。</summary>
    public static MotionPlaybackContext Combine(in MotionPlaybackContext a, in MotionPlaybackContext b)
    {
        var r = a;
        if (b.FreezeNormalizedAdvance)
        {
            r.FreezeNormalizedAdvance = true;
        }

        if (b.HasAnimatorSpeedOverride)
        {
            r.HasAnimatorSpeedOverride = true;
            r.AnimatorSpeedOverride = Mathf.Max(0f, b.AnimatorSpeedOverride);
        }

        if (b.HasLoopWindow)
        {
            r.HasLoopWindow = true;
            r.LoopWindowStart = b.LoopWindowStart;
            r.LoopWindowEnd = b.LoopWindowEnd;
        }

        return r;
    }
}
