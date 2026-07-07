#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 时间轴 → 场景角色 Pose 采样（141.1 PreviewController）。
/// 使用 AnimationMode，不写入运行时 Animator 状态。
/// </summary>
internal sealed partial class ActionTimelinePreviewController
{
    Transform _lastSampleRoot;
    AnimationClip _lastClip;
    bool _ownsAnimationMode;

    // 172.2 W4：MotionDriven 模式记录 anchor 进入预览时的世界位置，Stop / 切换 Action 时还原
    Transform _drivenAnchor;
    Vector3 _drivenAnchorOriginPos;
    Quaternion _drivenAnchorOriginRot;
    bool _drivenAnchorCaptured;

    // 197.3 / 203.1：MotionProfile 预览 — Segment 起点 Hips 在 Anchor 局部基准（剥离平面双位移）
    int _baselineClipId;
    int _baselineAnchorId;
    int _baselineMirrorKey;
    Vector3 _baselineHipsLocal;
    bool _hasHipsBaseline;

    // 203.1：Mirror 采样可能写入 Armature 根位移 — 每帧采样后还原 local，避免与 Motion 叠加
    Transform _sampleRootOriginTransform;
    Vector3 _sampleRootOriginLocalPos;
    Quaternion _sampleRootOriginLocalRot;
    bool _sampleRootOriginCaptured;

    public bool Enabled { get; set; } = true;

    // 171.5 W0：让外部（SceneBridge / Context）读到稳定的 CaptureOrigin，所有 Gizmo / Scene Label 锚定它
    public bool HasDrivenAnchor => _drivenAnchorCaptured;
    public Vector3 DrivenAnchorOriginPos => _drivenAnchorOriginPos;
    public Quaternion DrivenAnchorOriginRot => _drivenAnchorOriginRot;

    public void SamplePose(in ActionTimelinePreviewContext ctx, bool applyMotionDisplacement = true)
    {
        if (!Enabled || !ctx.HasAnchor || ctx.Action == null)
        {
            return;
        }

        var clip = ctx.Action.MainClip;
        if (clip == null)
        {
            return;
        }

        var sampleRoot = ResolveSampleRoot(ctx.Anchor);
        if (sampleRoot == null)
        {
            return;
        }

        var sampleSeconds = ResolveSampleSeconds(ctx.Action, ctx.NormalizedTime, clip);

        // 172.2 W4 / 198.x：MotionProfile + MotionDriven 视为同一种（都由 Profile 曲线驱动 Pose 位移）
        //   — Sample 前先把 anchor 还原到 origin，避免上一帧的位移污染下一帧的 SampleAnimationClip
        var motionDriven = ctx.HasMotionProfile
            && !ctx.Action.UseClipRootMotion
            && ctx.MotionMode == MotionPreviewMode.MotionDriven;
        if (motionDriven)
        {
            CaptureDrivenAnchorOnce(ctx.Anchor, sampleRoot);
            if (_drivenAnchorCaptured && _drivenAnchor != null)
            {
                _drivenAnchor.SetPositionAndRotation(_drivenAnchorOriginPos, _drivenAnchorOriginRot);
            }
        }
        else
        {
            ReleaseDrivenAnchor();
        }

        EnsureAnimationMode();

        var needsBaselineCapture = motionDriven && NeedsHipsBaselineCapture(ctx.Anchor, clip);
        var baselineClipSec = motionDriven
            ? ActionAnimSpeedAuthority.ResolvePreviewClipSeconds(ctx.Action, 0f)
            : 0f;

        AnimationMode.BeginSampling();

        if (needsBaselineCapture)
        {
            // Segment 起点：Mirror baseline pose → 剥离 Armature 根位移 → 读 Hips 局部基准
            MirroredClipSampler.Sample(sampleRoot.gameObject, clip, baselineClipSec);
            RestoreSampleRootOrigin(sampleRoot);
            TryCaptureHipsBaseline(ctx.Anchor, clip);
        }

        MirroredClipSampler.Sample(sampleRoot.gameObject, clip, sampleSeconds);

        AnimationMode.EndSampling();

        // 204.x：视觉镜像 — 采样完写 scale.x=-1（即使 clip 没勾 Mirror 也强制可见）
        ApplyMirrorScaleIfNeeded(sampleRoot);

        // 204.x：Foot IK — 防止预览角色脚陷地（须在 Mirror / 位移写入后做，否则脚位置不对）
        ApplyFootIKIfNeeded(ctx.Anchor, sampleRoot);

        if (motionDriven)
        {
            RestoreSampleRootOrigin(sampleRoot);
        }

        if (motionDriven && _drivenAnchorCaptured && ctx.Anchor != null && applyMotionDisplacement)
        {
            ctx.Anchor.position = ctx.MotionWorldPosition;

            if (ctx.UsesActionYawPreview)
            {
                var yawFwd = ctx.ActionYawForward;
                yawFwd.y = 0f;
                if (yawFwd.sqrMagnitude > 0.0001f)
                {
                    ctx.Anchor.rotation = Quaternion.LookRotation(yawFwd.normalized, Vector3.up);
                }
            }
            else
            {
                // 用 cached origin + heading 重算 world offset，避免被运行时偏移污染
                var fwd = _drivenAnchorOriginRot * Vector3.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd.Normalize();
                var headingRot = Quaternion.LookRotation(fwd, Vector3.up);
                ctx.Anchor.position = _drivenAnchorOriginPos + headingRot * ctx.MotionLocalPosition;
            }
        }

        if (motionDriven && ctx.Anchor != null)
        {
            ApplyInPlaceHipsCompensation(ctx.Anchor);
        }

        _lastSampleRoot = sampleRoot;
        _lastClip = clip;
    }

    void ApplyInPlaceHipsCompensation(Transform anchor)
    {
        if (!MotionProfileInPlaceBoneCompensator.TryResolveHipsBone(anchor, out var hips)
            || !_hasHipsBaseline)
        {
            return;
        }

        MotionProfileInPlaceBoneCompensator.CompensateHipsPlanarFromBaseline(
            anchor, hips, in _baselineHipsLocal);
    }

    bool NeedsHipsBaselineCapture(Transform anchor, AnimationClip clip)
    {
        if (clip == null || anchor == null)
        {
            return false;
        }

        var mirrorKey = MirroredClipSampler.ResolveMirrorCacheKey(clip);
        return !_hasHipsBaseline
            || _baselineClipId != clip.GetInstanceID()
            || _baselineAnchorId != anchor.GetInstanceID()
            || _baselineMirrorKey != mirrorKey;
    }

    void TryCaptureHipsBaseline(Transform anchor, AnimationClip clip)
    {
        if (!MotionProfileInPlaceBoneCompensator.TryResolveHipsBone(anchor, out var hips))
        {
            return;
        }

        _baselineHipsLocal = MotionProfileInPlaceBoneCompensator.ReadHipsLocalOnAnchor(anchor, hips);
        _hasHipsBaseline = true;
        _baselineClipId = clip.GetInstanceID();
        _baselineAnchorId = anchor.GetInstanceID();
        _baselineMirrorKey = MirroredClipSampler.ResolveMirrorCacheKey(clip);
    }

    void InvalidateHipsBaseline()
    {
        _hasHipsBaseline = false;
        _baselineClipId = 0;
        _baselineAnchorId = 0;
        _baselineMirrorKey = 0;
    }

    void InvalidateSampleRootOrigin()
    {
        _sampleRootOriginCaptured = false;
        _sampleRootOriginTransform = null;
    }

    public void Stop()
    {
        if (_ownsAnimationMode && AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        _ownsAnimationMode = false;
        _lastSampleRoot = null;
        _lastClip = null;
        InvalidateHipsBaseline();
        InvalidateSampleRootOrigin();
        ReleaseDrivenAnchor();
        // 204.x：释放镜像采样 Graph + 临时 AnimatorController
        MirroredClipSampler.Release();
        // 204.x：还原视觉镜像缩放，避免预览关闭后 X 缩放残留在 prefab/scene
        RestoreMirrorScale();
    }

    /// <summary>172.2 W5：策划点 "Reset Pos" 强制把角色还原到进入预览时的位置（不退出预览）。</summary>
    public void ResetDrivenAnchorPosition()
    {
        if (_drivenAnchorCaptured && _drivenAnchor != null)
        {
            _drivenAnchor.SetPositionAndRotation(_drivenAnchorOriginPos, _drivenAnchorOriginRot);
        }
    }

    void CaptureDrivenAnchorOnce(Transform anchor, Transform sampleRoot)
    {
        if (_drivenAnchorCaptured && _drivenAnchor == anchor)
        {
            CaptureSampleRootOriginOnce(sampleRoot);
            return;
        }

        // 切换 anchor → 先还原前一个再记录新的
        ReleaseDrivenAnchor();
        if (anchor == null)
        {
            return;
        }

        _drivenAnchor = anchor;
        _drivenAnchorOriginPos = anchor.position;
        _drivenAnchorOriginRot = anchor.rotation;
        _drivenAnchorCaptured = true;
        CaptureSampleRootOriginOnce(sampleRoot);
    }

    void CaptureSampleRootOriginOnce(Transform sampleRoot)
    {
        if (sampleRoot == null)
        {
            return;
        }

        if (_sampleRootOriginCaptured && _sampleRootOriginTransform == sampleRoot)
        {
            return;
        }

        _sampleRootOriginTransform = sampleRoot;
        _sampleRootOriginLocalPos = sampleRoot.localPosition;
        _sampleRootOriginLocalRot = sampleRoot.localRotation;
        _sampleRootOriginCaptured = true;
    }

    void RestoreSampleRootOrigin(Transform sampleRoot)
    {
        if (!_sampleRootOriginCaptured || sampleRoot == null || sampleRoot != _sampleRootOriginTransform)
        {
            return;
        }

        sampleRoot.localPosition = _sampleRootOriginLocalPos;
        sampleRoot.localRotation = _sampleRootOriginLocalRot;
    }

    void ReleaseDrivenAnchor()
    {
        if (!_drivenAnchorCaptured)
        {
            return;
        }

        if (_drivenAnchor != null)
        {
            _drivenAnchor.SetPositionAndRotation(_drivenAnchorOriginPos, _drivenAnchorOriginRot);
        }

        _drivenAnchorCaptured = false;
        _drivenAnchor = null;
        InvalidateHipsBaseline();
        InvalidateSampleRootOrigin();
    }

    static Transform ResolveSampleRoot(Transform anchor)
    {
        if (anchor == null)
        {
            return null;
        }

        var animator = anchor.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            return animator.transform;
        }

        return anchor;
    }

    static float ResolveSampleSeconds(ActionDataSO action, float normalizedTime, AnimationClip clip)
    {
        _ = clip;
        return ActionAnimSpeedAuthority.ResolvePreviewClipSeconds(action, normalizedTime);
    }

    void EnsureAnimationMode()
    {
        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            _ownsAnimationMode = true;
        }
    }
}
#endif
