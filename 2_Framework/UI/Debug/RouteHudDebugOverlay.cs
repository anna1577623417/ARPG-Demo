using UnityEngine;

/// <summary>
/// Play 模式 HUD 叠层：Entry / Route / Stage / CD / Action 支柱 / HudHandles。
/// 与 Player.DebugSkillRoute 配合；挂到 HUD 根并由 PlayerHudBootstrap 绑定 Player。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("GameMain/UI/Route HUD Debug Overlay")]
public sealed class RouteHudDebugOverlay : MonoBehaviour
{
    [SerializeField] Player playerOverride;

    [SerializeField] Vector2 guiPosition = new Vector2(8f, 220f);

    [SerializeField] int fontSize = 11;

    [SerializeField] float lineWidth = 520f;

    [SerializeField] float lineHeight = 640f;

    Player m_player;

    public void Bind(Player player) => m_player = player;

    public void Unbind() => m_player = null;

    void OnGUI()
    {
        if (!isActiveAndEnabled || !Application.isPlaying)
        {
            return;
        }

        var player = m_player != null ? m_player : playerOverride;
        if (player == null)
        {
            return;
        }

        if (!player.DebugSkillRoute)
        {
            return;
        }

        var service = player.SkillEntries;
        var text = service != null
            ? BuildSnapshot(player, service)
            : "(no SkillEntryService)";

        var prev = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = fontSize;
        GUI.Label(new Rect(guiPosition.x, guiPosition.y, lineWidth, lineHeight), text);
        GUI.skin.label.fontSize = prev;
    }

    static string BuildSnapshot(Player player, SkillEntryService service)
    {
        var sb = new System.Text.StringBuilder(384);
        var state = player.States?.Current?.StateId ?? "?";
        sb.Append("State:       ").AppendLine(state);
        sb.Append("Attacking:   ").Append(player.IsAttacking).AppendLine();
        sb.Append("ActiveSlot:  ").AppendLine(service.ActiveEntrySlot.ToString());

        var rt = service.ActiveRoute;
        if (rt != null)
        {
            sb.Append("Route:       ").Append(rt.Definition != null ? rt.Definition.name : "?")
              .Append(" (").Append(rt.GetType().Name).Append(")")
              .Append("  active=").Append(rt.IsActive)
              .Append("  CD=").Append(rt.CdRemainingSeconds.ToString("F2")).AppendLine();

            if (rt.Stage != null)
            {
                var stName = rt.Stage.Definition != null ? rt.Stage.Definition.name : "(no def)";
                sb.Append("Stage:       ").Append(stName)
                  .Append("  idx=").Append(rt.CurrentStageIndex)
                  .Append("  done=").Append(rt.Stage.Completed)
                  .Append("  dur=").Append(rt.Stage.DurationSeconds.ToString("F2"))
                  .Append("  nt=").Append(rt.Stage.NormalizedTime.ToString("F2"))
                  .AppendLine();
            }

            if (rt is ChargeRouteRuntime cr)
            {
                sb.Append("Charge:      hold=").Append(cr.HoldSeconds.ToString("F2"))
                  .Append("  p=").Append(cr.ChargeProgress01.ToString("F2"))
                  .Append("  full=").Append(cr.HasReachedFull).AppendLine();
            }

            if (rt is MultiStageRouteRuntime ms)
            {
                sb.Append("MultiStage:  pending=").Append(ms.HasPendingNextStage)
                  .Append("  win=").Append(ms.PendingWindowRemainingSeconds.ToString("F2"))
                  .AppendLine();
            }
        }
        else
        {
            sb.AppendLine("Route:       —");
        }

        sb.Append("HudHandles:  ").Append(service.HudHandles.Count).AppendLine();
        sb.AppendLine("(Console: [SkillRoute][Dodge4] 仅四向闪避链 · 需 Player.DebugSkillRoute)");
        return sb.ToString();
    }
}
