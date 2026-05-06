using UnityEngine;

/// <summary>
/// 旁路桥接：订阅 <see cref="DamageTextRequestedEvent"/> 并调用 <see cref="DamageTextSystem.Spawn"/>。
/// 不改伤害管线与扣血逻辑，仅做表现层注入。
/// </summary>
[DisallowMultipleComponent]
public sealed class DamageTextEventBridge : MonoBehaviour
{
    [SerializeField] DamageTextSystem damageTextSystem;
    [SerializeField] Camera worldCamera;
    [SerializeField] Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color critColor = new Color(1f, 0.9f, 0.2f, 1f);

    void OnEnable()
    {
        GlobalEventBus.Subscribe<DamageTextRequestedEvent>(OnDamageTextRequested);
    }

    void OnDisable()
    {
        GlobalEventBus.Unsubscribe<DamageTextRequestedEvent>(OnDamageTextRequested);
    }

    void OnDamageTextRequested(DamageTextRequestedEvent evt)
    {
        if (damageTextSystem == null)
        {
            return;
        }

        var cam = worldCamera != null ? worldCamera : Camera.main;
        var world = evt.HitPoint + worldOffset;

        // ScreenSpace：投影到屏幕像素；WorldSpace：保持世界坐标
        if (cam != null && damageTextSystem.SpaceMode == DamageTextSpaceMode.ScreenSpace)
        {
            world = cam.WorldToScreenPoint(world);
        }

        var data = new DamageTextData
        {
            Value = Mathf.RoundToInt(evt.FinalDamage),
            WorldPos = world,
            IsCrit = evt.IsCritical,
            Color = evt.IsCritical ? critColor : normalColor,
            StartTime = Time.time
        };

        damageTextSystem.Spawn(data);
    }
}

