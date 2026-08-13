using System;
using UnityEngine;

[Flags]
public enum CombatCapability : ushort
{
    None = 0,
    Damage = 1 << 0,
    Heal = 1 << 1,
    Effect = 1 << 2,
    Impulse = 1 << 3,
    Guardable = 1 << 4,
    Parryable = 1 << 5,
    Clashable = 1 << 6,
    Reflectable = 1 << 7,
    TriggerOnInvincible = 1 << 8,
    AffectDead = 1 << 9,
}

/// <summary>Action 与 Spawned 共用的、裁决前完整接触事实。</summary>
public readonly struct CombatContactFact
{
    public readonly Entity Source;
    public readonly Entity Target;
    public readonly CombatExecutionModel ExecutionModel;
    public readonly CombatCapability Capabilities;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly string BoneName;
    public readonly string EventId;
    public readonly uint ActionLeaseVersion;
    public readonly int SampleId;
    public readonly int HitCountOnTarget;
    public readonly ActionDataSO Action;
    public readonly SpawnedCombatHandle ObjectHandle;
    public readonly ulong RootSpawnId;
    public readonly int DefinitionRevision;
    public readonly float ElapsedSeconds;

    public CombatContactFact(
        Entity source,
        Entity target,
        CombatExecutionModel executionModel,
        CombatCapability capabilities,
        Vector3 point,
        Vector3 normal,
        string boneName,
        string eventId,
        uint actionLeaseVersion,
        int sampleId,
        int hitCountOnTarget,
        ActionDataSO action,
        SpawnedCombatHandle objectHandle,
        ulong rootSpawnId,
        int definitionRevision,
        float elapsedSeconds)
    {
        Source = source;
        Target = target;
        ExecutionModel = executionModel;
        Capabilities = capabilities;
        Point = point;
        Normal = normal;
        BoneName = string.IsNullOrEmpty(boneName) ? "Body" : boneName;
        EventId = eventId ?? string.Empty;
        ActionLeaseVersion = actionLeaseVersion;
        SampleId = sampleId;
        HitCountOnTarget = hitCountOnTarget;
        Action = action;
        ObjectHandle = objectHandle;
        RootSpawnId = rootSpawnId;
        DefinitionRevision = definitionRevision;
        ElapsedSeconds = elapsedSeconds;
    }
}

/// <summary>已解析但尚未提交的效果意图；Heal 与 Damage 使用不同字段。</summary>
public readonly struct CombatOutcomeSet
{
    public readonly float BaseDamage;
    public readonly float HealAmount;
    public readonly EffectDefinitionSO Effect;
    public readonly HitReaction Reaction;

    public CombatOutcomeSet(
        float baseDamage,
        float healAmount,
        EffectDefinitionSO effect,
        in HitReaction reaction)
    {
        BaseDamage = Mathf.Max(0f, baseDamage);
        HealAmount = Mathf.Max(0f, healAmount);
        Effect = effect;
        Reaction = reaction;
    }
}

public readonly struct CombatOutcomeSummary
{
    public readonly CombatInteraction Interaction;
    public readonly bool ApplicationAccepted;
    public readonly bool ConsumedHitBudget;
    public readonly bool RequestsTermination;

    public CombatOutcomeSummary(
        CombatInteraction interaction,
        bool applicationAccepted,
        bool consumedHitBudget,
        bool requestsTermination)
    {
        Interaction = interaction;
        ApplicationAccepted = applicationAccepted;
        ConsumedHitBudget = consumedHitBudget;
        RequestsTermination = requestsTermination;
    }
}

public static class CombatOutcomeBuilder
{
    public static CombatOutcomeSet FromProfile(in CombatOutcomeProfile profile)
    {
        return profile.Kind == CombatOutcomeAuthoringKind.DamageDefinition
            ? FromDamageDefinition(profile.DamageDefinition)
            : FromHitReaction(in profile.Reaction);
    }

    public static CombatOutcomeSet FromHitReaction(in HitReaction reaction)
    {
        return new CombatOutcomeSet(
            reaction.BaseDamage,
            0f,
            reaction.OnHitEffect,
            in reaction);
    }

    public static CombatOutcomeSet FromDamageDefinition(DamageDefinitionSO definition)
    {
        if (definition == null)
        {
            var empty = default(HitReaction);
            return new CombatOutcomeSet(0f, 0f, null, in empty);
        }

        var reaction = new HitReaction
        {
            BaseDamage = definition.Kind is DamageKind.Instant or DamageKind.InstantPlusEffect
                ? definition.Amount
                : 0f,
            ImpulseLocalDir = definition.KnockbackLocalDir,
            ImpulseForce = definition.Kind == DamageKind.Knockback
                ? definition.KnockbackForce
                : 0f,
            LaunchUpSpeed = definition.Kind == DamageKind.Launch
                ? definition.LaunchUpSpeed
                : 0f,
            OnHitEffect = definition.OnHitEffect,
        };
        var heal = definition.Kind == DamageKind.Heal ? definition.Amount : 0f;
        return new CombatOutcomeSet(
            reaction.BaseDamage,
            heal,
            definition.OnHitEffect,
            in reaction);
    }

    public static CombatCapability ResolveCapabilities(
        CombatExecutionModel executionModel,
        CombatObjectArchetype archetype,
        HitShapeMode shapeMode,
        in CombatOutcomeSet outcome)
    {
        var capability = CombatCapability.None;
        if (outcome.BaseDamage > 0f) capability |= CombatCapability.Damage;
        if (outcome.HealAmount > 0f) capability |= CombatCapability.Heal;
        if (outcome.Effect != null) capability |= CombatCapability.Effect;
        if (outcome.Reaction.ImpulseForce > 0f || outcome.Reaction.LaunchUpSpeed > 0f)
            capability |= CombatCapability.Impulse;

        if (executionModel == CombatExecutionModel.ActionWindowBound)
        {
            capability |= CombatCapability.Guardable | CombatCapability.Parryable;
            if (shapeMode == HitShapeMode.WeaponTrace)
            {
                capability |= CombatCapability.Clashable;
            }
        }
        else if (archetype == CombatObjectArchetype.Projectile)
        {
            capability |= CombatCapability.Guardable
                | CombatCapability.Parryable
                | CombatCapability.Reflectable;
        }

        return capability;
    }
}

public interface ISpawnedCombatCandidateSink
{
    void Process(
        SpawnedCombatRuntime runtime,
        in SpawnedCombatSampleFact sample,
        ContactCandidateBuffer candidates);
}
