using UnityEngine;

/// <summary>185.2 P1 — 敌人破势时开处决窗口（186+ 接通事件源）。</summary>
public sealed class ExecutionWindowOpener : MonoBehaviour
{
    [SerializeField] float m_windowSec = 4f;

    public void OnEnemyStaggered(Player player)
    {
        if (player == null)
        {
            return;
        }

        player.ContextWindows.Open(EventWindowTag.EnemyStaggerExecution, m_windowSec, Time.time);
    }
}
