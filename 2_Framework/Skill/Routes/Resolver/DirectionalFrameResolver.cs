using UnityEngine;

/// <summary>
/// 209.3 SplitFrameDirectional — 输入分轨单点真相（WSAD → 八向 Route 槽）。
/// 与 <see cref="MotionSpace"/> 位移分轨正交；Group 配置 <see cref="DirectionalInputFrame"/>。
/// </summary>
public static class DirectionalFrameResolver
{
    /// <summary>
    /// Chord 态：按 Group 输入分轨解析 moveBuffered → DirectionalRouteType。
    /// </summary>
    public static DirectionalRouteType ResolveInputChord(
        DirectionalInputFrame frame,
        Vector2 moveBuffered,
        Player owner,
        DirectionalRouteType defaultDir = DirectionalRouteType.Forward)
    {
        switch (frame)
        {
            case DirectionalInputFrame.LogicProjected when owner != null:
            {
                var world = owner.ResolveCameraRelativeWorldDirection(moveBuffered);
                return InputChordResolver.ResolveRelativeToLogicForward(
                    world, owner.LogicForward, defaultDir);
            }

            case DirectionalInputFrame.BodyFixed:
            case DirectionalInputFrame.CharacterStick:
            case DirectionalInputFrame.WorldStick:
            default:
                return InputChordResolver.Resolve(moveBuffered, defaultDir);
        }
    }
}
