#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// GameMain → HUD → Dump HUD State（Play 模式快照）。Log 开关见 GameMain → Debug → Log Settings。
/// </summary>
public static class HudBugDiagnosticsMenu
{
    const string DumpPath = "GameMain/HUD/Dump HUD State";

    [MenuItem(DumpPath, false, 121)]
    static void DumpHudState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[HudBug] Dump HUD State 仅 Play Mode 可用。");
            return;
        }

        var player = ResolveActivePlayer();
        if (player == null)
        {
            Debug.LogWarning("[HudBug] Dump HUD State — 未找到 Active Player。");
            return;
        }

        var prev = HudBugProbe.IsEnabled;
        if (!prev)
        {
            HudBugProbe.SetEnabled(true);
        }

        HudBugProbe.ScanScene(player);
        Debug.Log("[HudBug] Dump complete — 详见上方 [HudBug] SCAN 与 WARNING 行。");

        if (!prev)
        {
            HudBugProbe.SetEnabled(false);
        }
    }

    [MenuItem(DumpPath, true)]
    static bool DumpHudStateValidate() => Application.isPlaying;

    static Player ResolveActivePlayer()
    {
        var managers = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
        for (var i = 0; i < managers.Length; i++)
        {
            var p = managers[i]?.ActivePlayer;
            if (p != null)
            {
                return p;
            }
        }

        return Object.FindFirstObjectByType<Player>();
    }
}
#endif
