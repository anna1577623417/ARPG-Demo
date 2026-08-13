using UnityEngine;

/// <summary>Physics 粗筛之后、HitPolicy 之前的一条标准化候选。</summary>
public readonly struct ContactCandidate
{
    public readonly Collider Collider;
    public readonly Entity Target;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly string BoneName;

    public ContactCandidate(
        Collider collider,
        Entity target,
        Vector3 point,
        Vector3 normal,
        string boneName)
    {
        Collider = collider;
        Target = target;
        Point = point;
        Normal = normal;
        BoneName = string.IsNullOrEmpty(boneName) ? "Body" : boneName;
    }
}

/// <summary>
/// 一次 Action Contact 被 HitPolicy 接受后的完整事实。
/// 该结构随 HitResult 穿过现有 Resolver/Event 管线，为统一 Outcome Landing 保留来源身份。
/// </summary>
public readonly struct ContactFact
{
    public readonly Entity Source;
    public readonly Entity Target;
    public readonly ActionDataSO Action;
    public readonly int ActionInstanceId;
    public readonly string ActionName;
    public readonly string ContactEventId;
    public readonly uint ActionLeaseVersion;
    public readonly int SampleId;
    public readonly int DefinitionRevision;
    public readonly HitShapeMode ShapeMode;
    public readonly ContactMotionKind Motion;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly string BoneName;
    public readonly int HitCountOnTarget;
    public readonly float ElapsedSec;

    public ContactFact(
        Entity source,
        Entity target,
        ActionDataSO action,
        string contactEventId,
        uint actionLeaseVersion,
        int sampleId,
        int definitionRevision,
        HitShapeMode shapeMode,
        ContactMotionKind motion,
        Vector3 point,
        Vector3 normal,
        string boneName,
        int hitCountOnTarget,
        float elapsedSec)
    {
        Source = source;
        Target = target;
        Action = action;
        ActionInstanceId = action != null ? action.GetInstanceID() : 0;
        ActionName = action != null ? action.name : "(no-action)";
        ContactEventId = contactEventId ?? string.Empty;
        ActionLeaseVersion = actionLeaseVersion;
        SampleId = sampleId;
        DefinitionRevision = definitionRevision;
        ShapeMode = shapeMode;
        Motion = motion;
        Point = point;
        Normal = normal;
        BoneName = string.IsNullOrEmpty(boneName) ? "Body" : boneName;
        HitCountOnTarget = hitCountOnTarget;
        ElapsedSec = elapsedSec;
    }

    public bool IsValid =>
        Source != null
        && Target != null
        && Action != null
        && !string.IsNullOrEmpty(ContactEventId);
}

/// <summary>ContactFact 到现有 Resolver 输入的唯一适配器。</summary>
public static class ContactFactHitResultAdapter
{
    public static HitResult ToHitResult(in ContactFact fact)
    {
        var emptyOutcome = default(CombatOutcomeSet);
        var capabilities = CombatOutcomeBuilder.ResolveCapabilities(
            CombatExecutionModel.ActionWindowBound,
            CombatObjectArchetype.ActionContact,
            fact.ShapeMode,
            in emptyOutcome)
            | CombatCapability.Damage
            | CombatCapability.Effect
            | CombatCapability.Impulse;
        var unified = new CombatContactFact(
            fact.Source,
            fact.Target,
            CombatExecutionModel.ActionWindowBound,
            capabilities,
            fact.Point,
            fact.Normal,
            fact.BoneName,
            fact.ContactEventId,
            fact.ActionLeaseVersion,
            fact.SampleId,
            fact.HitCountOnTarget,
            fact.Action,
            default,
            0UL,
            fact.DefinitionRevision,
            fact.ElapsedSec);
        return new HitResult(in unified, in fact, fact.ContactEventId);
    }
}

public enum ActionAttackTrackKind : byte
{
    None = 0,
    Contact = 1,
}

/// <summary>Action 攻击主轨选择器。运行时只接受 ActionContact；空数据明确表示没有攻击判定。</summary>
public static class ActionAttackTrackRuntimePolicy
{
    public static ActionAttackTrackKind Select(ActionDataSO action)
    {
        if (action == null)
        {
            return ActionAttackTrackKind.None;
        }

        if (action.ContactEvents != null && action.ContactEvents.Count > 0)
        {
            return ActionAttackTrackKind.Contact;
        }

        return ActionAttackTrackKind.None;
    }
}
