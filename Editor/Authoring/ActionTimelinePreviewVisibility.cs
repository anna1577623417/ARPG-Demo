#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>171.5 W4 — Scene / Pose 预览可见性 mask（EditorPrefs 持久化）。</summary>
[System.Flags]
internal enum PreviewVisibilityMask : int
{
    None = 0,
    Pose = 1 << 0,
    MotionDriven = 1 << 1,
    Composite = 1 << 2,
    XAxis = 1 << 3,
    YAxis = 1 << 4,
    ZAxis = 1 << 5,
    FutureMarkers = 1 << 6,
    Windows = 1 << 7,
    Hitbox = 1 << 8,
    Hurtbox = 1 << 9,
    InvuInter = 1 << 10,
    SceneInfo = 1 << 11,
    SceneAnchor = 1 << 12,
    GhostTrail = 1 << 13,
    Teleport = 1 << 14,
    Presentation = 1 << 15,
    All = ~0,
}

internal static class ActionTimelinePreviewVisibility
{
    const string PrefKey = "ATL.PreviewMask";

    public static PreviewVisibilityMask Load()
    {
        if (!EditorPrefs.HasKey(PrefKey))
        {
            return PreviewVisibilityMask.All;
        }

        return (PreviewVisibilityMask)EditorPrefs.GetInt(PrefKey, (int)PreviewVisibilityMask.All);
    }

    public static void Save(PreviewVisibilityMask mask)
    {
        EditorPrefs.SetInt(PrefKey, (int)mask);
    }

    public static bool Has(in PreviewVisibilityMask mask, PreviewVisibilityMask flag) =>
        (mask & flag) != 0;

    public static PreviewVisibilityMask PresetPoseOnly =>
        PreviewVisibilityMask.Pose | PreviewVisibilityMask.MotionDriven;

    public static PreviewVisibilityMask PresetTrajectoryOnly =>
        PreviewVisibilityMask.Composite
        | PreviewVisibilityMask.XAxis
        | PreviewVisibilityMask.YAxis
        | PreviewVisibilityMask.ZAxis
        | PreviewVisibilityMask.FutureMarkers;

    public static PreviewVisibilityMask PresetInfoOnly =>
        PreviewVisibilityMask.Windows
        | PreviewVisibilityMask.Hitbox
        | PreviewVisibilityMask.Hurtbox
        | PreviewVisibilityMask.InvuInter
        | PreviewVisibilityMask.SceneInfo;

    public static string GetSummaryLabel(PreviewVisibilityMask mask)
    {
        if (mask == PreviewVisibilityMask.All)
        {
            return "全部";
        }

        if (mask == PresetPoseOnly)
        {
            return "仅动作";
        }

        if (mask == PresetTrajectoryOnly)
        {
            return "仅轨迹";
        }

        if (mask == PresetInfoOnly)
        {
            return "仅信息";
        }

        var count = 0;
        for (var i = 0; i < 16; i++)
        {
            if (((int)mask & (1 << i)) != 0)
            {
                count++;
            }
        }

        return count <= 0 ? "无" : $"自定义({count})";
    }
}
#endif
