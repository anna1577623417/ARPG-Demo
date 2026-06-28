#if UNITY_EDITOR

using UnityEditor;

using UnityEngine;



/// <summary>

/// 171.1 — 预览用 Clip RootMotion 轨迹采样（Overlay / ClipRootMotion 模式）。

/// 172.1 — 按 Action Segment 映射 Clip 采样时间。

/// </summary>

internal static class ActionTimelineRootMotionSampler

{

    struct CacheKey

    {

        public int ClipId;

        public int AnchorId;

        public int ActionId;

        public int SampleCount;

        public int SegmentStartMilli;

        public int SegmentEndMilli;

    }



    static CacheKey s_lastKey;

    static Vector3[] s_cachedPath;

    static Vector3 s_cachedOrigin;



    public static bool TryBuildPath(

        ActionDataSO action,

        Transform anchor,

        int sampleCount,

        out Vector3[] worldPoints,

        out Vector3 pathOrigin)

    {

        worldPoints = null;

        pathOrigin = default;



        if (action == null || anchor == null || action.MainClip == null || sampleCount < 2)

        {

            return false;

        }



        var clip = action.MainClip;

        var segStart = ActionTimeAuthority.ResolveSegmentStart(action);

        var segEnd = ActionTimeAuthority.ResolveSegmentEnd(action);

        var key = new CacheKey

        {

            ClipId = clip.GetInstanceID(),

            AnchorId = anchor.GetInstanceID(),

            ActionId = action.GetInstanceID(),

            SampleCount = sampleCount,

            SegmentStartMilli = Mathf.RoundToInt(segStart * 1000f),

            SegmentEndMilli = Mathf.RoundToInt(segEnd * 1000f),

        };



        if (s_cachedPath != null

            && s_cachedPath.Length == sampleCount

            && key.ClipId == s_lastKey.ClipId

            && key.AnchorId == s_lastKey.AnchorId

            && key.ActionId == s_lastKey.ActionId

            && key.SampleCount == s_lastKey.SampleCount

            && key.SegmentStartMilli == s_lastKey.SegmentStartMilli

            && key.SegmentEndMilli == s_lastKey.SegmentEndMilli)

        {

            worldPoints = s_cachedPath;

            pathOrigin = s_cachedOrigin;

            return true;

        }



        var sampleRoot = ResolveSampleRoot(anchor);

        if (sampleRoot == null)

        {

            return false;

        }



        var points = new Vector3[sampleCount];

        var originPos = anchor.position;

        var originRot = anchor.rotation;



        var wasActive = sampleRoot.gameObject.activeSelf;

        if (!wasActive)

        {

            sampleRoot.gameObject.SetActive(true);

        }



        var enteredAnimMode = false;

        try

        {

            if (!AnimationMode.InAnimationMode())

            {

                AnimationMode.StartAnimationMode();

                enteredAnimMode = true;

            }



            AnimationMode.BeginSampling();

            var sampleRootLocalPos = sampleRoot.localPosition;
            var sampleRootLocalRot = sampleRoot.localRotation;

            sampleRoot.localPosition = sampleRootLocalPos;
            sampleRoot.localRotation = sampleRootLocalRot;
            var startSeconds = action.MapActionTimeToClipSeconds(0f);
            MirroredClipSampler.Sample(sampleRoot.gameObject, clip, startSeconds);
            var startRootPos = sampleRoot.position;
            points[0] = originPos;

            for (var i = 1; i < sampleCount; i++)
            {
                sampleRoot.localPosition = sampleRootLocalPos;
                sampleRoot.localRotation = sampleRootLocalRot;

                var actionT = i / (float)(sampleCount - 1);
                var seconds = action.MapActionTimeToClipSeconds(actionT);
                MirroredClipSampler.Sample(sampleRoot.gameObject, clip, seconds);

                var delta = sampleRoot.position - startRootPos;

                points[i] = originPos + delta;
            }

            AnimationMode.EndSampling();

        }

        finally

        {

            anchor.SetPositionAndRotation(originPos, originRot);

            if (!wasActive)

            {

                sampleRoot.gameObject.SetActive(false);

            }



            if (enteredAnimMode && AnimationMode.InAnimationMode())

            {

                AnimationMode.StopAnimationMode();

            }

        }



        s_lastKey = key;

        s_cachedPath = points;

        s_cachedOrigin = originPos;

        worldPoints = points;

        pathOrigin = originPos;

        return true;

    }



    public static Vector3 EvaluateWorldPosition(

        ActionDataSO action,

        Transform anchor,

        float normalizedTime,

        Vector3 anchorOrigin)

    {

        if (action == null || anchor == null || action.MainClip == null)

        {

            return anchorOrigin;

        }



        if (!TryBuildPath(action, anchor, PreviewMotionDriver.DefaultPathSampleCount, out var path, out _))

        {

            return anchorOrigin;

        }



        var t = Mathf.Clamp01(normalizedTime);

        var idx = t * (path.Length - 1);

        var i0 = Mathf.FloorToInt(idx);

        var i1 = Mathf.Min(i0 + 1, path.Length - 1);

        var frac = idx - i0;

        return Vector3.Lerp(path[i0], path[i1], frac);

    }



    public static void InvalidateCache()

    {

        s_lastKey = default;

        s_cachedPath = null;

    }



    static Transform ResolveSampleRoot(Transform anchor)

    {

        if (anchor == null)

        {

            return null;

        }



        var animator = anchor.GetComponentInChildren<Animator>();

        return animator != null ? animator.transform : anchor;

    }

}

#endif

