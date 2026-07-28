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

        var scratch = new WeaponTracePreviewSampler.SocketWorldSample[16];
        Vector3? prevTip = null;

        for (var s = 0; s < SampleCount; s++)
        {
            var t = SampleCount <= 1 ? 0f : s / (float)(SampleCount - 1);
            var nt = Mathf.Lerp(start, end, t);
            var a = Mathf.Lerp(0.15f, 0.4f, t);
            var color = new Color(CloudTrace.r, CloudTrace.g, CloudTrace.b, a);

            var count = WeaponTracePreviewSampler.SampleChain(
                action, in clip, anchor, planarForward, anchorOrigin, nt, scratch);
            if (count <= 0)
            {
                continue;
            }

            WeaponTracePreviewDrawer.DrawSocketChain(scratch, count, color, labelSockets: false);

            if (TryGetTip(scratch, count, out var tip))
            {
                if (prevTip.HasValue)
                {
                    Handles.color = color;
                    Handles.DrawDottedLine(prevTip.Value, tip, 2.5f);
                }

                prevTip = tip;
            }
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
        return WeaponTracePreviewSampler.TrySampleTip(
            action, in clip, anchor, planarForward, anchorOrigin, nt, out tipPos, out tipRadius);
    }

    static bool TryGetTip(
        WeaponTracePreviewSampler.SocketWorldSample[] samples,
        int count,
        out Vector3 tip)
    {
        tip = default;
        for (var i = count - 1; i >= 0; i--)
        {
            if (!samples[i].Valid)
            {
                continue;
            }

            tip = samples[i].Position;
            return true;
        }

        return false;
    }
}
#endif
