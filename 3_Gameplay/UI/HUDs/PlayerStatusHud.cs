using TMPro;
using UnityEngine;

/// <summary>
/// 示例 HUD：需在场景中把 <see cref="UIScreenBase"/> 上的 Deactivate On Awake 关掉或预制体初始激活再由 RegisterHUD 接管。
/// </summary>
public sealed class PlayerStatusHud : UIHUD<PlayerStatusHudProps>
{
    [SerializeField] TMP_Text healthText;

    protected override void OnPropsSet()
    {
        if (healthText != null)
        {
            healthText.text = $"{Props.HealthCurrent:0} / {Props.HealthMax:0}";
        }
    }
}
