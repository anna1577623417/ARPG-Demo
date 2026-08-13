using UnityEngine;

/// <summary>
/// 224.1 L3 — Binding/Sweep 正交下的统一 Pose 解析。
/// StaticAtWindowStart 在 Begin 冻结；Follow 每帧重取；Sweep 不在此决定，由 Sampler 读 SweepPolicy。
/// </summary>
public static class ContactPoseResolver
{
    public static ResolvedContactPose Compose(
        in ContactAnchorPose anchor,
        Vector3 localPosition,
        Quaternion localRotation,
        ContactAnchorScalePolicy scalePolicy,
        bool isFrozen,
        float sourceNormalizedTime)
    {
        var local = localPosition;
        if (scalePolicy == ContactAnchorScalePolicy.MultiplyAnchorLossyScale)
        {
            local = Vector3.Scale(local, anchor.Scale);
        }

        ContactPlacementMath.ResolveWorld(
            anchor.Position,
            anchor.Rotation,
            local,
            localRotation,
            out var worldPos,
            out var worldRot);
        return new ResolvedContactPose(
            worldPos,
            worldRot,
            isFrozen,
            sourceNormalizedTime,
            in anchor);
    }

    public static ResolvedContactPose ResolveForBegin(
        in ResolvedContactSpec spec,
        in ContactAnchorPose anchorAtWindowStart,
        float windowStartNormalized)
    {
        var frozen = spec.BindingMode == ContactAnchorBindingMode.StaticAtWindowStart;
        return Compose(
            in anchorAtWindowStart,
            spec.LocalOffset,
            spec.LocalRotation,
            spec.ScalePolicy,
            frozen,
            windowStartNormalized);
    }

    public static ResolvedContactPose ResolveForTick(
        in ResolvedContactSpec spec,
        in ResolvedContactPose? frozenPose,
        in ContactAnchorPose currentAnchor,
        float normalizedTime)
    {
        if (spec.BindingMode == ContactAnchorBindingMode.StaticAtWindowStart
            && frozenPose.HasValue
            && frozenPose.Value.IsFrozen)
        {
            return frozenPose.Value;
        }

        return Compose(
            in currentAnchor,
            spec.LocalOffset,
            spec.LocalRotation,
            spec.ScalePolicy,
            isFrozen: false,
            normalizedTime);
    }

    public static ResolvedContactPose ResolveForPreview(
        in ResolvedContactSpec spec,
        in ContactAnchorPose sampledAnchor,
        float windowStartNormalized,
        float previewNormalizedTime)
    {
        var sampleTime = spec.BindingMode == ContactAnchorBindingMode.StaticAtWindowStart
            ? windowStartNormalized
            : previewNormalizedTime;
        var frozen = spec.BindingMode == ContactAnchorBindingMode.StaticAtWindowStart;
        return Compose(
            in sampledAnchor,
            spec.LocalOffset,
            spec.LocalRotation,
            spec.ScalePolicy,
            frozen,
            sampleTime);
    }

    /// <summary>从 Entity 当前骨骼解析 Anchor（Runtime Begin/Tick）。</summary>
    public static ContactAnchorPose ResolveRuntimeAnchor(Entity source, SpawnSource origin)
    {
        var root = source != null ? source.transform : null;
        var basisPosition = root != null ? root.position : Vector3.zero;
        var basisRotation = root != null ? root.rotation : Quaternion.identity;
        var scale = root != null ? root.lossyScale : Vector3.one;
        var path = root != null ? root.name : "null";

        if (CombatSpawnBoneResolver.TryGetBoneTransform(source, origin, out var bone)
            && bone != null)
        {
            basisPosition = bone.position;
            basisRotation = bone.rotation;
            scale = bone.lossyScale;
            path = bone.name;
        }

        return new ContactAnchorPose(basisPosition, basisRotation, scale, path);
    }
}
