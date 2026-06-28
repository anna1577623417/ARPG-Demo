using UnityEngine;

/// <summary>185.2 P1 — 挡刀成功时开 GuardCounter 窗口（186+ 接通事件源）。</summary>
public sealed class GuardCounterOpener : MonoBehaviour
{
    [SerializeField] float m_windowSec = 0.8f;

    public void OnGuardSuccessful(Player player)
    {
        if (player == null)
        {
            return;
        }

        player.ContextWindows.Open(EventWindowTag.GuardCounter, m_windowSec, Time.time);
    }
}
