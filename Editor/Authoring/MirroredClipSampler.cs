#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 204.x：镜像采样辅助。
///
/// ★★ 历史教训（206.x 退化重写）★★
/// 早期版本尝试用 PlayableGraph + AnimatorControllerPlayable(state.mirror=true) 走 Mecanim Humanoid mirror。
/// 在 AnimationMode 下该路径表现极不稳定：
///   1. `new AnimatorController()` 创建的未初始化 Controller 赋给 Animator.runtimeAnimatorController
///      → 选中场景对象时 TransformInspector / AnimatorInspector / GameObjectInspector 抛 NullReferenceException
///   2. Mirror Pose 实际未生效（Humanoid retargeter 在 AnimationMode 下无完整 state machine 上下文）
///
/// 现在的策略：本类降级为薄包装，**只做 SampleAnimationClip 直透**，
/// 视觉镜像由 ActionTimelinePreviewController 的 `_forceMirror` (scale.x=-1) 接管 — 见 204.x 视觉镜像方案。
///
/// 保留 ResolveMirrorCacheKey / UsesMirrorPath 公开 API 以兼容调用方；clip Mirror 导入标记**仅作信息**，
/// 不再触发任何额外采样路径。
/// </summary>
internal static class MirroredClipSampler
{
    const string DiagPrefix = "[MirrorDiag]";
    const double DiagThrottleSec = 0.5;

    static readonly System.Collections.Generic.Dictionary<string, double> s_lastDiagPerTag
        = new System.Collections.Generic.Dictionary<string, double>();
    static int s_sampleCallCount;

    static MirroredClipSampler()
    {
    }

    internal static bool IsDiagEnabled => GameMainDebugSettings.MirrorDiagLog;

    static void Diag(string msg, bool force = false)
    {
        if (!IsDiagEnabled) return;
        var now = EditorApplication.timeSinceStartup;
        var tagEnd = msg.IndexOf(' ');
        var tag = tagEnd > 0 ? msg.Substring(0, tagEnd) : "*";
        if (!force)
        {
            if (s_lastDiagPerTag.TryGetValue(tag, out var last) && now - last < DiagThrottleSec) return;
        }
        s_lastDiagPerTag[tag] = now;
        Debug.Log($"{DiagPrefix} {msg}");
    }

    /// <summary>
    /// 统一采样入口：直接走 AnimationMode.SampleAnimationClip。
    /// 视觉镜像由 PreviewController 的 scale.x=-1 toggle 接管，本入口不参与 mirror 逻辑。
    /// 调用方须自行包 BeginSampling/EndSampling。
    /// </summary>
    public static void Sample(GameObject root, AnimationClip clip, float time)
    {
        s_sampleCallCount++;
        if (root == null || clip == null)
        {
            Diag($"[1] Sample SKIP root/clip null root={root != null} clip={clip != null}", force: true);
            return;
        }

        Diag($"[1] Sample call#{s_sampleCallCount} clip={clip.name} time={time:F3}");
        AnimationMode.SampleAnimationClip(root, clip, time);
    }

    /// <summary>无副作用 — 留空兼容旧调用。Controller / PlayableGraph 已不再创建。</summary>
    public static void Release() { }

    /// <summary>供 PreviewController baseline 缓存失效（Mirror 导入标记变化时须重采）。</summary>
    public static int ResolveMirrorCacheKey(AnimationClip clip)
    {
        if (clip == null) return 0;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        return settings.mirror ? 1 : 0;
    }

    /// <summary>当前 Clip 是否带 Mirror 导入标记（仅供 UI 提示；不再触发额外路径）。</summary>
    public static bool UsesMirrorPath(GameObject root, AnimationClip clip)
    {
        if (root == null || clip == null) return false;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        return settings.mirror;
    }
}
#endif
