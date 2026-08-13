using System.Collections.Generic;

public enum CombatInspectorSectionKind : byte
{
    Identity = 0,
    Payload = 1,
    Geometry = 2,
    Placement = 3,
    ActionWindow = 4,
    Lifetime = 5,
    Sampling = 6,
    Motion = 7,
    Guidance = 8,
    Attachment = 9,
    GeometryEvolution = 10,
    Termination = 11,
    Legacy = 12,
}

public enum CombatInspectorPresentationState : byte
{
    Editable = 0,
    ReadOnlyInherited = 1,
    HiddenLegacy = 2,
    Invalid = 3,
    MissingRequired = 4,
}

public readonly struct CombatObjectInspectorProjection
{
    readonly CombatObjectArchetype _archetype;
    readonly CombatArchetypeSchema _schema;
    readonly IReadOnlyList<CombatInspectorSectionKind> _sections;

    public CombatObjectInspectorProjection(
        CombatObjectArchetype archetype,
        in CombatArchetypeSchema schema,
        IReadOnlyList<CombatInspectorSectionKind> sections)
    {
        _archetype = archetype;
        _schema = schema;
        _sections = sections;
    }

    public CombatObjectArchetype Archetype => _archetype;
    public CombatArchetypeSchema Schema => _schema;
    public IReadOnlyList<CombatInspectorSectionKind> Sections => _sections;
    public bool IsLegacy => _archetype == CombatObjectArchetype.UnclassifiedLegacy;

    public bool Allows(CombatFeatureBlock feature) =>
        (_schema.AllowedFeatures & feature) == feature;
}

public static class CombatObjectInspectorProjectionResolver
{
    public static CombatObjectInspectorProjection Resolve(
        CombatObjectDefinitionSO definition)
    {
        var archetype = definition != null
            ? definition.Archetype
            : CombatObjectArchetype.UnclassifiedLegacy;
        var schema = CombatObjectArchetypeSchemaRegistry.Get(archetype);
        var sections = new List<CombatInspectorSectionKind>(10)
        {
            CombatInspectorSectionKind.Identity,
        };

        AddIf(schema, CombatFeatureBlock.AttackProfile, sections, CombatInspectorSectionKind.Payload);
        AddIf(schema, CombatFeatureBlock.Geometry, sections, CombatInspectorSectionKind.Geometry);
        AddIf(schema, CombatFeatureBlock.SpawnPlacement, sections, CombatInspectorSectionKind.Placement);
        AddIf(schema, CombatFeatureBlock.ActionWindow, sections, CombatInspectorSectionKind.ActionWindow);
        AddIf(schema, CombatFeatureBlock.Lifetime, sections, CombatInspectorSectionKind.Lifetime);
        AddIf(schema, CombatFeatureBlock.Sampling, sections, CombatInspectorSectionKind.Sampling);
        AddIf(schema, CombatFeatureBlock.Motion, sections, CombatInspectorSectionKind.Motion);
        AddIf(schema, CombatFeatureBlock.Guidance, sections, CombatInspectorSectionKind.Guidance);
        AddIf(schema, CombatFeatureBlock.Attachment, sections, CombatInspectorSectionKind.Attachment);
        AddIf(schema, CombatFeatureBlock.GeometryEvolution, sections, CombatInspectorSectionKind.GeometryEvolution);
        AddIf(schema, CombatFeatureBlock.TerminationSpawn, sections, CombatInspectorSectionKind.Termination);
        sections.Add(CombatInspectorSectionKind.Legacy);

        return new CombatObjectInspectorProjection(archetype, in schema, sections);
    }

    static void AddIf(
        in CombatArchetypeSchema schema,
        CombatFeatureBlock feature,
        List<CombatInspectorSectionKind> sections,
        CombatInspectorSectionKind section)
    {
        if ((schema.AllowedFeatures & feature) == feature)
        {
            sections.Add(section);
        }
    }
}
