#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class ActionDataTimelineEditor
{
    static bool s_foldStopDeduction = true;
    static float s_stopPreviewEntrySpeed = 6f;

    void DrawStopInheritPhysicsDeductionSection()
    {
        if (_action == null
            || !_action.EnableStopFeature
            || _action.StopStrategy != StopStrategy.InheritPhysics)
        {
            return;
        }

        if (_action.MotionProfile == null || !_action.MotionProfile.EnableStopAuthoring)
        {
            EditorGUILayout.HelpBox(
                "InheritPhysics 推演：需 MotionProfile 且 EnableStopAuthoring。",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(4f);
        s_foldStopDeduction = ActionTimelineEditorUI.Foldout(
            s_foldStopDeduction,
            "Stop 推演 · 积分 D(v)=v²/(2a)（234.6）");
        if (!s_foldStopDeduction)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            var inheritProp = _so?.FindProperty(nameof(ActionDataSO.InheritPhysics));
            StopTapTailSegmentEditor.Draw(_so, inheritProp, _action);

            var inherit = _action.InheritPhysics;
            var vRef = inherit.MaxSpeed > 0.01f ? inherit.MaxSpeed : 8f;
            s_stopPreviewEntrySpeed = EditorGUILayout.Slider(
                new GUIContent("预览入速 (m/s)", "仅 Editor，不进 Runtime 序列化决策"),
                s_stopPreviewEntrySpeed,
                0f,
                Mathf.Max(vRef, inherit.MaxSpeed, 8f));

            var ctx = StopMotionRuntime.Build(_action, _action.MotionProfile, s_stopPreviewEntrySpeed, vRef);
            if (ctx.IsActive)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("runtimeDuration", $"{ctx.RuntimeDuration:F3}s", GUILayout.Width(180f));
                    EditorGUILayout.LabelField("runtimeDistance", $"{ctx.RuntimeDistance:F2}m", GUILayout.Width(160f));
                    EditorGUILayout.LabelField("a", $"{ctx.BrakeDeceleration:F2}", GUILayout.Width(120f));
                }
            }

            DrawStopDeductionChart(_action, s_stopPreviewEntrySpeed);

            EditorGUILayout.HelpBox(
                "绿线 = Loop 积分距离。点按不走绿线：丢掉入场速度，D_tap 铺满尾段。本区 Segment 只定点按起播与尾部，与上方 Time Authority Segment 互不影响。",
                MessageType.None);
        }
    }

    static void DrawStopDeductionChart(ActionDataSO action, float highlightSpeed)
    {
        var inherit = action.InheritPhysics;
        var maxSpeed = Mathf.Max(0.01f, inherit.MaxSpeed);
        const int sampleCount = 32;

        var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(96f), GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

        var plot = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
        if (plot.width < 8f || plot.height < 8f)
        {
            return;
        }

        var dRef = inherit.FullSpeedStopDistance > 0.0001f ? inherit.FullSpeedStopDistance : inherit.MaxDistance;
        var maxDist = Mathf.Max(0.01f, dRef);
        var maxDur = 0.01f;
        if (StopIntegrator.TryDeriveDeceleration(maxSpeed, dRef, out var aRef))
        {
            maxDur = Mathf.Max(0.01f, StopIntegrator.PredictDuration(maxSpeed, aRef));
        }

        Vector2 MapPoint(float speed, float value, float valueMax, float yBias)
        {
            var tx = Mathf.InverseLerp(0f, maxSpeed, speed);
            var ty = value / valueMax;
            var x = plot.x + tx * plot.width;
            var y = plot.yMax - yBias * plot.height * 0.45f - ty * plot.height * 0.45f;
            return new Vector2(x, y);
        }

        var distColor = new Color(0.35f, 0.85f, 0.45f, 1f);
        var durColor = new Color(0.45f, 0.65f, 1f, 1f);

        Vector2 prevDist = default;
        Vector2 prevDur = default;
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)(sampleCount - 1);
            var speed = Mathf.Lerp(0f, maxSpeed, t);
            var ctx = StopMotionRuntime.Build(action, action.MotionProfile, speed, maxSpeed);
            if (!ctx.IsActive)
            {
                continue;
            }

            var distPt = MapPoint(speed, ctx.RuntimeDistance, maxDist, 0f);
            var physT = StopIntegrator.PredictDuration(speed, ctx.BrakeDeceleration);
            var durPt = MapPoint(speed, physT, maxDur, 1f);
            if (i > 0)
            {
                Handles.color = distColor;
                Handles.DrawLine(prevDist, distPt);
                Handles.color = durColor;
                Handles.DrawLine(prevDur, durPt);
            }

            prevDist = distPt;
            prevDur = durPt;
        }

        var hl = Mathf.Clamp(highlightSpeed, 0f, maxSpeed);
        var hx = plot.x + Mathf.InverseLerp(0f, maxSpeed, hl) * plot.width;
        Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Handles.DrawLine(new Vector3(hx, plot.yMin, 0f), new Vector3(hx, plot.yMax, 0f));

        GUI.Label(new Rect(plot.x, plot.yMax + 2f, 60f, 14f), "0 m/s", EditorStyles.miniLabel);
        GUI.Label(
            new Rect(plot.xMax - 60f, plot.yMax + 2f, 60f, 14f),
            $"{maxSpeed:F1} m/s",
            EditorStyles.miniLabel);
    }
}
#endif
