/// <summary>
/// 8 向移动语义（与 <see cref="DirectionalRouteType"/> 对齐，供 CombatContext / Transition 条件消费）。
/// </summary>
public enum MoveDirection8 : byte
{
    None = 0,
    Forward = 1,
    Backward = 2,
    Left = 3,
    Right = 4,
    ForwardLeft = 5,
    ForwardRight = 6,
    BackwardLeft = 7,
    BackwardRight = 8,
}

public static class MoveDirection8Extensions
{
    public static MoveDirection8 FromDirectional(DirectionalRouteType dir)
    {
        switch (dir)
        {
            case DirectionalRouteType.Forward:       return MoveDirection8.Forward;
            case DirectionalRouteType.Backward:      return MoveDirection8.Backward;
            case DirectionalRouteType.Left:          return MoveDirection8.Left;
            case DirectionalRouteType.Right:         return MoveDirection8.Right;
            case DirectionalRouteType.ForwardLeft:   return MoveDirection8.ForwardLeft;
            case DirectionalRouteType.ForwardRight:  return MoveDirection8.ForwardRight;
            case DirectionalRouteType.BackwardLeft:  return MoveDirection8.BackwardLeft;
            case DirectionalRouteType.BackwardRight: return MoveDirection8.BackwardRight;
            default:                                 return MoveDirection8.None;
        }
    }

    public static DirectionalRouteType ToDirectional(this MoveDirection8 dir)
    {
        switch (dir)
        {
            case MoveDirection8.Forward:       return DirectionalRouteType.Forward;
            case MoveDirection8.Backward:      return DirectionalRouteType.Backward;
            case MoveDirection8.Left:          return DirectionalRouteType.Left;
            case MoveDirection8.Right:         return DirectionalRouteType.Right;
            case MoveDirection8.ForwardLeft:   return DirectionalRouteType.ForwardLeft;
            case MoveDirection8.ForwardRight:  return DirectionalRouteType.ForwardRight;
            case MoveDirection8.BackwardLeft:  return DirectionalRouteType.BackwardLeft;
            case MoveDirection8.BackwardRight: return DirectionalRouteType.BackwardRight;
            default:                         return DirectionalRouteType.Forward;
        }
    }
}
