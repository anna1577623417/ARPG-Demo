#if UNITY_EDITOR
using UnityEngine;

internal readonly struct ContactPreviewState
{
    public readonly ContactAuthoringSelection Selection;
    public readonly ContactEvent Event;
    public readonly ResolvedContactSpec Spec;
    public readonly Transform Basis;
    public readonly Vector3 WorldPosition;
    public readonly Quaternion WorldRotation;
    public readonly bool IsActiveAtPreviewTime;
    public readonly ResolvedContactPose Pose;

    public ContactPreviewState(
        in ContactAuthoringSelection selection,
        in ContactEvent contactEvent,
        in ResolvedContactSpec spec,
        Transform basis,
        in ResolvedContactPose pose,
        bool isActiveAtPreviewTime)
    {
        Selection = selection;
        Event = contactEvent;
        Spec = spec;
        Basis = basis;
        Pose = pose;
        WorldPosition = pose.Position;
        WorldRotation = pose.Rotation;
        IsActiveAtPreviewTime = isActiveAtPreviewTime;
    }
}

internal static class ContactPreviewResolver
{
    public static bool TryResolve(
        Transform previewAnchor,
        out ContactPreviewState state,
        out string failure)
    {
        state = default;
        failure = null;
        if (!ContactAuthoringSelectionContext.TryGet(out var selection))
        {
            failure = "No ContactEvent selection.";
            return false;
        }

        if (previewAnchor == null)
        {
            failure = "No preview anchor.";
            return false;
        }

        if (!TryFindEvent(selection.Action, selection.EventId, out var contactEvent))
        {
            failure = "Selected EventId no longer exists.";
            return false;
        }

        if (!CombatObjectSpecResolver.TryResolveContact(
                contactEvent.Definition,
                in contactEvent.Override,
                out var spec,
                out var validation))
        {
            failure = validation.FirstErrorOrNull();
            return false;
        }

        var windowStart = Mathf.Min(contactEvent.ActiveStart, contactEvent.ActiveEnd);
        var end = Mathf.Max(contactEvent.ActiveStart, contactEvent.ActiveEnd);

        // L3：无副作用采样 API 尚未完整接入 Preview Controller。
        // Static 的 SourceNormalizedTime 标记为 windowStart；骨骼仍取当前预览姿态。
        // 真正“采样到 Window Start 动画再恢复”留后续补齐（见 224.2 OPEN）。
        var basis = ResolveBasis(previewAnchor, spec.Origin);
        var anchor = new ContactAnchorPose(
            basis.position,
            basis.rotation,
            basis.lossyScale,
            basis.name);
        var pose = ContactPoseResolver.ResolveForPreview(
            in spec,
            in anchor,
            windowStart,
            selection.PreviewTime);

        state = new ContactPreviewState(
            in selection,
            in contactEvent,
            in spec,
            basis,
            in pose,
            selection.PreviewTime >= windowStart && selection.PreviewTime <= end);
        return true;
    }

    public static bool TryFindEvent(ActionDataSO action, string eventId, out ContactEvent contactEvent)
    {
        contactEvent = default;
        if (action?.ContactEvents == null || !ContactEventId.IsValid(eventId)) return false;
        for (var i = 0; i < action.ContactEvents.Count; i++)
        {
            if (action.ContactEvents[i].EventId != eventId) continue;
            contactEvent = action.ContactEvents[i];
            return true;
        }

        return false;
    }

    public static int FindEventIndex(ActionDataSO action, string eventId)
    {
        if (action?.ContactEvents == null) return -1;
        for (var i = 0; i < action.ContactEvents.Count; i++)
        {
            if (action.ContactEvents[i].EventId == eventId) return i;
        }

        return -1;
    }

    static Transform ResolveBasis(Transform anchor, SpawnSource origin)
    {
        return CombatSpawnBoneResolver.TryGetBoneTransform(anchor, origin, out var bone)
            ? bone
            : anchor;
    }
}
#endif
