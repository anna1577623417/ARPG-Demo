#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 204.x：Action Timeline 预览选项 — 视觉镜像 + Foot IK。
/// 独立 partial 文件，供 <see cref="ActionDataTimelineEditor.MotionPreview"/> 调用。
/// </summary>
internal sealed partial class ActionTimelinePreviewController
{
    // 204.x：视觉镜像状态 — 与 clip Mirror 导入设置完全独立
    bool _forceMirror;
    Transform _mirroredTransform;
    Vector3 _savedMirroredLocalScale;

    // 204.x Foot IK — 默认开启，外部通过 SetFootIKEnabled 控制
    bool _footIKEnabled = true;

    public void SetForceMirrorPreview(bool enabled)
    {
        if (_forceMirror == enabled)
        {
            return;
        }

        _forceMirror = enabled;
        if (!enabled)
        {
            RestoreMirrorScale();
        }
    }

    public bool IsMirroring => _forceMirror;

    public void SetFootIKEnabled(bool enabled)
    {
        _footIKEnabled = enabled;
    }

    public bool IsFootIKEnabled => _footIKEnabled;

    void ApplyFootIKIfNeeded(Transform anchor, Transform sampleRoot)
    {
        if (!_footIKEnabled || anchor == null)
        {
            return;
        }

        var animator = sampleRoot != null
            ? sampleRoot.GetComponentInChildren<Animator>()
            : anchor.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            return;
        }

        if (!FootIKKernel.TryResolveHumanoidFeet(animator, out var pelvis, out var lFoot, out var rFoot))
        {
            return;
        }

        var runtime = animator.GetComponent<FootIKSystem>() ?? animator.GetComponentInParent<FootIKSystem>();
        var settings = runtime != null ? runtime.ResolveSettings() : FootIKKernel.Settings.Default;
        FootIKKernel.Apply(pelvis, lFoot, rFoot, settings);
    }

    void ApplyMirrorScaleIfNeeded(Transform sampleRoot)
    {
        if (sampleRoot == null)
        {
            return;
        }

        if (!_forceMirror)
        {
            RestoreMirrorScale();
            return;
        }

        if (_mirroredTransform != sampleRoot)
        {
            RestoreMirrorScale();
            _mirroredTransform = sampleRoot;
            _savedMirroredLocalScale = sampleRoot.localScale;
        }

        var s = _savedMirroredLocalScale;
        s.x = -Mathf.Abs(s.x);
        sampleRoot.localScale = s;
    }

    void RestoreMirrorScale()
    {
        if (_mirroredTransform == null)
        {
            return;
        }

        _mirroredTransform.localScale = _savedMirroredLocalScale;
        _mirroredTransform = null;
    }
}
#endif
