using UnityEngine;

/// <summary>
/// 177.1 W0 — Stance 调试 HUD（OnGUI 极简）。
/// <para>挂在 StanceController 同 GameObject；运行时左上角显示当前 Stance / 切换进度 / 屏蔽能力。</para>
/// </summary>
[RequireComponent(typeof(StanceController))]
public sealed class StanceHud : MonoBehaviour
{
    [SerializeField] bool m_show = true;
    [SerializeField] Rect m_rect = new(12, 12, 360, 120);

    StanceController _ctrl;

    void Awake() => _ctrl = GetComponent<StanceController>();

    void OnGUI()
    {
        if (!m_show || _ctrl == null) return;

        GUI.Box(m_rect, GUIContent.none);
        GUILayout.BeginArea(new Rect(m_rect.x + 6, m_rect.y + 4, m_rect.width - 12, m_rect.height - 8));

        var cur = _ctrl.Current;
        var pen = _ctrl.Pending;

        GUILayout.Label(cur != null
            ? $"Stance: {cur.Id} ({cur.DisplayName})  Height×{_ctrl.HeightScale:F2}"
            : "Stance: <unset>");

        if (pen != null)
        {
            GUILayout.Label($"→ {pen.Id} ({pen.DisplayName})  {_ctrl.TransitionProgress * 100f:F0}%");
        }

        if (cur != null && cur.BlockedAbilityNames != null && cur.BlockedAbilityNames.Length > 0)
        {
            GUILayout.Label("Blocked: " + string.Join(", ", cur.BlockedAbilityNames));
        }

        if (cur != null && cur.GrantedAbilityNames != null && cur.GrantedAbilityNames.Length > 0)
        {
            GUILayout.Label("Granted: " + string.Join(", ", cur.GrantedAbilityNames));
        }

        GUILayout.EndArea();
    }
}
