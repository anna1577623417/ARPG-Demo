using UnityEngine;

/// <summary>
/// 移动输入缓冲（Phase6）：松键后短时内仍保留最近方向。
/// 有效时长由 <see cref="LocomotionTuningSO.DirectionModifierBufferSec"/> 驱动（Player 每帧同步）。
/// </summary>
public sealed class InputModifierBuffer
{
    public const float DefaultBufferSeconds = 0.28f;

    float _bufferSeconds = DefaultBufferSeconds;

    Vector2 m_lastMove;
    float m_lastMoveTime = -999f;

    public float BufferSeconds => _bufferSeconds;

    public void SetBufferSeconds(float seconds) =>
        _bufferSeconds = Mathf.Max(0.05f, seconds);

    public void PushMove(Vector2 move, float now)
    {
        if (move.sqrMagnitude < 1e-6f)
        {
            return;
        }

        m_lastMove = move.normalized;
        m_lastMoveTime = now;
    }

    public Vector2 GetBufferedMove(float now)
    {
        if (now - m_lastMoveTime > _bufferSeconds)
        {
            return Vector2.zero;
        }

        return m_lastMove;
    }

    public float GetBufferAgeSec(float now) =>
        m_lastMoveTime > -900f ? now - m_lastMoveTime : -1f;

    /// <summary>
    /// 213.6 — 超出硬 buffer 但在 softGrace 内仍返回 last move（Shift 专用）。
    /// </summary>
    public bool TryGetSoftBufferedMove(float now, float softGraceSec, out Vector2 move)
    {
        move = Vector2.zero;
        if (m_lastMoveTime < -900f || m_lastMove.sqrMagnitude < 1e-6f)
        {
            return false;
        }

        var age = now - m_lastMoveTime;
        if (age > _bufferSeconds + Mathf.Max(0f, softGraceSec))
        {
            return false;
        }

        move = m_lastMove;
        return true;
    }
}
