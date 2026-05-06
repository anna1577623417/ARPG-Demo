using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 假人调试 HUD：事件驱动刷新，避免每帧轮询。
/// </summary>
public sealed class CombatDebugHUD : MonoBehaviour
{
    [SerializeField] Text hpText;
    [SerializeField] Text lastDamageText;
    [SerializeField] Image hpFill;

    TestDummy _dummy;

    public void Bind(TestDummy dummy)
    {
        _dummy = dummy;
    }

    void OnEnable()
    {
        GlobalEventBus.Subscribe<DummyDamagedEvent>(OnDummyDamaged);
    }

    void OnDisable()
    {
        GlobalEventBus.Unsubscribe<DummyDamagedEvent>(OnDummyDamaged);
    }

    void OnDummyDamaged(DummyDamagedEvent evt)
    {
        if (_dummy == null || evt.Dummy != _dummy)
        {
            return;
        }

        var maxHp = Mathf.Max(1f, _dummy.Stats.Get(StatType.MaxHealth));
        var curHp = Mathf.Clamp(evt.RemainingHP, 0f, maxHp);

        if (hpText != null)
        {
            hpText.text = $"{curHp:F0} / {maxHp:F0}";
        }

        if (lastDamageText != null)
        {
            lastDamageText.text = evt.Result.IsCritical
                ? $"-{evt.Result.FinalDamage:F1} CRIT"
                : $"-{evt.Result.FinalDamage:F1}";
        }

        if (hpFill != null)
        {
            hpFill.fillAmount = curHp / maxHp;
        }
    }
}
