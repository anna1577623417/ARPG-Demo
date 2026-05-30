#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Scene 视图：选中 MotionProfile 或 Player 时绘制局部轨迹折线（白色）。</summary>
[InitializeOnLoad]
public static class MotionPathGizmoDrawer
{
    static MotionProfileSO s_previewProfile;
    static Vector3 s_origin = Vector3.zero;
    static Vector3 s_forward = Vector3.forward;
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
    }

    public static void SetPreviewProfile(MotionProfileSO profile)
    {
        s_previewProfile = profile;
        var player = Object.FindFirstObjectByType<Player>();
        if (player != null)
        {
            s_origin = player.transform.position;
            s_forward = player.transform.forward;
        }
        else
        {
            s_origin = Vector3.zero;
            s_forward = Vector3.forward;
        }

        SceneView.RepaintAll();
    }

    static void OnSceneGUI(SceneView view)
    {
        if (s_previewProfile == null || !s_previewProfile.UsesAxisCurves)
        {
            return;
        }

        var right = Vector3.Cross(Vector3.up, s_forward).normalized;
        if (right.sqrMagnitude < 0.01f)
        {
            right = Vector3.right;
        }

        var prev = s_origin;
        Handles.color = Color.white;
        for (var i = 1; i <= SampleCount; i++)
        {
            var t = i / (float)SampleCount;
            var local = s_previewProfile.AxisCurves.SampleLocalPosition(t);
            var world = s_origin + right * local.x + Vector3.up * local.y + s_forward * local.z;
            Handles.DrawLine(prev, world);
            prev = world;
        }

        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        Handles.SphereHandleCap(0, s_origin, Quaternion.identity, 0.1f, EventType.Repaint);
        Handles.color = Color.cyan;
        Handles.SphereHandleCap(0, prev, Quaternion.identity, 0.12f, EventType.Repaint);

        Handles.color = Color.white;
        Handles.Label(s_origin + Vector3.up * 0.2f, s_previewProfile.name);
    }
}
#endif
