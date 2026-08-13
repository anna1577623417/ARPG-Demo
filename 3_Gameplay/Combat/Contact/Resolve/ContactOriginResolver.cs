using UnityEngine;

/// <summary>ResolvedContactSpec 的运行时 Anchor 查询；完整 Binding Pose 由 ContactPoseResolver 负责。</summary>
public static class ContactOriginResolver
{
    public static void Resolve(
        Entity source,
        in ResolvedContactSpec spec,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        var anchor = ContactPoseResolver.ResolveRuntimeAnchor(source, spec.Origin);
        var pose = ContactPoseResolver.Compose(
            in anchor,
            spec.LocalOffset,
            spec.LocalRotation,
            spec.ScalePolicy,
            isFrozen: false,
            sourceNormalizedTime: 0f);
        worldPosition = pose.Position;
        worldRotation = pose.Rotation;
    }

    public static ContactAnchorPose ResolveAnchor(Entity source, in ResolvedContactSpec spec) =>
        ContactPoseResolver.ResolveRuntimeAnchor(source, spec.Origin);
}
