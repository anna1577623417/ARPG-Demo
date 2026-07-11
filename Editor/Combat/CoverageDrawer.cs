#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 216.3 M6 L1 — Active 期多帧采样攻击云（Coverage）。纯 Handles，不参与逻辑。
/// </summary>
public static class CoverageDrawer
{
    const int SampleCount = 10;

    static readonly Color CloudVolume = new Color(0.98f, 0.35f, 0.28f, 0.22f);
    static readonly Color CloudTrace = new Color(1f, 0.55f, 0.2f, 0.28f);

    /// <summary>
    /// 对每个 HitClip 在 Active 区间内采样多帧 Shape / Socket，叠成攻击云。
    /// </summary>
    public static void Draw(
        ActionDataSO action,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin)
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
            if (end - start < 1e-4f)
            {
                continue;
            }

            switch (clip.ShapeMode)
            {
                case HitShapeMode.WeaponTrace:
                    DrawWeaponTraceCloud(action, in clip, anchor, planarForward, anchorOrigin, start, end);
                    break;

                case HitShapeMode.Volume:
                default:
                    DrawVolumeCloud(action, in clip, anchor, planarForward, anchorOrigin, start, end);
                    break;
            }
        }
    }

    static void DrawVolumeCloud(
        ActionDataSO action,
        in HitClip clip,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float start,
        float end)
    {
        if (clip.Shape == null)
        {
            return;
        }

        for (var s = 0; s < SampleCount; s++)
        {
            var t = SampleCount <= 1 ? 0f : s / (float)(SampleCount - 1);
            var nt = Mathf.Lerp(start, end, t);
            if (!CombatHitPreviewResolver.TryResolveHitClipWorldPose(
                    action, in clip, anchor, planarForward, anchorOrigin, nt, out var pos, out var rot))
            {
                continue;
            }

            var a = Mathf.Lerp(0.12f, 0.35f, t);
            var color = new Color(CloudVolume.r, CloudVolume.g, CloudVolume.b, a);
            HitShapeGizmoPreview.DrawShapeHandles(clip.Shape, pos, rot, color);
        }
    }

    static void DrawWeaponTraceCloud(
        ActionDataSO action,
        in HitClip clip,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float start,
        float end)
    {
        if (clip.WeaponSockets == null || clip.WeaponSockets.Count <= 0)
        {
            return;
        }

        Vector3? prevTip = null;
        for (var s = 0; s < SampleCount; s++)
        {
            var t = SampleCount <= 1 ? 0f : s / (float)(SampleCount - 1);
            var nt = Mathf.Lerp(start, end, t);
            var a = Mathf.Lerp(0.15f, 0.4f, t);
            var color = new Color(CloudTrace.r, CloudTrace.g, CloudTrace.b, a);

            if (!TrySampleSockets(
                    action, in clip, anchor, planarForward, anchorOrigin, nt,
                    out var tip, out var tipRadius))
            {
                continue;
            }

            Handles.color = color;
            DrawWireSphere(tip, tipRadius);

            if (prevTip.HasValue)
            {
                Handles.DrawDottedLine(prevTip.Value, tip, 2.5f);
            }

            prevTip = tip;
        }
    }

    internal static bool TrySampleSockets(
        ActionDataSO action,
        in HitClip clip,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float nt,
        out Vector3 tipPos,
        out float tipRadius)
    {
        tipPos = default;
        tipRadius = 0.05f;
        var set = clip.WeaponSockets;
        if (set == null || set.Sockets == null || set.Sockets.Length == 0)
        {
            return false;
        }

        if (!CombatHitPreviewResolver.TryResolveHitClipWorldPose(
                action, in clip, anchor, planarForward, anchorOrigin, nt, out var origin, out var rot))
        {
            return false;
        }

        // Editor 无逐帧 Animator：用 Origin 位姿 + Socket LocalOffset 近似刀尖云。
        var last = set.Sockets[set.Sockets.Length - 1];
        tipRadius = last.Radius > 0.01f ? last.Radius : 0.05f;
        tipPos = origin + rot * last.LocalOffset;
        return true;
    }

    static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }
}
#endif
