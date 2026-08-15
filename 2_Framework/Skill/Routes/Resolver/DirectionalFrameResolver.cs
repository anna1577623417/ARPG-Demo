using UnityEngine;

/// <summary>
/// 209.3 SplitFrameDirectional — 输入分轨单点真相（WSAD → 八向 Route 槽）。
/// 237 L3 — LogicProjected 必须吃传入的 basisFacing，禁止默认读 owner.LogicForward。
/// </summary>
public static class DirectionalFrameResolver
{
    /// <summary>
    /// Chord 态：按 Group 输入分轨解析 stick / 世界方向 → DirectionalRouteType。
    /// LogicProjected 相对 <paramref name="basisFacing"/>，不是 live Logic。
    /// </summary>
    public static DirectionalRouteType ResolveInputChord(
        DirectionalInputFrame frame,
        Vector2 moveBuffered,
        Vector3 worldDir,
        Vector3 basisFacing,
        DirectionalRouteType defaultDir = DirectionalRouteType.Forward)
    {
        switch (frame)
        {
            case DirectionalInputFrame.LogicProjected:
                return InputChordResolver.ResolveRelativeToLogicForward(
                    worldDir, basisFacing, defaultDir);

            case DirectionalInputFrame.BodyFixed:
                return InputChordResolver.Resolve(moveBuffered, defaultDir);

#pragma warning disable CS0618
            case DirectionalInputFrame.CharacterStick:
            case DirectionalInputFrame.WorldStick:
#pragma warning restore CS0618
                // T7：Play 槽与 BodyFixed 同走 ScreenStickRaw。保留枚举值以免改既有资产 int。
                return InputChordResolver.Resolve(moveBuffered, defaultDir);

            default:
                return InputChordResolver.Resolve(moveBuffered, defaultDir);
        }
    }
}
