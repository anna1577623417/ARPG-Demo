#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Origin/Binding 切换时的位姿保持策略。</summary>
public enum ContactOriginChangeMode : byte
{
    KeepLocalPose = 0,
    KeepWorldPose = 1,
    UseDefaultPose = 2,
}

/// <summary>L2 Preview 上下文；无上下文时 KeepWorldPose 不可用。</summary>
public interface IContactAuthoringPreviewContext
{
    bool TryResolveAnchorWorld(
        in ContactAnchorReference origin,
        out Vector3 position,
        out Quaternion rotation);
}

/// <summary>
/// 224.1 L2 — ActionContact CO 空间事务唯一写入口。
/// Inspector/Timeline/Scene 不得自行散落 Undo.RecordObject。
/// </summary>
public static class CombatObjectAuthoringService
{
    public static bool TryEnsureExplicitAuthoring(
        CombatObjectDefinitionSO definition,
        out string failure)
    {
        failure = null;
        if (definition == null)
        {
            failure = "Definition is null.";
            return false;
        }

        if (definition.Archetype != CombatObjectArchetype.ActionContact)
        {
            failure = "Only ActionContact definitions accept ActionContactAuthoring writes.";
            return false;
        }

        var data = definition.ActionContactAuthoring;
        if (data.UseExplicitData
            && data.Version == ActionContactAuthoringVersion.CombatObjectSingleSourceV1)
        {
            return true;
        }

        BeginEdit(definition, "Enable ActionContact Explicit Authoring");
        data = ActionContactAuthoringData.CreateNewV1();
        // 不从 Preset 自动拷贝：避免暗改语义；作者需显式确认或后续迁移周期处理。
        definition.ActionContactAuthoring = data;
        BumpRevision(definition);
        EndEdit(definition, CombatAuthoringChangeKind.Migration);
        return true;
    }

    public static bool TryChangeBinding(
        CombatObjectDefinitionSO definition,
        ContactAnchorBindingMode nextMode,
        ContactOriginChangeMode rebaseMode,
        IContactAuthoringPreviewContext context,
        out string failure)
    {
        failure = null;
        if (!TryEnsureExplicitAuthoring(definition, out failure)) return false;

        var data = definition.ActionContactAuthoring;
        if (data.BindingMode == nextMode) return true;

        if (nextMode == ContactAnchorBindingMode.StaticAtWindowStart
            && data.SweepPolicy == ContactSweepPolicy.BetweenSamples)
        {
            failure = "StaticAtWindowStart cannot keep BetweenSamples; set Sweep=None first.";
            return false;
        }

        BeginEdit(definition, "Change Contact Binding");
        var previousOrigin = data.Origin;
        var previousLocal = data.LocalPosition;
        var previousEuler = data.LocalEuler;

        if (data.OriginPolicy == ContactOriginPolicy.Explicit)
        {
            data.RememberExplicitOrigin(data.BindingMode, data.Origin);
        }

        data.BindingMode = nextMode;
        if (data.OriginPolicy == ContactOriginPolicy.Auto)
        {
            data.Origin = nextMode == ContactAnchorBindingMode.FollowAnchor
                ? ContactAnchorReference.DefaultFollow
                : ContactAnchorReference.DefaultStatic;
        }
        else
        {
            var remembered = nextMode == ContactAnchorBindingMode.FollowAnchor
                ? data.LastExplicitFollowOrigin
                : data.LastExplicitStaticOrigin;
            data.Origin = remembered.Source != default
                ? remembered
                : (nextMode == ContactAnchorBindingMode.FollowAnchor
                    ? ContactAnchorReference.DefaultFollow
                    : ContactAnchorReference.DefaultStatic);
        }

        if (rebaseMode == ContactOriginChangeMode.UseDefaultPose)
        {
            data.LocalPosition = Vector3.zero;
            data.LocalEuler = Vector3.zero;
        }
        else if (rebaseMode == ContactOriginChangeMode.KeepWorldPose)
        {
            if (!TryRebaseLocalFromWorld(
                    previousOrigin,
                    previousLocal,
                    previousEuler,
                    data.Origin,
                    context,
                    out var nextLocal,
                    out var nextEuler,
                    out failure))
            {
                EndEdit(definition, CombatAuthoringChangeKind.ContactBinding);
                return false;
            }

            data.LocalPosition = nextLocal;
            data.LocalEuler = nextEuler;
        }

        definition.ActionContactAuthoring = data;
        BumpRevision(definition);
        EndEdit(definition, CombatAuthoringChangeKind.ContactBinding);
        return true;
    }

    public static bool TryChangeOrigin(
        CombatObjectDefinitionSO definition,
        ContactAnchorReference nextOrigin,
        ContactOriginChangeMode rebaseMode,
        IContactAuthoringPreviewContext context,
        out string failure)
    {
        failure = null;
        if (!TryEnsureExplicitAuthoring(definition, out failure)) return false;

        BeginEdit(definition, "Change Contact Origin");
        var data = definition.ActionContactAuthoring;
        var previousOrigin = data.Origin;
        var previousLocal = data.LocalPosition;
        var previousEuler = data.LocalEuler;

        data.OriginPolicy = ContactOriginPolicy.Explicit;
        data.Origin = nextOrigin;
        data.RememberExplicitOrigin(data.BindingMode, nextOrigin);

        if (rebaseMode == ContactOriginChangeMode.UseDefaultPose)
        {
            data.LocalPosition = Vector3.zero;
            data.LocalEuler = Vector3.zero;
        }
        else if (rebaseMode == ContactOriginChangeMode.KeepWorldPose)
        {
            if (!TryRebaseLocalFromWorld(
                    previousOrigin,
                    previousLocal,
                    previousEuler,
                    nextOrigin,
                    context,
                    out var nextLocal,
                    out var nextEuler,
                    out failure))
            {
                EndEdit(definition, CombatAuthoringChangeKind.ContactConfig);
                return false;
            }

            data.LocalPosition = nextLocal;
            data.LocalEuler = nextEuler;
        }

        definition.ActionContactAuthoring = data;
        BumpRevision(definition);
        EndEdit(definition, CombatAuthoringChangeKind.ContactConfig);
        return true;
    }

    public static bool TryChangeLocalPose(
        CombatObjectDefinitionSO definition,
        Vector3 localPosition,
        Vector3 localEuler,
        out string failure)
    {
        failure = null;
        if (!TryEnsureExplicitAuthoring(definition, out failure)) return false;

        BeginEdit(definition, "Change Contact Local Pose");
        var data = definition.ActionContactAuthoring;
        data.LocalPosition = localPosition;
        data.LocalEuler = localEuler;
        definition.ActionContactAuthoring = data;
        BumpRevision(definition);
        EndEdit(definition, CombatAuthoringChangeKind.ContactConfig);
        return true;
    }

    public static bool TryChangeLocalPoseFromWorldHandle(
        CombatObjectDefinitionSO definition,
        Vector3 anchorPosition,
        Quaternion anchorRotation,
        Vector3 nextWorldPosition,
        Quaternion nextWorldRotation,
        out string failure)
    {
        failure = null;
        if (!TryEnsureExplicitAuthoring(definition, out failure)) return false;

        ContactPlacementMath.ResolveLocal(
            anchorPosition,
            anchorRotation,
            nextWorldPosition,
            nextWorldRotation,
            out var localOffset,
            out var localRotation);

        return TryChangeLocalPose(
            definition,
            localOffset,
            localRotation.eulerAngles,
            out failure);
    }

    public static bool TryChangeSweepPolicy(
        CombatObjectDefinitionSO definition,
        ContactSweepPolicy next,
        out string failure)
    {
        failure = null;
        if (!TryEnsureExplicitAuthoring(definition, out failure)) return false;

        var data = definition.ActionContactAuthoring;
        if (data.BindingMode == ContactAnchorBindingMode.StaticAtWindowStart
            && next == ContactSweepPolicy.BetweenSamples)
        {
            failure = "StaticAtWindowStart cannot use BetweenSamples.";
            return false;
        }

        BeginEdit(definition, "Change Contact Sweep Policy");
        data.SweepPolicy = next;
        definition.ActionContactAuthoring = data;
        BumpRevision(definition);
        EndEdit(definition, CombatAuthoringChangeKind.ContactConfig);
        return true;
    }

    public static CombatObjectDefinitionSO DuplicateForContact(
        CombatObjectDefinitionSO source,
        ActionDataSO action,
        string eventId)
    {
        if (source == null) return null;

        var copy = Object.Instantiate(source);
        copy.name = $"{source.name}_Variant";
        copy.Id = $"{source.Id}_{ContactEventId.NewId().Substring(0, 8)}";
        copy.DisplayName = string.IsNullOrEmpty(source.DisplayName)
            ? copy.name
            : $"{source.DisplayName} Variant";
        copy.DefinitionRevision = 1;
        if (!copy.ActionContactAuthoring.UseExplicitData)
        {
            copy.ActionContactAuthoring = ActionContactAuthoringData.CreateNewV1();
        }

        var folder = "Assets";
        var sourcePath = AssetDatabase.GetAssetPath(source);
        if (!string.IsNullOrEmpty(sourcePath))
        {
            folder = System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? folder;
        }

        var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{copy.name}.asset");
        AssetDatabase.CreateAsset(copy, path);
        AssetDatabase.SaveAssets();
        CombatObjectReferenceIndex.Invalidate();
        CombatAuthoringChangeBus.PublishContactConfig(copy, CombatAuthoringChangeKind.Migration);
        return copy;
    }

    static bool TryRebaseLocalFromWorld(
        in ContactAnchorReference previousOrigin,
        Vector3 previousLocal,
        Vector3 previousEuler,
        in ContactAnchorReference nextOrigin,
        IContactAuthoringPreviewContext context,
        out Vector3 nextLocal,
        out Vector3 nextEuler,
        out string failure)
    {
        nextLocal = previousLocal;
        nextEuler = previousEuler;
        failure = null;
        if (context == null
            || !context.TryResolveAnchorWorld(previousOrigin, out var oldAnchorPos, out var oldAnchorRot)
            || !context.TryResolveAnchorWorld(nextOrigin, out var newAnchorPos, out var newAnchorRot))
        {
            failure = "KeepWorldPose requires a resolvable preview anchor context.";
            return false;
        }

        ContactPlacementMath.ResolveWorld(
            oldAnchorPos,
            oldAnchorRot,
            previousLocal,
            Quaternion.Euler(previousEuler),
            out var worldPos,
            out var worldRot);
        ContactPlacementMath.ResolveLocal(
            newAnchorPos,
            newAnchorRot,
            worldPos,
            worldRot,
            out nextLocal,
            out var nextLocalRot);
        nextEuler = nextLocalRot.eulerAngles;
        return true;
    }

    static void BeginEdit(CombatObjectDefinitionSO definition, string undoName)
    {
        Undo.RecordObject(definition, undoName);
    }

    static void BumpRevision(CombatObjectDefinitionSO definition)
    {
        definition.DefinitionRevision = Mathf.Max(0, definition.DefinitionRevision) + 1;
    }

    static void EndEdit(CombatObjectDefinitionSO definition, CombatAuthoringChangeKind kind)
    {
        EditorUtility.SetDirty(definition);
        CombatObjectReferenceIndex.Invalidate();
        CombatAuthoringChangeBus.PublishContactConfig(definition, kind);
    }
}
#endif
