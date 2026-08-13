using UnityEngine;

/// <summary>Preview 与 Runtime 共用的 Pose 合成/逆合成；禁止用 Vector 相减替代旋转基准。</summary>
public static class ContactPlacementMath
{
    public static void ResolveWorld(
        Vector3 basisPosition,
        Quaternion basisRotation,
        Vector3 localOffset,
        Quaternion localRotation,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        worldPosition = basisPosition + basisRotation * localOffset;
        worldRotation = basisRotation * localRotation;
    }

    public static void ResolveLocal(
        Vector3 basisPosition,
        Quaternion basisRotation,
        Vector3 worldPosition,
        Quaternion worldRotation,
        out Vector3 localOffset,
        out Quaternion localRotation)
    {
        var inverse = Quaternion.Inverse(basisRotation);
        localOffset = inverse * (worldPosition - basisPosition);
        localRotation = inverse * worldRotation;
    }
}
