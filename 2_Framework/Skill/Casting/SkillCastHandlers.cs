using UnityEngine;

/// <summary>
/// Phase 5 释放五态：<see cref="CastType"/> 对应的输入/推进协议。
/// <see cref="SkillSystem"/> 在准备意图时装配处理器；Action 段内见 <see cref="SkillContext.CastHandler"/> 每帧 Tick。
/// </summary>
public interface ICastHandler
{
    void OnInputPressed(SkillContext ctx);
    void OnInputReleased(SkillContext ctx);
    void OnUpdate(SkillContext ctx, float deltaTime);

    /// <summary>技能段是否已结束（用于 HoldRelease 松手、引导到时等）。</summary>
    bool IsComplete { get; }

    /// <summary>
    /// 语义因 CastType 而异：CastTime=读条是否完成；Channel=是否仍在引导内；HoldRelease=按住中持续为 true。
    /// 与 <see cref="SkillCastGating.ShouldDispatchTasksAndWindows"/> 共用。
    /// </summary>
    bool ShouldTrigger { get; }
}

public sealed class InstantCastHandler : ICastHandler
{
    public bool IsComplete => true;
    public bool ShouldTrigger => true;

    public void OnInputPressed(SkillContext ctx) { }

    public void OnInputReleased(SkillContext ctx) { }

    public void OnUpdate(SkillContext ctx, float deltaTime) { }
}

public sealed class ChargeCastHandler : ICastHandler
{
    public bool IsComplete { get; private set; }
    public bool ShouldTrigger { get; private set; }

    public void OnInputPressed(SkillContext ctx)
    {
        IsComplete = false;
        ShouldTrigger = false;
        if (ctx?.Runtime != null)
        {
            ctx.Runtime.IsCharging = true;
            ctx.Runtime.ChargeStartTime = Time.time;
        }
    }

    public void OnInputReleased(SkillContext ctx)
    {
        ShouldTrigger = true;
        IsComplete = true;
        if (ctx?.Runtime != null)
        {
            ctx.Runtime.IsCharging = false;
        }
    }

    public void OnUpdate(SkillContext ctx, float deltaTime) { }
}

public sealed class CastTimeCastHandler : ICastHandler
{
    readonly float _seconds;
    float _elapsed;
    bool _cancelled;

    public CastTimeCastHandler(float seconds)
    {
        _seconds = Mathf.Max(0f, seconds);
    }

    /// <summary>读条中途松键等导致的取消；非自然读条完成。</summary>
    public bool WasCancelled => _cancelled;

    public bool IsComplete => _cancelled || _seconds <= 1e-4f || _elapsed >= _seconds;

    public bool ShouldTrigger => !_cancelled && (_seconds <= 1e-4f || _elapsed >= _seconds);

    public void OnInputPressed(SkillContext ctx)
    {
        _elapsed = 0f;
        _cancelled = false;
    }

    public void OnInputReleased(SkillContext ctx)
    {
        if (_cancelled || _seconds <= 1e-4f)
        {
            return;
        }

        if (_elapsed + 1e-4f < _seconds)
        {
            _cancelled = true;
        }
    }

    public void OnUpdate(SkillContext ctx, float deltaTime)
    {
        if (!_cancelled)
        {
            _elapsed += deltaTime;
        }
    }
}

public sealed class ChannelCastHandler : ICastHandler
{
    readonly float _duration;
    float _elapsed;
    bool _releasedEarly;

    public ChannelCastHandler(float duration)
    {
        _duration = Mathf.Max(0f, duration);
    }

    /// <summary>引导未到时长提前松键结束。</summary>
    public bool ReleasedEarly => _releasedEarly;

    public bool IsComplete => _releasedEarly || _duration <= 1e-4f || _elapsed >= _duration;

    public bool ShouldTrigger => !IsComplete;

    public void OnInputPressed(SkillContext ctx)
    {
        _elapsed = 0f;
        _releasedEarly = false;
    }

    public void OnInputReleased(SkillContext ctx)
    {
        if (!IsComplete)
        {
            _releasedEarly = true;
        }
    }

    public void OnUpdate(SkillContext ctx, float deltaTime)
    {
        if (IsComplete)
        {
            return;
        }

        _elapsed += deltaTime;
    }
}

public sealed class HoldReleaseCastHandler : ICastHandler
{
    bool _released;

    public bool IsComplete => _released;

    /// <summary>按住持续效果时为 true；松手完成收尾后 IsComplete。</summary>
    public bool ShouldTrigger => !_released;

    public void OnInputPressed(SkillContext ctx)
    {
        _released = false;
        if (ctx?.Runtime != null)
        {
            ctx.Runtime.IsActive = true;
        }
    }

    public void OnInputReleased(SkillContext ctx)
    {
        _released = true;
        if (ctx?.Runtime != null)
        {
            ctx.Runtime.IsActive = false;
        }
    }

    public void OnUpdate(SkillContext ctx, float deltaTime) { }
}

/// <summary>
/// 由 <see cref="SkillDataSO.castType"/> 装配处理器。
/// </summary>
public static class SkillCastHandlerFactory
{
    public static ICastHandler Create(SkillDataSO sheet)
    {
        if (sheet == null)
        {
            return new InstantCastHandler();
        }

        switch (sheet.castType)
        {
            case CastType.Instant:
                return new InstantCastHandler();
            case CastType.Charge:
                return new ChargeCastHandler();
            case CastType.CastTime:
                return new CastTimeCastHandler(sheet.castTime);
            case CastType.Channel:
                return new ChannelCastHandler(sheet.channelDuration);
            case CastType.HoldRelease:
                return new HoldReleaseCastHandler();
            default:
                return new InstantCastHandler();
        }
    }
}

/// <summary>
/// 将 Cast 五态映射到 Action 内的 Task / Window 闸门（读条未结束不派发 HitFrame 等）。
/// </summary>
public static class SkillCastGating
{
    public static bool ShouldDispatchTasksAndWindows(ICastHandler handler, SkillDataSO sheet)
    {
        if (sheet == null || handler == null)
        {
            return true;
        }

        switch (sheet.castType)
        {
            case CastType.CastTime:
            case CastType.Channel:
            case CastType.HoldRelease:
                return handler.ShouldTrigger;
            default:
                return true;
        }
    }
}
