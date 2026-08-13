using UnityEngine;

/// <summary>
/// 224.1 L1 — ActionContact 新旧空间数据统一解析。
/// 新 CO UseExplicitData 只读 CO；否则只读 Preset Default + Event Override（开发期 Adapter，非 Runtime 兜底）。
/// </summary>
public static class ContactAuthoringAdapter
{
    public static bool TryResolveContactAuthoring(
        CombatObjectDefinitionSO definition,
        in LegacyContactOverrideAdapter legacyOverride,
        out ResolvedContactAuthoringConfig config,
        out ContactAuthoringResolutionInfo info,
        out CombatDefinitionValidationResult validation)
    {
        config = default;
        info = default;
        validation = CombatObjectDefinitionValidator.Validate(
            definition,
            CombatDefinitionUseSite.ContactEvent);
        if (definition == null || !validation.IsValid)
        {
            return false;
        }

        if (definition.Archetype != CombatObjectArchetype.ActionContact)
        {
            validation.Add(new CombatValidationIssue(
                "CO.USESITE.INVALID",
                CombatValidationSeverity.Error,
                CombatDefinitionUseSite.ContactEvent,
                "Archetype",
                $"{definition.Archetype} cannot resolve as ActionContact authoring."));
            return false;
        }

        var authoring = definition.ActionContactAuthoring;
        if (authoring.UseExplicitData)
        {
            return TryResolveExplicit(
                definition,
                in authoring,
                in legacyOverride,
                out config,
                out info,
                validation);
        }

        return TryResolveLegacyPresetOverride(
            definition,
            in legacyOverride,
            out config,
            out info,
            validation);
    }

    public static void MapLegacyMotion(
        ContactMotionKind motion,
        out ContactAnchorBindingMode binding,
        out ContactSweepPolicy sweep)
    {
        switch (motion)
        {
            case ContactMotionKind.StaticAtSpawn:
                binding = ContactAnchorBindingMode.StaticAtWindowStart;
                sweep = ContactSweepPolicy.None;
                break;
            case ContactMotionKind.FollowAnchor:
                binding = ContactAnchorBindingMode.FollowAnchor;
                sweep = ContactSweepPolicy.None;
                break;
            case ContactMotionKind.SweepBetweenFrames:
            default:
                // 当前 Runtime 每帧取 Anchor 再 Sweep，必须保留 Follow。
                binding = ContactAnchorBindingMode.FollowAnchor;
                sweep = ContactSweepPolicy.BetweenSamples;
                break;
        }
    }

    public static ContactMotionKind ToLegacyMotion(
        ContactAnchorBindingMode binding,
        ContactSweepPolicy sweep)
    {
        if (binding == ContactAnchorBindingMode.StaticAtWindowStart)
        {
            return ContactMotionKind.StaticAtSpawn;
        }

        return sweep == ContactSweepPolicy.BetweenSamples
            ? ContactMotionKind.SweepBetweenFrames
            : ContactMotionKind.FollowAnchor;
    }

    static bool TryResolveExplicit(
        CombatObjectDefinitionSO definition,
        in ActionContactAuthoringData authoring,
        in LegacyContactOverrideAdapter legacyOverride,
        out ResolvedContactAuthoringConfig config,
        out ContactAuthoringResolutionInfo info,
        CombatDefinitionValidationResult validation)
    {
        config = default;
        info = default;

        if (authoring.Version != ActionContactAuthoringVersion.CombatObjectSingleSourceV1)
        {
            validation.Add(new CombatValidationIssue(
                "CO.CONTACT.AUTHORING.MISSING",
                CombatValidationSeverity.Error,
                CombatDefinitionUseSite.ContactEvent,
                "ActionContactAuthoring.Version",
                "UseExplicitData requires CombatObjectSingleSourceV1."));
            return false;
        }

        if (authoring.BindingMode == ContactAnchorBindingMode.StaticAtWindowStart
            && authoring.SweepPolicy == ContactSweepPolicy.BetweenSamples)
        {
            validation.Add(new CombatValidationIssue(
                "CO.CONTACT.SWEEP.STATIC_CONFLICT",
                CombatValidationSeverity.Error,
                CombatDefinitionUseSite.ContactEvent,
                "ActionContactAuthoring.SweepPolicy",
                "StaticAtWindowStart cannot combine with BetweenSamples sweep."));
            return false;
        }

        if (legacyOverride.OverridePlacement || legacyOverride.OverrideMotion)
        {
            validation.Add(new CombatValidationIssue(
                "CO.CONTACT.LEGACY.MIXED_WRITABLE_SOURCE",
                CombatValidationSeverity.Error,
                CombatDefinitionUseSite.ContactEvent,
                "ContactEvent.Override",
                "Explicit CO authoring cannot mix writable Event Override placement/motion."));
            return false;
        }

        var preset = definition.ShapePreset;
        if (preset == null)
        {
            validation.Add(new CombatValidationIssue(
                "CO.CONTACT.PRESET.NULL",
                CombatValidationSeverity.Error,
                CombatDefinitionUseSite.ContactEvent,
                "ShapePreset",
                "ActionContact requires ShapePreset for geometry bundle."));
            return false;
        }

        var origin = ResolveExplicitOrigin(in authoring);
        var legacyMotion = ToLegacyMotion(authoring.BindingMode, authoring.SweepPolicy);
        info = new ContactAuthoringResolutionInfo(
            usesLegacyAuthoring: false,
            sourcePath: "CombatObject.ActionContactAuthoring",
            authoring.BindingMode,
            authoring.SweepPolicy,
            legacyMotion);
        config = new ResolvedContactAuthoringConfig(
            definition,
            definition.DefinitionRevision,
            authoring.BindingMode,
            authoring.SweepPolicy,
            authoring.OriginPolicy,
            in origin,
            authoring.LocalPosition,
            Quaternion.Euler(authoring.LocalEuler),
            authoring.ScalePolicy,
            preset,
            preset.ShapeMode,
            preset.Geometry,
            preset.WeaponSockets,
            preset.WeaponSocketLayout,
            in definition.QueryPolicy,
            in definition.HitPolicy,
            in definition.AttackProfile,
            usesLegacyAuthoring: false,
            legacyFieldSources: string.Empty,
            legacyMotion);
        return true;
    }

    static bool TryResolveLegacyPresetOverride(
        CombatObjectDefinitionSO definition,
        in LegacyContactOverrideAdapter legacyOverride,
        out ResolvedContactAuthoringConfig config,
        out ContactAuthoringResolutionInfo info,
        CombatDefinitionValidationResult validation)
    {
        config = default;
        info = default;
        var preset = definition.ShapePreset;
        if (preset == null)
        {
            validation.Add(new CombatValidationIssue(
                "CO.CONTACT.PRESET.NULL",
                CombatValidationSeverity.Error,
                CombatDefinitionUseSite.ContactEvent,
                "ShapePreset",
                "Legacy ActionContact requires ShapePreset defaults."));
            return false;
        }

        validation.Add(new CombatValidationIssue(
            "CO.CONTACT.LEGACY.MIGRATION_REQUIRED",
            CombatValidationSeverity.Warning,
            CombatDefinitionUseSite.ContactEvent,
            "ActionContactAuthoring",
            "Contact still resolves from Preset Default + Event Override; migrate to CO single source."));

        var originSource = legacyOverride.OverridePlacement ? legacyOverride.Origin : preset.DefaultOrigin;
        var localOffset = legacyOverride.OverridePlacement ? legacyOverride.LocalOffset : preset.DefaultLocalOffset;
        var localEuler = legacyOverride.OverridePlacement ? legacyOverride.LocalEuler : preset.DefaultLocalEuler;
        var motion = legacyOverride.OverrideMotion ? legacyOverride.Motion : preset.DefaultMotion;
        MapLegacyMotion(motion, out var binding, out var sweep);

        var sourcePath = legacyOverride.OverridePlacement || legacyOverride.OverrideMotion
            ? "AttackShapePreset.Default* + ContactEvent.Override"
            : "AttackShapePreset.Default*";
        var origin = ContactAnchorReference.FromSpawnSource(originSource);
        info = new ContactAuthoringResolutionInfo(
            usesLegacyAuthoring: true,
            sourcePath,
            binding,
            sweep,
            motion);
        config = new ResolvedContactAuthoringConfig(
            definition,
            definition.DefinitionRevision,
            binding,
            sweep,
            ContactOriginPolicy.Explicit,
            in origin,
            localOffset,
            Quaternion.Euler(localEuler),
            ContactAnchorScalePolicy.IgnoreAnchorScale,
            preset,
            preset.ShapeMode,
            preset.Geometry,
            preset.WeaponSockets,
            preset.WeaponSocketLayout,
            in definition.QueryPolicy,
            in definition.HitPolicy,
            in definition.AttackProfile,
            usesLegacyAuthoring: true,
            legacyFieldSources: sourcePath,
            motion);
        return true;
    }

    static ContactAnchorReference ResolveExplicitOrigin(in ActionContactAuthoringData authoring)
    {
        if (authoring.OriginPolicy == ContactOriginPolicy.Explicit)
        {
            return authoring.Origin;
        }

        return authoring.BindingMode == ContactAnchorBindingMode.FollowAnchor
            ? ContactAnchorReference.DefaultFollow
            : ContactAnchorReference.DefaultStatic;
    }
}
