using UnityEngine;

/// <summary>
/// 209.3 — 八向 Group 选路与 SplitFrame 双轴配置桥接。
/// 输入分轨读 Group.DirectionalInputFrame；位移分轨读 Group.ResolveMotionCurveBasis。
/// </summary>
internal static class DirectionalRouteMotionBinding
{
    internal static SkillRouteDefinition SelectRouteForChord(
        SkillGroupDefinition group,
        Vector2 moveBuffered,
        Player owner,
        out DirectionalRouteType resolvedDir)
    {
        resolvedDir = DirectionalRouteType.Forward;
        if (group == null)
        {
            return null;
        }

        var inputFrame = group.DirectionalInputFrame;
        resolvedDir = DirectionalFrameResolver.ResolveInputChord(
            inputFrame, moveBuffered, owner);

        var picked = group.SelectByDirection(resolvedDir);
        var profile = picked?.FirstStage()?.Action?.MotionProfile;
        var motionBasis = group.ResolveMotionCurveBasis(profile);
        DodgeChord8Probe.LogSplitFramePick(inputFrame, motionBasis, moveBuffered, resolvedDir, picked?.name);

        return picked;
    }
}
