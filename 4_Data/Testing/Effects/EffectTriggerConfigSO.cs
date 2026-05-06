using UnityEngine;

[CreateAssetMenu(menuName = "Testing/Combat/Effect Trigger Config", fileName = "EffectTriggerConfig")]
public class EffectTriggerConfigSO : ScriptableObject
{
    [Header("Trigger Rules | 触发规则")]
    [Tooltip("triggerMode（触发模式）：进入触发 / 间隔触发 / 驻留触发。")]
    public TriggerMode triggerMode = TriggerMode.OnEnter;

    [Tooltip("interval（间隔秒）：Interval 模式每次触发的最小时间间隔。")]
    public float interval = 1f;

    [Tooltip("triggerOnce（仅触发一次）：true 常用于地雷/爆炸桶。")]
    public bool triggerOnce;

    [Tooltip("activationDelay（激活延迟秒）：用于延迟爆炸等预警型触发器。")]
    public float activationDelay;

    [Header("Faction Filter | 阵营过滤")]
    [Tooltip("factionFilter（过滤模式）：敌方/友方/全部。")]
    public FactionFilterMode factionFilter = FactionFilterMode.AffectAll;

    [Tooltip("affectedFactions（受影响阵营）：用于敌我筛选。")]
    public FactionTag[] affectedFactions = { FactionTag.Enemy };

    [Header("Effects | 效果")]
    [Tooltip("effects（效果列表）：触发后按顺序投递到 EffectSystem。")]
    public EffectDataSO[] effects;

    [Header("Lifecycle | 生命周期")]
    [Tooltip("lifetime（持续秒）：0 表示永久。")]
    public float lifetime;

    [Tooltip("destroyAfterTrigger（触发后失活）：用于一次性机关。")]
    public bool destroyAfterTrigger;
}

public enum TriggerMode
{
    OnEnter,
    Interval,
    OnStay
}

public enum FactionFilterMode
{
    AffectEnemies,
    AffectAllies,
    AffectAll
}
