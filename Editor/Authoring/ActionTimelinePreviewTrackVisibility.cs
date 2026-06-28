#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 171.1 Phase4 — 各预览轨 Scene 开关（Inspector 持久化于 Timeline 窗口）。
/// </summary>
[System.Serializable]
internal struct ActionTimelinePreviewTrackVisibility
{
    public bool Motion;
    public bool Combat;
    public bool Teleport;
    public bool Fx;
    public bool Audio;
    public bool Camera;
    public bool TimeScale;
    public bool GhostTrail;

    public static ActionTimelinePreviewTrackVisibility DefaultAllOn => new ActionTimelinePreviewTrackVisibility
    {
        Motion = true,
        Combat = true,
        Teleport = true,
        Fx = true,
        Audio = true,
        Camera = true,
        TimeScale = true,
        GhostTrail = true,
    };

    public int ToPrefsMask()
    {
        var mask = 0;
        if (Motion) mask |= 1 << 0;
        if (Combat) mask |= 1 << 1;
        if (GhostTrail) mask |= 1 << 2;
        if (Teleport) mask |= 1 << 3;
        if (Fx) mask |= 1 << 4;
        if (Audio) mask |= 1 << 5;
        if (Camera) mask |= 1 << 6;
        if (TimeScale) mask |= 1 << 7;
        return mask;
    }

    public static ActionTimelinePreviewTrackVisibility FromPrefsMask(int mask)
    {
        if (mask == 0)
        {
            return DefaultAllOn;
        }

        return new ActionTimelinePreviewTrackVisibility
        {
            Motion = (mask & (1 << 0)) != 0,
            Combat = (mask & (1 << 1)) != 0,
            GhostTrail = (mask & (1 << 2)) != 0,
            Teleport = (mask & (1 << 3)) != 0,
            Fx = (mask & (1 << 4)) != 0,
            Audio = (mask & (1 << 5)) != 0,
            Camera = (mask & (1 << 6)) != 0,
            TimeScale = (mask & (1 << 7)) != 0,
        };
    }
}
#endif
