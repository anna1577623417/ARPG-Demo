using UnityEngine;

/// <summary>
/// 210.2 — DirectionalRouteType ↔ 逻辑/世界平面方向（与 InputChordResolver 象限一致）。
/// </summary>
public static class DirectionalRouteGeometry
{
    public static Vector2 SlotToLocalStick(DirectionalRouteType slot)
    {
        switch (slot)
        {
            case DirectionalRouteType.Forward:
                return new Vector2(0f, 1f);
            case DirectionalRouteType.Backward:
                return new Vector2(0f, -1f);
            case DirectionalRouteType.Left:
                return new Vector2(-1f, 0f);
            case DirectionalRouteType.Right:
                return new Vector2(1f, 0f);
            case DirectionalRouteType.ForwardLeft:
                return new Vector2(-1f, 1f).normalized;
            case DirectionalRouteType.ForwardRight:
                return new Vector2(1f, 1f).normalized;
            case DirectionalRouteType.BackwardLeft:
                return new Vector2(-1f, -1f).normalized;
            case DirectionalRouteType.BackwardRight:
                return new Vector2(1f, -1f).normalized;
            default:
                return new Vector2(0f, 1f);
        }
    }

    public static Vector3 SlotToWorldDirection(DirectionalRouteType slot, Vector3 logicForward)
    {
        logicForward.y = 0f;
        if (logicForward.sqrMagnitude < 0.0001f)
        {
            logicForward = Vector3.forward;
        }

        var stick = SlotToLocalStick(slot);
        var basis = Quaternion.LookRotation(logicForward.normalized, Vector3.up);
        var local = new Vector3(stick.x, 0f, stick.y);
        return basis * local;
    }
}
