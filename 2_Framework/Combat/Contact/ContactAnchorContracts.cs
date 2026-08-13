using UnityEngine;

/// <summary>224.1 L3 — Anchor 在某一采样时刻的世界姿态。</summary>
public readonly struct ContactAnchorPose
{
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly Vector3 Scale;
    public readonly string DebugPath;

    public ContactAnchorPose(
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        string debugPath)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        DebugPath = debugPath ?? string.Empty;
    }

    public static ContactAnchorPose Identity =>
        new ContactAnchorPose(Vector3.zero, Quaternion.identity, Vector3.one, "identity");
}

/// <summary>统一 Contact 查询姿态；Static 时 IsFrozen=true。</summary>
public readonly struct ResolvedContactPose
{
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly bool IsFrozen;
    public readonly float SourceNormalizedTime;
    public readonly ContactAnchorPose Anchor;

    public ResolvedContactPose(
        Vector3 position,
        Quaternion rotation,
        bool isFrozen,
        float sourceNormalizedTime,
        in ContactAnchorPose anchor)
    {
        Position = position;
        Rotation = rotation;
        IsFrozen = isFrozen;
        SourceNormalizedTime = sourceNormalizedTime;
        Anchor = anchor;
    }
}

/// <summary>运行时/预览共用的 Anchor 查询合同。</summary>
public interface IContactAnchorPoseSource
{
    bool TryResolveAnchor(
        in ContactAnchorReference origin,
        float normalizedTime,
        out ContactAnchorPose pose);
}
