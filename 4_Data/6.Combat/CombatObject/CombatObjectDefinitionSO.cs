using UnityEngine;

/// <summary>
/// 188.3 W6 — CombatObject 主资产（5 维笛卡尔积：Shape × Movement × Damage × Lifecycle × TargetFilter）。
/// <para>策划新做技能 = 新建本 SO 资产 + 选既有 Shape/Movement/Damage/Filter 组合，0 行代码。</para>
/// </summary>
public sealed class CombatObjectDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string Id;
    public string DisplayName;
    [TextArea] public string Description;

    [Header("223.4 Schema")]
    [Tooltip("零值为未分类旧资产；必须显式迁移后才能进入 ContactEvent 或 SpawnRequest 新入口。")]
    public CombatObjectArchetype Archetype = CombatObjectArchetype.UnclassifiedLegacy;

    public CombatObjectSchemaVersion SchemaVersion = CombatObjectSchemaVersion.Legacy;
    public CombatObjectMigrationState MigrationState = CombatObjectMigrationState.RequiresReview;

    [Tooltip("作者数据实质变化时递增；运行时 Spec 记录该值用于诊断。")]
    [Min(0)] public int DefinitionRevision;

    [Header("Action Contact (223.4)")]
    [Tooltip("ActionContact 的几何、默认摆放与 Motion 真相。旧 Shape 字段仅供 Legacy/Spawned 迁移。")]
    public AttackShapePresetSO ShapePreset;

    [Tooltip("224.1 — ActionContact 唯一空间作者数据。UseExplicitData=false 时仍走 Preset/Override Adapter。")]
    public ActionContactAuthoringData ActionContactAuthoring;

    public CombatAttackProfile AttackProfile;
    public ContactQueryPolicy QueryPolicy;
    public HitPolicyParams HitPolicy;

    [Header("Legacy / Spawned Compatibility")]
    [Header("Shape (188.3 W1 复用 4 子类，可视化已就绪)")]
    [Tooltip("判定形状：Sphere/Box/Capsule/Cone。可在 Inspector 单独 Preview。")]
    public HitShapeSO Shape;

    [Header("Movement (188.3 W4)")]
    public MovementParams Movement;

    [Header("Damage (188.3 W5)")]
    public DamageDefinitionSO Damage;

    [Header("Lifecycle (188.3 W5)")]
    public LifecycleParams Lifecycle;

    [Header("Spawned Runtime Policy (223.4)")]
    [Tooltip("新资产应启用显式策略。旧 Lifecycle 只在 Spec Resolver 中做确定性映射。")]
    public SpawnedRuntimePolicyAuthoring SpawnedPolicy;
    public SpawnedSpatialPolicyAuthoring SpatialPolicy;

    [Header("Spawned V2 Authoring")]
    [Tooltip("ArchetypeV2 Spawned Definition 的唯一新作者数据；启用后 Resolver 不读取 Legacy 字段。")]
    public SpawnedCombatAuthoringData SpawnedData;

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
        // Legacy Runtime 兼容入口只做 Intrinsic 检查；新入口必须调用带 UseSite 的 Validator。
        var validation = CombatObjectDefinitionValidator.Validate(
            this,
            CombatDefinitionUseSite.Intrinsic);
        reason = validation.FirstErrorOrNull();
        return validation.IsValid;
    }
}
