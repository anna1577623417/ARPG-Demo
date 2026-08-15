using UnityEngine;

/// <summary>
/// 237 L6 — Action 进入时冻结的 Motion 参考系。Tick 只读，禁止再取 live Transform。
/// </summary>
public readonly struct MotionFrameSnapshot
{
    public readonly bool IsValid;
    public readonly bool Frozen;
    public readonly MotionSpace Space;
    public readonly Vector3 Forward;
    public readonly Vector3 Right;
    public readonly Vector3 Up;

    public MotionFrameSnapshot(
        bool isValid,
        bool frozen,
        MotionSpace space,
        Vector3 forward,
        Vector3 right,
        Vector3 up)
    {
        IsValid = isValid;
        Frozen = frozen;
        Space = space;
        Forward = forward;
        Right = right;
        Up = up;
    }

    public static MotionFrameSnapshot Invalid(MotionSpace space) =>
        new MotionFrameSnapshot(false, false, space, Vector3.forward, Vector3.right, Vector3.up);

    public static MotionFrameSnapshot Freeze(Vector3 planarForward, MotionSpace space)
    {
        planarForward.y = 0f;
        if (planarForward.sqrMagnitude < 0.0001f)
        {
            return Invalid(space);
        }

        var forward = planarForward.normalized;
        var right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        return new MotionFrameSnapshot(true, true, space, forward, right, Vector3.up);
    }
}
