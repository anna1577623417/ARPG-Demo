using UnityEngine;

/// <summary>237 L1 — 当帧方向意图。DesiredFacing 不是 CommittedFacing。</summary>
public readonly struct DirectionalIntent
{
    public readonly Vector2 MoveIntent;
    public readonly Vector3 DesiredFacing;
    public readonly float Magnitude;

    public DirectionalIntent(Vector2 moveIntent, Vector3 desiredFacing, float magnitude)
    {
        MoveIntent = moveIntent;
        DesiredFacing = desiredFacing;
        Magnitude = magnitude;
    }

    public bool HasMove => Magnitude > 0.0001f;
}
