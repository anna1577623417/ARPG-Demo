using UnityEngine;

[CreateAssetMenu(menuName = "Testing/Combat/Projectile Data", fileName = "ProjectileData")]
public class ProjectileDataSO : ScriptableObject
{
    [Header("Movement | 运动")]
    [Tooltip("speed（速度）：增大=飞行更快。")]
    public float speed = 20f;

    [Tooltip("lifetime（生命周期秒）：增大=飞得更久。")]
    public float lifetime = 5f;

    [Tooltip("useGravity（使用重力）：true 时抛物线下落。")]
    public bool useGravity;

    [Tooltip("gravityScale（重力倍率）：增大=下坠更快。")]
    public float gravityScale = 1f;

    [Header("Hit Detection | 命中检测")]
    [Tooltip("hitRadius（命中半径）：增大=判定更宽容。")]
    public float hitRadius = 0.2f;

    [Tooltip("hitLayerMask（命中层）：只检测这些层。")]
    public LayerMask hitLayerMask;

    [Tooltip("piercing（穿透）：开启后命中后不立刻销毁。")]
    public bool piercing;

    [Tooltip("maxPierceCount（最大穿透次数）：增大=可命中更多单位。")]
    public int maxPierceCount = 1;

    [Header("On Hit Effects | 命中效果")]
    [Tooltip("onHitEffects（命中效果列表）：投递到 EffectSystem。")]
    public EffectDataSO[] onHitEffects;
}
