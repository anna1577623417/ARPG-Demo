#if UNITY_EDITOR

using UnityEditor;

using UnityEngine;



/// <summary>

/// Action 时间轴 Scene 预览上下文（141.1 + 171.1 统一 Evaluate 入口）。

/// 210.5 Phase B：位移轨迹仅 PreviewMotionDriver；朝向预览仅 Action Yaw。

/// </summary>

internal readonly struct ActionTimelinePreviewContext

{

    public ActionDataSO Action { get; }

    public float NormalizedTime { get; }

    public float LogicDurationSeconds { get; }

    public float WallTimeSeconds { get; }

    public Transform Anchor { get; }

    public Vector3 AnchorOrigin { get; }

    public Vector3 Position { get; }

    public Vector3 PlanarForward { get; }

    public ulong ActiveStateBits { get; }

    public bool HasAnchor { get; }

    public MotionPreviewMode MotionMode { get; }

    public Vector3 MotionLocalPosition { get; }

    public Vector3 MotionWorldPosition { get; }

    public Vector3 RootMotionWorldPosition { get; }

    public bool HasMotionProfile { get; }

    public bool UseMotionProfileForVolumes { get; }

    public float ProfileAnimSpeedFactor { get; }

    public float RootMotionOverlayDeltaMeters { get; }



    /// <summary>210.5 — Action Yaw 预览（与 MP 位移独立）。</summary>

    public bool UsesActionYawPreview { get; }



    public Vector3 ActionYawForward { get; }



    public float ActionYawDegrees { get; }



    /// <summary>171.5 W0：CaptureOrigin —— 进入 MotionDriven 时一次性 cached 的世界位置；所有 Gizmo / Scene 信息锚定它，不随角色移动漂移。</summary>

    public Vector3 CaptureOriginPos { get; }

    /// <summary>171.5 W0：CaptureOrigin 旋转 —— 决定 heading（用于 local→world 变换）。</summary>

    public Quaternion CaptureOriginRot { get; }

    /// <summary>171.5 W0：是否有外部传入的 capture origin（PreviewController.HasDrivenAnchor）。</summary>

    public bool HasCaptureOrigin { get; }



    /// <summary>Combat / FX / Camera 等轨道的统一世界锚点。</summary>

    public Vector3 TrackAnchor => UseMotionProfileForVolumes ? MotionWorldPosition : Position;



    ActionTimelinePreviewContext(

        ActionDataSO action,

        float normalizedTime,

        float logicDurationSeconds,

        Transform anchor,

        Vector3 anchorOrigin,

        Vector3 position,

        Vector3 planarForward,

        ulong activeStateBits,

        bool hasAnchor,

        MotionPreviewMode motionMode,

        Vector3 motionLocal,

        Vector3 motionWorld,

        Vector3 rootMotionWorld,

        bool hasMotionProfile,

        bool useMotionProfileForVolumes,

        float profileAnimSpeedFactor,

        float rootMotionOverlayDeltaMeters,

        bool usesActionYawPreview,

        Vector3 actionYawForward,

        float actionYawDegrees,

        Vector3 captureOriginPos,

        Quaternion captureOriginRot,

        bool hasCaptureOrigin)

    {

        Action = action;

        NormalizedTime = normalizedTime;

        LogicDurationSeconds = logicDurationSeconds;

        WallTimeSeconds = logicDurationSeconds * normalizedTime;

        Anchor = anchor;

        AnchorOrigin = anchorOrigin;

        Position = position;

        PlanarForward = planarForward;

        ActiveStateBits = activeStateBits;

        HasAnchor = hasAnchor;

        MotionMode = motionMode;

        MotionLocalPosition = motionLocal;

        MotionWorldPosition = motionWorld;

        RootMotionWorldPosition = rootMotionWorld;

        HasMotionProfile = hasMotionProfile;

        UseMotionProfileForVolumes = useMotionProfileForVolumes;

        ProfileAnimSpeedFactor = profileAnimSpeedFactor;

        RootMotionOverlayDeltaMeters = rootMotionOverlayDeltaMeters;

        UsesActionYawPreview = usesActionYawPreview;

        ActionYawForward = actionYawForward;

        ActionYawDegrees = actionYawDegrees;

        CaptureOriginPos = captureOriginPos;

        CaptureOriginRot = captureOriginRot;

        HasCaptureOrigin = hasCaptureOrigin;

    }



    /// <summary>171.1 蓝图统一入口：拖动 / 播放 / Scene 预览均调用此方法。</summary>

    public static ActionTimelinePreviewContext Evaluate(

        ActionDataSO action,

        float normalizedTime,

        Transform anchorOverride,

        MotionPreviewMode motionMode = MotionPreviewMode.MotionDriven)

        => Build(action, normalizedTime, anchorOverride, motionMode,

                 captureOriginPos: default, captureOriginRot: default, hasCaptureOrigin: false);



    /// <summary>171.5 W0：携带 CaptureOrigin 的入口（MotionDriven 模式下绕过 anchor.position 漂移）。</summary>

    public static ActionTimelinePreviewContext Evaluate(

        ActionDataSO action,

        float normalizedTime,

        Transform anchorOverride,

        MotionPreviewMode motionMode,

        Vector3 captureOriginPos,

        Quaternion captureOriginRot,

        bool hasCaptureOrigin)

        => Build(action, normalizedTime, anchorOverride, motionMode,

                 captureOriginPos, captureOriginRot, hasCaptureOrigin);



    public static ActionTimelinePreviewContext Build(

        ActionDataSO action,

        float normalizedTime,

        Transform anchorOverride,

        MotionPreviewMode motionMode = MotionPreviewMode.MotionDriven,

        Vector3 captureOriginPos = default,

        Quaternion captureOriginRot = default,

        bool hasCaptureOrigin = false)

    {

        if (action == null)

        {

            return default;

        }



        motionMode = MotionPreviewModeUtility.Normalize(motionMode);

        var anchor = ResolveAnchor(anchorOverride);

        var t = Mathf.Clamp01(normalizedTime);

        var logicDuration = action.ResolveLogicalDurationSeconds();

        if (anchor == null)

        {

            return new ActionTimelinePreviewContext(

                action, t, logicDuration, null, default, default, default, 0, false,

                motionMode, default, default, default, false, false, 1f, 0f,

                false, default, 0f,

                captureOriginPos, captureOriginRot, hasCaptureOrigin);

        }



        var useCapture = hasCaptureOrigin && motionMode == MotionPreviewMode.MotionDriven;

        var anchorOrigin = useCapture ? captureOriginPos : anchor.position;

        var fwd = useCapture

            ? captureOriginRot * Vector3.forward

            : anchor.forward;

        fwd.y = 0f;

        if (fwd.sqrMagnitude < 0.0001f)

        {

            fwd = Vector3.forward;

        }

        else

        {

            fwd.Normalize();

        }



        var profile = action.MotionProfile;

        var hasProfile = profile != null && profile.UsesAxisCurves && !action.UseClipRootMotion;

        var motionLocal = hasProfile

            ? PreviewMotionDriver.EvaluateLocalPosition(profile, t)

            : Vector3.zero;

        var motionWorld = hasProfile

            ? PreviewMotionDriver.EvaluateWorldPosition(profile, t, anchorOrigin, fwd)

            : anchorOrigin;



        var usesActionYawPreview = profile != null && profile.UsesActionYaw;

        var actionYawDegrees = usesActionYawPreview ? profile.SampleActionYawDegrees(t) : 0f;

        var actionYawForward = usesActionYawPreview

            ? ActionYawResolver.ResolveForwardFromBurstForward(fwd, actionYawDegrees)

            : fwd;

        var animSpeedFactor = hasProfile

            ? profile.SampleAnimSpeed(action, t)

            : 1f;



        var rootWorld = motionMode is MotionPreviewMode.ClipRootMotion or MotionPreviewMode.Overlay

            ? ActionTimelineRootMotionSampler.EvaluateWorldPosition(action, anchor, t, anchorOrigin)

            : anchorOrigin;



        var overlayDelta = motionMode == MotionPreviewMode.Overlay && hasProfile

            ? Vector3.Distance(motionWorld, rootWorld)

            : 0f;



        var useMotionForVolumes = motionMode switch

        {

            MotionPreviewMode.MotionDriven => hasProfile,

            MotionPreviewMode.Overlay => hasProfile,

            MotionPreviewMode.ClipRootMotion => false,

            _ => hasProfile,

        };



        var displayPos = motionMode switch

        {

            MotionPreviewMode.ClipRootMotion => rootWorld,

            MotionPreviewMode.Overlay when hasProfile => motionWorld,

            MotionPreviewMode.Overlay => rootWorld,

            MotionPreviewMode.MotionDriven when hasProfile => motionWorld,

            _ => hasProfile ? motionWorld : rootWorld,

        };



        var mask = default(GameplayTagMask);

        action.EvaluatePhaseTags(t, ref mask);



        return new ActionTimelinePreviewContext(

            action,

            t,

            logicDuration,

            anchor,

            anchorOrigin,

            displayPos,

            fwd,

            mask.Value,

            true,

            motionMode,

            motionLocal,

            motionWorld,

            rootWorld,

            hasProfile,

            useMotionForVolumes,

            animSpeedFactor,

            overlayDelta,

            usesActionYawPreview,

            actionYawForward,

            actionYawDegrees,

            captureOriginPos,

            captureOriginRot,

            hasCaptureOrigin);

    }



    public Vector3 ResolveWorldAtNormalizedTime(float markerTime)

    {

        if (!HasAnchor || Action == null)

        {

            return Position;

        }



        var t = Mathf.Clamp01(markerTime);

        if (UseMotionProfileForVolumes && HasMotionProfile)

        {

            return PreviewMotionDriver.EvaluateWorldPosition(

                Action.MotionProfile,

                t,

                AnchorOrigin,

                ResolveInitialPlanarForward());

        }



        if (MotionMode is MotionPreviewMode.ClipRootMotion or MotionPreviewMode.Overlay)

        {

            return ActionTimelineRootMotionSampler.EvaluateWorldPosition(

                Action, Anchor, t, AnchorOrigin);

        }



        return AnchorOrigin;

    }



    Vector3 ResolveInitialPlanarForward()

    {

        var fwd = HasCaptureOrigin

            ? CaptureOriginRot * Vector3.forward

            : Anchor != null ? Anchor.forward : Vector3.forward;

        fwd.y = 0f;

        return fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;

    }



    static Transform ResolveAnchor(Transform anchorOverride)

    {

        return ActionTimelineEditorUI.ResolvePreviewAnchor(anchorOverride);

    }

}

#endif


