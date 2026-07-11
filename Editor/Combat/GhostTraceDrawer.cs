#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 216.3 M6 L1 — Trace / Volume Active 起止插值 Ghost 帧。纯 Handles，不参与逻辑。
/// </summary>
public static class GhostTraceDrawer
{
    const int GhostCount = 5;

    static readonly Color GhostColor = new Color(0.55f, 0.85f, 1f, 0.55f);
    static readonly Color GhostTrail = new Color(0.4f, 0.75f, 1f, 0.7f);

    /// <summary>
    /// playhead 越过 ActiveStart 后，在 [ActiveStart, min(playhead, ActiveEnd)] 画 Ghost1..N。
    /// </summary>
    public static void Draw(
        ActionDataSO action,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float playheadNt)
    {
        if (action?.AttackClips == null || anchor == null)
        {
            return;
        }

        var clips = action.AttackClips;
        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            var start = Mathf.Min(clip.ActiveStart, clip.ActiveEnd);
            var end = Mathf.Max(clip.ActiveStart, clip.ActiveEnd);
            if (playheadNt < start - 1e-4f || end - start < 1e-4f)
            {
                continue;
            }

            var ghostEnd = Mathf.Min(playheadNt, end);
            if (ghostEnd <= start + 1e-4f)
            {
                continue;
            }

            switch (clip.ShapeMode)
            {
                case HitShapeMode.WeaponTrace:
                    DrawWeaponGhosts(action, in clip, anchor, planarForward, anchorOrigin, start, ghostEnd, i);
                    break;

                case HitShapeMode.Volume:
                default:
                    DrawVolumeGhosts(action, in clip, anchor, planarForward, anchorOrigin, start, ghostEnd, i);
                    break;
            }
        }
    }

    static void DrawVolumeGhosts(
        ActionDataSO action,
        in HitClip clip,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float start,
        float ghostEnd,
        int clipIndex)
    {
        if (clip.Shape == null)
        {
            return;
        }

        Vector3? prev = null;
        for (var g = 0; g < GhostCount; g++)
        {
            var t = GhostCount <= 1 ? 1f : g / (float)(GhostCount - 1);
            var nt = Mathf.Lerp(start, ghostEnd, t);
            if (!CombatHitPreviewResolver.TryResolveHitClipWorldPose(
                    action, in clip, anchor, planarForward, anchorOrigin, nt, out var pos, out var rot))
            {
                continue;
            }

            var a = Mathf.Lerp(0.25f, 0.65f, t);
            var color = new Color(GhostColor.r, GhostColor.g, GhostColor.b, a);
            HitShapeGizmoPreview.DrawShapeHandles(clip.Shape, pos, rot, color);

            Handles.color = color;
            Handles.Label(pos + Vector3.up * 0.12f, $"Ghost{g + 1}");

            if (prev.HasValue)
            {
                Handles.color = GhostTrail;
                Handles.DrawDottedLine(prev.Value, pos, 3f);
            }

            prev = pos;
        }

        var name = string.IsNullOrEmpty(clip.DebugName) ? $"Atk{clipIndex}" : clip.DebugName;
        if (prev.HasValue)
        {
            Handles.color = GhostTrail;
            Handles.Label(prev.Value + Vector3.up * 0.28f, $"{name} Ghost");
        }
    }

    static void DrawWeaponGhosts(
        ActionDataSO action,
        in HitClip clip,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float start,
        float ghostEnd,
        int clipIndex)
    {
        if (clip.WeaponSockets == null || clip.WeaponSockets.Count <= 0)
        {
            return;
        }

        Vector3? prev = null;
        for (var g = 0; g < GhostCount; g++)
        {
            var t = GhostCount <= 1 ? 1f : g / (float)(GhostCount - 1);
            var nt = Mathf.Lerp(start, ghostEnd, t);
            if (!CoverageDrawer.TrySampleSockets(
                    action, in clip, anchor, planarForward, anchorOrigin, nt,
                    out var tip, out var radius))
            {
                continue;
            }

            var a = Mathf.Lerp(0.3f, 0.75f, t);
            Handles.color = new Color(GhostColor.r, GhostColor.g, GhostColor.b, a);
            DrawWireSphere(tip, radius);
            Handles.Label(tip + Vector3.up * 0.1f, $"Ghost{g + 1}");

            if (prev.HasValue)
            {
                Handles.color = GhostTrail;
                Handles.DrawLine(prev.Value, tip);
            }

            prev = tip;
        }
    }

    static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }
}
#endif
