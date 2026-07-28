#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 217.2 — Editor 侧 WeaponTrace Socket 采样（跟 Humanoid 骨骼 + Motion 位移，与 Runtime 语义对齐）。
/// </summary>
public static class WeaponTracePreviewSampler
{
    public struct SocketWorldSample
    {
        public string Name;
        public Vector3 Position;
        public float Radius;
        public bool Valid;
    }

    public static int SampleChain(
        ActionDataSO action,
        in HitClip clip,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float normalizedTime,
        SocketWorldSample[] results)
    {
        if (results == null || anchor == null || clip.WeaponSockets == null)
        {
            return 0;
        }

        var set = clip.WeaponSockets;
        if (set.Sockets == null || set.Sockets.Length == 0)
        {
            return 0;
        }

        var motionDelta = ComputeMotionDelta(action, normalizedTime, anchorOrigin, planarForward);
        var anim = anchor.GetComponentInChildren<Animator>();
        var max = Mathf.Min(set.Sockets.Length, results.Length);
        var written = 0;

        for (var i = 0; i < max; i++)
        {
            var def = set.Sockets[i];
            var name = string.IsNullOrEmpty(def.DebugName) ? $"s{i}" : def.DebugName;
            var radius = def.Radius > 0.01f ? def.Radius : 0.05f;

            if (TryResolveSocketWorld(anim, anchor, in def, out var pos))
            {
                pos += motionDelta;
                results[written++] = new SocketWorldSample
                {
                    Name = name,
                    Position = pos,
                    Radius = radius,
                    Valid = true,
                };
            }
            else
            {
                results[written++] = new SocketWorldSample
                {
                    Name = name,
                    Position = default,
                    Radius = radius,
                    Valid = false,
                };
            }
        }

        return written;
    }

    /// <summary>兼容旧 API：返回链上最远端（tip）位置。</summary>
    public static bool TrySampleTip(
        ActionDataSO action,
        in HitClip clip,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float normalizedTime,
        out Vector3 tipPos,
        out float tipRadius)
    {
        tipPos = default;
        tipRadius = 0.05f;
        var scratch = new SocketWorldSample[16];
        var count = SampleChain(action, in clip, anchor, planarForward, anchorOrigin, normalizedTime, scratch);
        for (var i = count - 1; i >= 0; i--)
        {
            if (!scratch[i].Valid)
            {
                continue;
            }

            tipPos = scratch[i].Position;
            tipRadius = scratch[i].Radius;
            return true;
        }

        return false;
    }

    static Vector3 ComputeMotionDelta(
        ActionDataSO action,
        float normalizedTime,
        Vector3 anchorOrigin,
        Vector3 planarForward)
    {
        if (action?.MotionProfile == null || !action.MotionProfile.UsesAxisCurves)
        {
            return Vector3.zero;
        }

        var root0 = PreviewMotionDriver.EvaluateWorldPosition(
            action.MotionProfile, 0f, anchorOrigin, planarForward);
        var rootT = PreviewMotionDriver.EvaluateWorldPosition(
            action.MotionProfile, Mathf.Clamp01(normalizedTime), anchorOrigin, planarForward);
        return rootT - root0;
    }

    static bool TryResolveSocketWorld(
        Animator anim,
        Transform fallbackRoot,
        in WeaponSocketDef def,
        out Vector3 worldPos)
    {
        worldPos = default;

        if (anim != null && anim.isHuman)
        {
            var bone = anim.GetBoneTransform(def.Bone);
            if (bone != null)
            {
                worldPos = bone.position + bone.rotation * def.LocalOffset;
                return true;
            }
        }

        if (fallbackRoot != null)
        {
            worldPos = fallbackRoot.position + fallbackRoot.rotation * def.LocalOffset;
            return true;
        }

        return false;
    }
}
#endif
