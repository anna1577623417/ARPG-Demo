using UnityEngine;

/// <summary>
/// 已由反馈路由器翻译完成的世界空间冲量请求。
/// </summary>
public readonly struct ImpulseRequest
{
    public readonly Vector3 Direction;
    public readonly float Force;
    public readonly float LaunchUpSpeed;
    public readonly ImpulseKind Kind;
    public readonly IEntity Source;

    public ImpulseRequest(
        Vector3 direction,
        float force,
        float launchUpSpeed,
        ImpulseKind kind,
        IEntity source)
    {
        Direction = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.zero;
        Force = Mathf.Max(0f, force);
        LaunchUpSpeed = Mathf.Max(0f, launchUpSpeed);
        Kind = kind;
        Source = source;
    }
}
