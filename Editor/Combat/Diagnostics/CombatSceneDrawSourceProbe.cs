#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 224.1 L0 — Scene 主 Shape 绘制源标签与同帧冲突观察。
/// 只读诊断：不改写资产、不改变 Handle 写回、不接管绘制所有权（L5 Coordinator 再收口）。
/// </summary>
internal static class CombatSceneDrawSourceProbe
{
    public const string LogPrefix = "[224.0][DrawSource]";

    public const string SourceContactScene = "ContactSceneEditor";
    public const string SourceLegacyHitVolume = "CombatHitVolumeSceneEditor";
    public const string SourceHitShapeGizmo = "HitShapeGizmoPreview";
    public const string SourceTimelineCombatVolume = "TimelinePreview.CombatVolume";
    public const string SourceCombatTrackEditor = "CombatTrackEditor";

    static int s_frame = -1;
    static readonly List<string> s_sourcesThisFrame = new List<string>(8);
    static bool s_conflictLoggedThisFrame;

    public static bool IsEnabled => GameMainDebugSettings.CombatSceneDrawSource;

    public static void BeginSceneGuiFrame()
    {
        if (!IsEnabled) return;
        var frame = Time.frameCount;
        if (frame == s_frame) return;
        s_frame = frame;
        s_sourcesThisFrame.Clear();
        s_conflictLoggedThisFrame = false;
    }

    /// <summary>
    /// 登记本帧一次主 Shape 绘制，并在世界坐标旁画 source/owner 标签。
    /// </summary>
    public static void RegisterPrimaryDraw(
        string source,
        Vector3 worldPosition,
        string ownerDetail = null)
    {
        if (!IsEnabled) return;
        BeginSceneGuiFrame();

        if (!s_sourcesThisFrame.Contains(source))
        {
            s_sourcesThisFrame.Add(source);
        }

        if (s_sourcesThisFrame.Count > 1 && !s_conflictLoggedThisFrame)
        {
            s_conflictLoggedThisFrame = true;
            Debug.LogWarning(
                $"{LogPrefix} CONFLICT frame={s_frame} sources=[{string.Join(", ", s_sourcesThisFrame)}] " +
                $"detail={Safe(ownerDetail)}");
        }

        var label = string.IsNullOrEmpty(ownerDetail)
            ? $"SRC={source}"
            : $"SRC={source}\n{ownerDetail}";
        Handles.Label(worldPosition + Vector3.up * 0.15f, label);
    }

    public static void DrawHudBanner(string source, string detail)
    {
        if (!IsEnabled) return;
        Handles.BeginGUI();
        GUI.Box(
            new Rect(12f, 40f, 760f, 22f),
            $"{LogPrefix} {source} · {detail}");
        Handles.EndGUI();
    }

    static string Safe(string value) => string.IsNullOrEmpty(value) ? "-" : value;
}
#endif
