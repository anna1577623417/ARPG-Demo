using TMPro;
using UnityEngine;

/// <summary>
/// 玩家状态 HUD（v4.3 升级）：
/// · 优先走 Presenter 模式（Props.Player 非 null 时）：PlayerHUDPresenter / SkillBarPresenter / BuffStripPresenter
///   资源与 Buff 用事件；技能 CD 由槽位上 SkillCooldownTicker 自驱动。
/// · 兼容旧路径（Props.Player 为 null 时）：仅刷新 healthText（旧轮询模式）。
///
/// ═══ Inspector 搭建 ═══
///
/// PlayerStatusHud (this)
///   ├── ResourceGroup
///   │     ├── HP_Bar      (ResourceBarView, Type=HP, MaxStatLink=MaxHealth)
///   │     └── Stamina_Bar (ResourceBarView, Type=Stamina, MaxStatLink=MaxStamina)
///   └── SkillBar
///         ├── Slot_Primary   (SkillSlotView)   ← 拖到 SkillBarPresenter.slotBindings
///         ├── Slot_Secondary (SkillSlotView)
///         └── ...
///
/// 把 PlayerHUDPresenter、SkillBarPresenter、BuffStripPresenter 拖到本组件对应字段；技能槽预制体须挂 SkillCooldownTicker。
/// </summary>
public sealed class PlayerStatusHud : UIHUD<PlayerStatusHudProps>
{
    [Header("Legacy fallback (Props.Player == null 时使用)")]
    [SerializeField] TMP_Text healthText;

    [Header("v4.3 Presenters")]
    [Tooltip("玩家资源 HUD Presenter（HP / Stamina / MP 条订阅事件）。")]
    [SerializeField] PlayerHUDPresenter resourcePresenter;

    [Tooltip("玩家技能栏 Presenter（CD 由 SkillCooldownTicker 在槽位上刷新）。")]
    [SerializeField] SkillBarPresenter skillBarPresenter;

    [Tooltip("玩家 Buff 条 Presenter（订阅 BuffStack 增删 + 刷新剩余时间）。")]
    [SerializeField] BuffStripPresenter buffStripPresenter;

    protected override void OnPropsSet()
    {
        // ① 新路径（推荐）：Props.Player 非空 → 由 Presenters 接管
        if (Props.Player != null)
        {
            if (resourcePresenter != null) resourcePresenter.Bind(Props.Player);
            if (skillBarPresenter != null) skillBarPresenter.Bind(Props.Player);
            if (buffStripPresenter != null) buffStripPresenter.Bind(Props.Player);
            return;
        }

        // ② 兼容旧路径（轮询字段快照）
        if (healthText != null)
        {
            healthText.text = $"{Props.HealthCurrent:0} / {Props.HealthMax:0}";
        }
    }

    void OnDisable()
    {
        if (resourcePresenter != null) resourcePresenter.Unbind();
        if (skillBarPresenter != null) skillBarPresenter.Unbind();
        if (buffStripPresenter != null) buffStripPresenter.Unbind();
    }
}
