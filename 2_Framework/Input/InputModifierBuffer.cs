using UnityEngine;

/// <summary>移动输入缓冲（Phase6）：松键后短时内仍保留最近方向。</summary>
public sealed class InputModifierBuffer
{
    public const float BufferSeconds = 0.15f;

    Vector2 m_lastMove;
    float m_lastMoveTime = -999f;

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
        if (now - m_lastMoveTime > BufferSeconds)
        {
            return Vector2.zero;
        }

        return m_lastMove;
    }
}
