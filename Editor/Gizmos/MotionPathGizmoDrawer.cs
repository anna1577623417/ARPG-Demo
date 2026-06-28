#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene 视图：选中 MotionProfile 或 Player 时绘制局部轨迹折线（白色）。
///
/// 198.x 校正：
///   ① 起点 / 朝向每帧实时读 Player.LogicForward（不再快照），与 gameplay 一致；
///   ② 应用 stats.MotionScale（运行时 MotionExecutor 同款），缩放对齐；
///   ③ Editor 无 Player 时 fallback 世界原点 + Vector3.forward。
/// </summary>
[InitializeOnLoad]
public static class MotionPathGizmoDrawer
{
    static MotionProfileSO s_previewProfile;
    const int SampleCount = 40;

    static MotionPathGizmoDrawer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += OnSelectionChanged;
    }

    static void OnSelectionChanged()
    {
        if (Selection.activeObject is MotionProfileSO mp)
        {
            SetPreviewProfile(mp);
        }
        else
        {
            // 204.1 W1：焦点切走 MF 时显式清空，避免折线 sticky
            ClearPreviewProfile();
        }
    }

    /// <summary>设置预览 Profile；起点/朝向不再快照，OnSceneGUI 每帧实时解析。</summary>
    public static void SetPreviewProfile(MotionProfileSO profile)
    {
        s_previewProfile = profile;
        SceneView.RepaintAll();
    }

    /// <summary>204.1 W1：清空预览 Profile（MotionProfileEditor.OnDisable 兜底用）。</summary>
    public static void ClearPreviewProfile()
    {
        if (s_previewProfile == null) return;
        s_previewProfile = null;
        SceneView.RepaintAll();
    }

    /// <summary>204.1 W1：判定指定 Profile 是否正在 Scene 预览（OnDisable 守卫用）。</summary>
    public static bool IsPreviewing(MotionProfileSO profile) =>
        profile != null && s_previewProfile == profile;

    static void OnSceneGUI(SceneView view)
    {
        if (s_previewProfile == null || !s_previewProfile.UsesAxisCurves)
        {
            return;
        }

        // 204.1 W2：ActionTimeline 预览已在画同一 Profile 的合成轨迹 → 跳过避免双线重叠
        if (ActionDataTimelineSceneBridge.Action != null
            && ActionDataTimelineSceneBridge.Action.MotionProfile == s_previewProfile)
        {
            return;
        }

        // 实时读 Player.LogicForward（gameplay 真相），无 Player 时 fallback 世界原点
        ResolveOriginAndForward(out var origin, out var forward, out var motionScale);

        var right = Vector3.Cross(Vector3.up, forward).normalized;
        if (right.sqrMagnitude < 0.01f)
        {
            right = Vector3.right;
        }

        var prev = origin;
        Handles.color = Color.white;
        for (var i = 1; i <= SampleCount; i++)
        {
            var t = i / (float)SampleCount;
            var local = s_previewProfile.AxisCurves.SampleLocalPosition(t) * motionScale;
            var world = origin + right * local.x + Vector3.up * local.y + forward * local.z;
            Handles.DrawLine(prev, world);
            prev = world;
        }

        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        Handles.SphereHandleCap(0, origin, Quaternion.identity, 0.1f, EventType.Repaint);
        Handles.color = Color.cyan;
        Handles.SphereHandleCap(0, prev, Quaternion.identity, 0.12f, EventType.Repaint);

        Handles.color = Color.white;
        Handles.Label(origin + Vector3.up * 0.2f,
            $"{s_previewProfile.name}  (scale={motionScale:F2})");
    }

    /// <summary>每帧实时解析；与运行时 MotionExecutor 算式对齐。</summary>
    static void ResolveOriginAndForward(out Vector3 origin, out Vector3 forward, out float motionScale)
    {
        var player = Object.FindFirstObjectByType<Player>();
        if (player == null)
        {
            origin = Vector3.zero;
            forward = Vector3.forward;
            motionScale = 1f;
            return;
        }

        origin = player.transform.position;

        // 184.1：gameplay 真相是 LogicForward；transform.forward 在 VisualRoot 独立旋转时会偏离
        var logic = player.LogicForward;
        logic.y = 0f;
        forward = logic.sqrMagnitude > 0.0001f ? logic.normalized : Vector3.forward;

        // MotionExecutor._motionScale = stats.GetMotionScale(profile.ScaleType)；Editor 模式可能无 stats
        motionScale = TryResolveMotionScale(player, s_previewProfile);
    }

    static float TryResolveMotionScale(Player player, MotionProfileSO profile)
    {
        if (profile == null || profile.ScaleType == MotionScaleType.None)
        {
            return 1f;
        }

        // Editor 非运行模式下 Stats 可能未初始化；用 1f fallback 至少保证 Gizmo 不闪现
        var stats = player.GetComponent<PlayerMotionStatsProvider>();
        if (stats == null)
        {
            return 1f;
        }

        var scale = stats.GetMotionScale(profile.ScaleType);
        return scale > 0.0001f ? scale : 1f;
    }
}
#endif
