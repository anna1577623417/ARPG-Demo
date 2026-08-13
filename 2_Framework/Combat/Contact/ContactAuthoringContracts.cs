using UnityEngine;

/// <summary>224.1 L1 — Contact Authoring 解析信息（只读诊断，不参与 Physics）。</summary>
public readonly struct ContactAuthoringResolutionInfo
{
    public readonly bool UsesLegacyAuthoring;
    public readonly string SourcePath;
    public readonly ContactAnchorBindingMode BindingMode;
    public readonly ContactSweepPolicy SweepPolicy;
    public readonly ContactMotionKind LegacyMotion;

    public ContactAuthoringResolutionInfo(
        bool usesLegacyAuthoring,
        string sourcePath,
        ContactAnchorBindingMode bindingMode,
        ContactSweepPolicy sweepPolicy,
        ContactMotionKind legacyMotion)
    {
        UsesLegacyAuthoring = usesLegacyAuthoring;
        SourcePath = sourcePath ?? string.Empty;
        BindingMode = bindingMode;
        SweepPolicy = sweepPolicy;
        LegacyMotion = legacyMotion;
    }
}

/// <summary>
/// Adapter 输出的不可变 Contact 配置。Editor/Runtime 共用；无 UnityEditor 依赖。
/// </summary>
public readonly struct ResolvedContactAuthoringConfig
{
    public readonly CombatObjectDefinitionSO Definition;
    public readonly int DefinitionRevision;
    public readonly ContactAnchorBindingMode BindingMode;
    public readonly ContactSweepPolicy SweepPolicy;
    public readonly ContactOriginPolicy OriginPolicy;
    public readonly ContactAnchorReference Origin;
    public readonly Vector3 LocalPosition;
    public readonly Quaternion LocalRotation;
    public readonly ContactAnchorScalePolicy ScalePolicy;
    public readonly AttackShapePresetSO ShapePreset;
    public readonly HitShapeMode ShapeMode;
    public readonly HitShapeSO Geometry;
    public readonly WeaponSocketSetSO WeaponSockets;
    public readonly WeaponSocketLayoutSO WeaponSocketLayout;
    public readonly ContactQueryPolicy Query;
    public readonly HitPolicyParams HitPolicy;
    public readonly CombatAttackProfile AttackProfile;
    public readonly bool UsesLegacyAuthoring;
    public readonly string LegacyFieldSources;
    public readonly ContactMotionKind LegacyMotion;

    public ResolvedContactAuthoringConfig(
        CombatObjectDefinitionSO definition,
        int definitionRevision,
        ContactAnchorBindingMode bindingMode,
        ContactSweepPolicy sweepPolicy,
        ContactOriginPolicy originPolicy,
        in ContactAnchorReference origin,
        Vector3 localPosition,
        Quaternion localRotation,
        ContactAnchorScalePolicy scalePolicy,
        AttackShapePresetSO shapePreset,
        HitShapeMode shapeMode,
        HitShapeSO geometry,
        WeaponSocketSetSO weaponSockets,
        WeaponSocketLayoutSO weaponSocketLayout,
        in ContactQueryPolicy query,
        in HitPolicyParams hitPolicy,
        in CombatAttackProfile attackProfile,
        bool usesLegacyAuthoring,
        string legacyFieldSources,
        ContactMotionKind legacyMotion)
    {
        Definition = definition;
        DefinitionRevision = definitionRevision;
        BindingMode = bindingMode;
        SweepPolicy = sweepPolicy;
        OriginPolicy = originPolicy;
        Origin = origin;
        LocalPosition = localPosition;
        LocalRotation = localRotation;
        ScalePolicy = scalePolicy;
        ShapePreset = shapePreset;
        ShapeMode = shapeMode;
        Geometry = geometry;
        WeaponSockets = weaponSockets;
        WeaponSocketLayout = weaponSocketLayout;
        Query = query;
        HitPolicy = hitPolicy;
        AttackProfile = attackProfile;
        UsesLegacyAuthoring = usesLegacyAuthoring;
        LegacyFieldSources = legacyFieldSources ?? string.Empty;
        LegacyMotion = legacyMotion;
    }
}

/// <summary>Legacy ContactOverride 的只读 Adapter 输入包装。</summary>
public readonly struct LegacyContactOverrideAdapter
{
    public readonly bool OverridePlacement;
    public readonly SpawnSource Origin;
    public readonly Vector3 LocalOffset;
    public readonly Vector3 LocalEuler;
    public readonly bool OverrideMotion;
    public readonly ContactMotionKind Motion;

    public LegacyContactOverrideAdapter(in ContactOverrideData data)
    {
        OverridePlacement = data.OverridePlacement;
        Origin = data.Origin;
        LocalOffset = data.LocalOffset;
        LocalEuler = data.LocalEuler;
        OverrideMotion = data.OverrideMotion;
        Motion = data.Motion;
    }

    public static LegacyContactOverrideAdapter From(in ContactOverrideData data) =>
        new LegacyContactOverrideAdapter(in data);

    public static LegacyContactOverrideAdapter Empty => default;
}
