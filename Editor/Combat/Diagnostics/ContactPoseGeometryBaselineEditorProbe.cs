#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 224.1 L0 — Editor 侧 Preview 捕获与菜单入口；核心日志在 <see cref="ContactPoseGeometryBaselineProbe"/>。
/// </summary>
internal static class ContactPoseGeometryBaselineEditorProbe
{
    static bool s_previewCaptured;

    [MenuItem("GameMain/Debug/224.0 Capture Contact Baseline (Preview)", false, 40)]
    static void MenuCapturePreview()
    {
        GameMainDebugSettings.CombatContactBaseline = true;
        GameMainDebugSettings.SaveToEditorPrefs();
        s_previewCaptured = false;
        if (!TryCapturePreviewNow(force: true))
        {
            Debug.LogWarning(
                $"{ContactPoseGeometryBaselineProbe.LogPrefix} Preview capture failed: need Timeline Contact selection + preview anchor. " +
                "Select a ContactEvent in Action Timeline, then retry.");
        }
    }

    [MenuItem("GameMain/Debug/224.0 Log GC Sample Method", false, 41)]
    static void MenuLogGcMethod()
    {
        ContactPoseGeometryBaselineProbe.LogGcSample("manual-menu", force: true);
    }

    [MenuItem("GameMain/Debug/224.0 Reset Baseline One-Shot Flags", false, 42)]
    static void MenuResetFlags()
    {
        s_previewCaptured = false;
        ContactPoseGeometryBaselineProbe.ResetOneShotFlags();
        Debug.Log($"{ContactPoseGeometryBaselineProbe.LogPrefix} one-shot flags reset (preview/runtime/gc).");
    }

    public static void TryCapturePreviewFromScene(in ContactPreviewState preview)
    {
        if (!ContactPoseGeometryBaselineProbe.IsEnabled || s_previewCaptured) return;
        CapturePreview(in preview, "scene-auto");
        s_previewCaptured = true;
    }

    public static bool TryCapturePreviewNow(bool force)
    {
        if (!ContactPoseGeometryBaselineProbe.IsEnabled && !force) return false;
        var anchor = ActionDataTimelineEditor.ActiveInstance?.GizmoAnchorOverride
                     ?? ActionTimelineEditorUI.ResolvePreviewAnchor(null);
        if (!ContactPreviewResolver.TryResolve(anchor, out var preview, out var failure))
        {
            Debug.LogWarning($"{ContactPoseGeometryBaselineProbe.LogPrefix} Preview resolve failed: {failure}");
            return false;
        }

        CapturePreview(in preview, force ? "menu" : "manual");
        s_previewCaptured = true;
        return true;
    }

    static void CapturePreview(in ContactPreviewState preview, string reason)
    {
        var action = preview.Selection.Action;
        ContactPoseGeometryBaselineProbe.LogPreviewSnapshot(
            reason,
            action != null ? action.name : null,
            preview.Selection.EventId,
            preview.Selection.PreviewTime,
            preview.Event.ActiveStart,
            preview.Event.ActiveEnd,
            in preview.Spec,
            preview.WorldPosition,
            preview.WorldRotation,
            preview.IsActiveAtPreviewTime);
    }
}
#endif
