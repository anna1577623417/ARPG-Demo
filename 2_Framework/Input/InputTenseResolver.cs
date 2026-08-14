using UnityEngine;

/// <summary>184.1 — WASD 输入时态（Tap / Hold / Pending）。</summary>
public enum InputTense : byte
{
    Idle = 0,
    Pending = 1,
    Tap = 2,
    Hold = 3,
    ShortHold = 4,
}

/// <summary>
/// 184.1 Layer 1：方向键 Tap/Hold 时态唯一裁决。
/// <para>Pending 窗口内可被 Combo（Skill+方向）抢占；WASD 从 Pending 起始终是 Locomotion 移动命令，Tap 不再补 Turn。</para>
/// </summary>
public sealed class InputTenseResolver
{
    public float TapMaxDuration = 0.15f;
    public float HoldEnterDelay = 0.08f;

    float m_pressedSinceTime = -999f;
    bool m_wasPressed;
    InputTense m_lastTense = InputTense.Idle;

    public InputTense LastTense => m_lastTense;

    /// <summary>每帧调用；<paramref name="isPressed"/> 为方向输入是否超过死区。</summary>
    public InputTense Tick(bool isPressed, float now)
    {
        if (isPressed && !m_wasPressed)
        {
            m_pressedSinceTime = now;
            m_wasPressed = true;
            m_lastTense = InputTense.Pending;
            return m_lastTense;
        }

        if (isPressed)
        {
            var pressed = now - m_pressedSinceTime;
            m_lastTense = pressed >= HoldEnterDelay ? InputTense.Hold : InputTense.Pending;
            return m_lastTense;
        }

        if (!isPressed && m_wasPressed)
        {
            var pressed = now - m_pressedSinceTime;
            m_wasPressed = false;
            m_lastTense = pressed < TapMaxDuration ? InputTense.Tap : InputTense.ShortHold;
            return m_lastTense;
        }

        m_lastTense = InputTense.Idle;
        return m_lastTense;
    }

    public void Reset()
    {
        m_wasPressed = false;
        m_pressedSinceTime = -999f;
        m_lastTense = InputTense.Idle;
    }

    public void ApplyTuning(LocomotionTuningSO tuning)
    {
        if (tuning == null)
        {
            return;
        }

        TapMaxDuration = tuning.TapMaxDuration;
        HoldEnterDelay = tuning.HoldEnterDelay;
    }
}
