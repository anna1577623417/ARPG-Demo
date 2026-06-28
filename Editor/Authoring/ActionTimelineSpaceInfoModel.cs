#if UNITY_EDITOR
using UnityEngine;

/// <summary>171.5 W5 — Timeline / Scene 空间信息单一数据源。</summary>
internal readonly struct ActionTimelineSpaceInfoModel
{
    public readonly string ActionName;
    public readonly string ClipLine;
    public readonly string SegLine;
    public readonly string AnimSpeedLine;
    public readonly string XyzLocalLine;
    public readonly string XyzWorldLine;
    public readonly string OriginLine;
    public readonly string HeadingLine;

    ActionTimelineSpaceInfoModel(
        string actionName,
        string clipLine,
        string segLine,
        string animSpeedLine,
        string xyzLocalLine,
        string xyzWorldLine,
        string originLine,
        string headingLine)
    {
        ActionName = actionName;
        ClipLine = clipLine;
        SegLine = segLine;
        AnimSpeedLine = animSpeedLine;
        XyzLocalLine = xyzLocalLine;
        XyzWorldLine = xyzWorldLine;
        OriginLine = originLine;
        HeadingLine = headingLine;
    }

    public static ActionTimelineSpaceInfoModel Build(in ActionTimelinePreviewContext ctx)
    {
        if (ctx.Action == null)
        {
            return default;
        }

        var action = ctx.Action;
        var clipName = action.MainClip != null ? action.MainClip.name : "—";
        var clipLen = action.MainClip != null ? action.MainClip.length : 0f;
        var clipNorm = ActionTimeAuthority.MapActionTimeToClipNormalized(ctx.NormalizedTime, action);
        var clipSecs = clipNorm * clipLen;
        var segStart = ActionTimeAuthority.ResolveSegmentStart(action);
        var segEnd = ActionTimeAuthority.ResolveSegmentEnd(action);
        var local = ctx.MotionLocalPosition;
        var origin = ctx.HasCaptureOrigin ? ctx.CaptureOriginPos : ctx.AnchorOrigin;
        var world = origin + Quaternion.LookRotation(
            ctx.PlanarForward.sqrMagnitude > 0.0001f ? ctx.PlanarForward : Vector3.forward,
            Vector3.up) * local;
        var heading = ctx.HasCaptureOrigin
            ? ctx.CaptureOriginRot * Vector3.forward
            : ctx.PlanarForward;
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.0001f)
        {
            heading = Vector3.forward;
        }
        else
        {
            heading.Normalize();
        }

        var yaw = Vector3.SignedAngle(Vector3.forward, heading, Vector3.up);

        return new ActionTimelineSpaceInfoModel(
            action.name,
            $"Clip: {clipName}  @ {clipSecs:F2}s / {clipLen:F2}s",
            $"Seg: {segStart:F2} ~ {segEnd:F2}",
            $"AnimSpeed: ×{ctx.ProfileAnimSpeedFactor:F2}",
            $"XYZ local:  X={local.x:F2}  Y={local.y:F2}  Z={local.z:F2}",
            $"XYZ world:  ( {world.x:F2}, {world.y:F2}, {world.z:F2} )",
            $"Origin:     ( {origin.x:F2}, {origin.y:F2}, {origin.z:F2} )",
            $"Heading:    Forward ({yaw:F0}°)");
    }
}
#endif
