#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 217.2 — WeaponTrace Scene 预览绘制（Socket 链 + 标签）。
/// </summary>
public static class WeaponTracePreviewDrawer
{
    static readonly Color ChainColor = new Color(1f, 0.55f, 0.15f, 0.95f);
    static readonly Color ChainLine = new Color(1f, 0.75f, 0.25f, 0.9f);

    public static void DrawSocketChain(
        WeaponTracePreviewSampler.SocketWorldSample[] samples,
        int count,
        Color color,
        bool labelSockets)
    {
        if (samples == null || count <= 0)
        {
            return;
        }

        Vector3? prev = null;
        for (var i = 0; i < count; i++)
        {
            var s = samples[i];
            if (!s.Valid)
            {
                continue;
            }

            Handles.color = color;
            DrawWireSphere(s.Position, s.Radius);

            if (labelSockets)
            {
                Handles.Label(s.Position + Vector3.up * (0.06f + s.Radius), s.Name);
            }

            if (prev.HasValue)
            {
                Handles.color = ChainLine;
                Handles.DrawLine(prev.Value, s.Position);
            }

            prev = s.Position;
        }
    }

    public static void DrawActiveTrace(
        ActionDataSO action,
        in HitClip clip,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float normalizedTime,
        string clipLabel)
    {
        if (clip.WeaponSockets == null || clip.WeaponSockets.Count <= 0 || anchor == null)
        {
            return;
        }

        var scratch = new WeaponTracePreviewSampler.SocketWorldSample[16];
        var count = WeaponTracePreviewSampler.SampleChain(
            action, in clip, anchor, planarForward, anchorOrigin, normalizedTime, scratch);
        if (count <= 0)
        {
            return;
        }

        DrawSocketChain(scratch, count, ChainColor, labelSockets: true);

        if (TryGetTip(scratch, count, out var tip))
        {
            Handles.color = ChainLine;
            Handles.Label(tip + Vector3.up * 0.28f, clipLabel);
        }
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

    static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }
}
#endif
