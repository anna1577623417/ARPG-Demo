using UnityEngine;

/// <summary>
/// 187.3 W12 — Motion Authority 实时调试 HUD。
/// <para>挂在任意 GameObject；运行时左上角显示当前 Move/Rotate Holder + Priority + 最近 N 条切换日志。</para>
/// <para>调参时实时看到权威切换轨迹；BUG 复现时一眼定位。</para>
/// </summary>
public sealed class AuthorityHud : MonoBehaviour
{
    [SerializeField] bool m_show = true;
    [SerializeField] Rect m_rect = new(12, 140, 460, 220);
    [SerializeField] MonoBehaviour m_arbiterProvider; // Player 或任何能提供 MotionAuthorityArbiter 的组件

    MotionAuthorityArbiter _arbiter;

    /// <summary>外部直接注入（避免依赖具体 Provider 类型）。</summary>
    public void Bind(MotionAuthorityArbiter arbiter)
    {
        _arbiter = arbiter;
    }

    void OnGUI()
    {
        if (!m_show || _arbiter == null) return;

        GUI.Box(m_rect, GUIContent.none);
        GUILayout.BeginArea(new Rect(m_rect.x + 6, m_rect.y + 4, m_rect.width - 12, m_rect.height - 8));

        GUILayout.Label("[Motion Authority]", GUI.skin.box);
        GUILayout.Label($"  Move   = {_arbiter.MoveHolder} ({_arbiter.MovePriority})");
        GUILayout.Label($"  Rotate = {_arbiter.RotateHolder} ({_arbiter.RotatePriority})");
        GUILayout.Space(4);

        GUILayout.Label("[Recent Changes]");
        foreach (var ev in _arbiter.RecentChanges)
        {
            var arrow = $"{ev.Previous}({ev.PreviousPriority}) → {ev.Current}({ev.CurrentPriority})";
            GUILayout.Label($"  {ev.TimeStamp:F2}s {ev.Channel,-6} {arrow}  {ev.Reason}");
        }

        GUILayout.EndArea();
    }
}
