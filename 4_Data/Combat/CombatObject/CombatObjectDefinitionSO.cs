using UnityEngine;

/// <summary>
/// 188.3 W6 — CombatObject 主资产（5 维笛卡尔积：Shape × Movement × Damage × Lifecycle × TargetFilter）。
/// <para>策划新做技能 = 新建本 SO 资产 + 选既有 Shape/Movement/Damage/Filter 组合，0 行代码。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Combat/Combat Object Definition", fileName = "CombatObject_")]
public sealed class CombatObjectDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string Id;
    public string DisplayName;
    [TextArea] public string Description;

    [Header("Shape (188.3 W1 复用 4 子类，可视化已就绪)")]
    [Tooltip("判定形状：Sphere/Box/Capsule/Cone。可在 Inspector 单独 Preview。")]
    public HitShapeSO Shape;

    [Header("Movement (188.3 W4)")]
    public MovementParams Movement;

    [Header("Damage (188.3 W5)")]
    public DamageDefinitionSO Damage;

    [Header("Lifecycle (188.3 W5)")]
    public LifecycleParams Lifecycle;

    [Header("Target Filter (214.4)")]
    [Tooltip("内嵌枚举过滤；Layer 物理查询见 QueryLayerMask。")]
    public TargetFilterParams TargetFilter;

    [Header("Spawn (188.3 §3.2)")]
    public SpawnSource SpawnSource = SpawnSource.SelfRootBone;

    [Tooltip("相对 SpawnSource 的局部偏移（米）。")]
    public Vector3 LocalOffset;

    [Tooltip("相对 SpawnSource 的局部欧拉旋转（度）。")]
    public Vector3 LocalEulerOffset;

    [Header("Physics Query")]
    [Tooltip("Overlap 查询的 LayerMask。0=全部层。")]
    public LayerMask QueryLayerMask = ~0;

    /// <summary>验证：必填字段检查（运行时 Spawn 前可调用）。</summary>
    public bool IsValid(out string reason)
    {
        if (Shape == null) { reason = "Shape is null"; return false; }
        if (Damage == null) { reason = "Damage is null"; return false; }
        if (Lifecycle.MaxHitsPerTarget < 1) { reason = "MaxHitsPerTarget < 1"; return false; }
        if (Lifecycle.MaxTargets < 1) { reason = "MaxTargets < 1"; return false; }
        reason = null;
        return true;
    }
}
