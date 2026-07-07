using UnityEngine;

/// <summary>
/// 209.3 — 八向 Group 选路与 SplitFrame 双轴配置桥接。
/// 213.6 — CharacterForward + BodyFixed 时 ChordReframe（LogicProjected 重选槽）。
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
        var motionBasis = group.ResolveMotionCurveBasis(null);

        var cameraSlot = DirectionalFrameResolver.ResolveInputChord(
            inputFrame, moveBuffered, owner);
        resolvedDir = cameraSlot;

        if (motionBasis == MotionSpace.CharacterForward
            && inputFrame == DirectionalInputFrame.BodyFixed
            && owner != null
            && moveBuffered.sqrMagnitude > 0.0001f)
        {
            var logicSlot = DirectionalFrameResolver.ResolveInputChord(
                DirectionalInputFrame.LogicProjected,
                moveBuffered,
                owner);

            if (logicSlot != cameraSlot)
            {
                resolvedDir = logicSlot;
                DodgeChord8Probe.LogChordReframe(cameraSlot, logicSlot, motionBasis);
                DirectionalInputDiagProbe.LogChordReframe(cameraSlot, logicSlot, motionBasis);
            }
        }

        var picked = group.SelectByDirection(resolvedDir);
        var profile = picked?.FirstStage()?.Action?.MotionProfile;
        var effectiveBasis = group.ResolveMotionCurveBasis(profile);
        DodgeChord8Probe.LogSplitFramePick(inputFrame, effectiveBasis, moveBuffered, resolvedDir, picked?.name);

        return picked;
    }
}
