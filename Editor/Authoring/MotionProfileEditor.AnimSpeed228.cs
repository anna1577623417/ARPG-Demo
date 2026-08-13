#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>【228】FreeFrontAutoTail 双控件 Inspector（partial）。</summary>
public sealed partial class MotionProfileEditor
{
    const float KnotDelta = AnimSpeedKnotTimeline.DefaultMinSegmentLength;

    void DrawFreeFrontAutoTailAuthoring(MotionProfileSO profile)
    {
        EditorGUILayout.HelpBox(
            "【228】控件 A：共享结点。段 From/To = 全局时间；倍率 Start/End = f(t)。\n" +
            "顶栏蓝=Front[0→t*]，棕=AutoTail[t*→1]；t* = 末段 To。\n" +
            "前段积分已 ≥1 时可取消勾选 AutoTail（无需自适应尾部）。\n" +
            "控件 B：只读 Bake。开启 AutoTail 时 End 自动求解。",
            MessageType.Info);

        if (!string.IsNullOrEmpty(profile.AnimSpeedKnotValidateMessage))
        {
            EditorGUILayout.HelpBox(
                "【结点校验】" + profile.AnimSpeedKnotValidateMessage,
                MessageType.Warning);
        }

        var timeline = profile.ReadAnimSpeedKnotTimeline();
        EditorGUI.BeginChangeCheck();

        DrawTailMeta(ref timeline);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("控件 A · 前段结点 / 段卡片", EditorStyles.boldLabel);
        DrawKnotTimelineBar(timeline);
        DrawSegmentCards(ref timeline);
        DrawInsertDeleteButtons(ref timeline);

        var changed = EditorGUI.EndChangeCheck();
        if (changed)
        {
            timeline.NormalizeContinuousJoins();
            Undo.RecordObject(profile, "Edit AnimSpeed Knot Timeline");
            profile.WriteAnimSpeedKnotTimeline(timeline);
            TryAutoBakeFreeFrontAutoTail(profile, recordUndo: false);
            EditorUtility.SetDirty(profile);
            serializedObject.Update();
            timeline = profile.ReadAnimSpeedKnotTimeline();
        }

        EditorGUILayout.Space(6f);
        DrawBakePreview(profile, timeline);
    }

    void DrawTailMeta(ref AnimSpeedKnotTimeline timeline)
    {
        timeline.AutoTailEnabled = EditorGUILayout.Toggle(
            new GUIContent(
                "AutoTail Enabled",
                "关闭：不求解自适应尾部；前段可占满到 t=1。前段积分过大时请关闭。"),
            timeline.AutoTailEnabled);

        using (new EditorGUI.DisabledScope(!timeline.AutoTailEnabled))
        {
            timeline.TailSolveShape = (AnimSpeedTailSolveShape)EditorGUILayout.EnumPopup(
                "Tail Solve Shape", timeline.TailSolveShape);
            timeline.TailJoinFromFront = (AnimSpeedJoinMode)EditorGUILayout.EnumPopup(
                "Tail Join From Front", timeline.TailJoinFromFront);
            timeline.TailMinLength = EditorGUILayout.Slider(
                "L_min (Tail Min Length)", timeline.TailMinLength, 0.01f, 0.4f);

            using (new EditorGUI.DisabledScope(timeline.TailJoinFromFront == AnimSpeedJoinMode.Continuous))
            {
                timeline.TailStartValue = EditorGUILayout.FloatField(
                    "Tail Start (Break)", Mathf.Max(0f, timeline.TailStartValue));
            }
        }

        if (!timeline.AutoTailEnabled)
        {
            EditorGUILayout.HelpBox(
                "AutoTail 已关闭：仅 Bake 前段；末段 To 可拉到 1。不再自动求解 Tail End。",
                MessageType.None);
        }
    }

    void DrawKnotTimelineBar(in AnimSpeedKnotTimeline timeline)
    {
        EditorGUILayout.LabelField(
            "全局时间轴（只读）· 宽度 = motionT∈[0,1] · 分割线 t* = 段列表最后 To",
            EditorStyles.miniLabel);

        var rect = GUILayoutUtility.GetRect(22f, 26f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
        if (timeline.KnotCount < 2)
        {
            return;
        }

        // t* 必须来自结点列表末结点（与段卡片 To 同源），禁止写死 0.5
        var tStar = Mathf.Clamp01(timeline.FrontEndTime);
        var frontW = rect.width * tStar;
        var tailW = rect.width - frontW;
        var frontRect = new Rect(rect.x, rect.y, frontW, rect.height);
        var tailRect = new Rect(rect.x + frontW, rect.y, Mathf.Max(0f, tailW), rect.height);

        EditorGUI.DrawRect(frontRect, new Color(0.25f, 0.45f, 0.7f, 0.9f));
        if (timeline.AutoTailEnabled && tailW > 0.5f)
        {
            EditorGUI.DrawRect(tailRect, new Color(0.55f, 0.35f, 0.2f, 0.9f));
        }
        else if (tailW > 0.5f)
        {
            EditorGUI.DrawRect(tailRect, new Color(0.28f, 0.28f, 0.28f, 0.9f));
        }

        Handles.BeginGUI();
        Handles.color = Color.white;
        for (var i = 0; i < timeline.KnotCount; i++)
        {
            var x = rect.x + rect.width * Mathf.Clamp01(timeline.Times[i]);
            Handles.DrawLine(new Vector3(x, rect.y + 2f), new Vector3(x, rect.yMax - 2f));
        }

        Handles.EndGUI();

        var frontLabel = frontW >= 72f ? $"Front 0→{tStar:F2}" : $"F {tStar:F2}";
        var tailLabel = !timeline.AutoTailEnabled
            ? (tailW >= 48f ? "hold→1" : "hold")
            : (tailW >= 72f ? $"AutoTail {tStar:F2}→1" : $"T→1");
        var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
        };
        if (frontW > 8f)
        {
            GUI.Label(frontRect, frontLabel, labelStyle);
        }

        if (tailW > 8f)
        {
            GUI.Label(tailRect, tailLabel, labelStyle);
        }

        var mode = timeline.AutoTailEnabled ? "AutoTail ON" : "AutoTail OFF";
        EditorGUILayout.LabelField(
            $"t*={tStar:F3}（=末段 To）· L={1f - tStar:F3} · {mode} · 上图是【时间】不是倍率",
            EditorStyles.miniLabel);
    }

    void DrawSegmentCards(ref AnimSpeedKnotTimeline timeline)
    {
        for (var seg = 0; seg < timeline.FrontSegmentCount; seg++)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"段 {seg + 1}", EditorStyles.boldLabel);
                var from = timeline.Times[seg];
                var to = timeline.Times[seg + 1];

                using (new EditorGUI.DisabledScope(seg == 0))
                {
                    var newFrom = EditorGUILayout.Slider("From (共享结点)", from, 0f, 1f - timeline.TailMinLength);
                    if (seg > 0)
                    {
                        timeline.Times[seg] = ClampKnotTime(timeline, seg, newFrom);
                    }
                }

                var maxTo = timeline.AutoTailEnabled
                    ? 1f - Mathf.Max(KnotDelta, timeline.TailMinLength)
                    : 1f;
                var newTo = EditorGUILayout.Slider("To (共享结点)", to, KnotDelta, maxTo);
                timeline.Times[seg + 1] = ClampKnotTime(timeline, seg + 1, newTo);

                // 段左端 = Leave[seg]；段右端 = Arrive[seg+1]
                timeline.LeaveValues[seg] = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        "倍率 Start (Leave@From)",
                        timeline.LeaveValues[seg]));
                if (timeline.Joins[seg] == AnimSpeedJoinMode.Continuous)
                {
                    timeline.ArriveValues[seg] = timeline.LeaveValues[seg];
                }

                timeline.ArriveValues[seg + 1] = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        "倍率 End (Arrive@To)",
                        timeline.ArriveValues[seg + 1]));

                timeline.Joins[seg + 1] = (AnimSpeedJoinMode)EditorGUILayout.EnumPopup(
                    "Join @ To", timeline.Joins[seg + 1]);
                if (timeline.Joins[seg + 1] == AnimSpeedJoinMode.Break)
                {
                    timeline.LeaveValues[seg + 1] = Mathf.Max(
                        0f,
                        EditorGUILayout.FloatField("Leave @ To (Break)", timeline.LeaveValues[seg + 1]));
                }
                else
                {
                    timeline.LeaveValues[seg + 1] = timeline.ArriveValues[seg + 1];
                }

                timeline.SegmentShapes[seg] = (AnimSpeedSegmentShapePreset)EditorGUILayout.EnumPopup(
                    "Shape", timeline.SegmentShapes[seg]);

                EditorGUILayout.LabelField(
                    $"显示区间 {timeline.Times[seg]:F3} → {timeline.Times[seg + 1]:F3}",
                    EditorStyles.miniLabel);
            }
        }
    }

    void DrawInsertDeleteButtons(ref AnimSpeedKnotTimeline timeline)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var canInsert = timeline.FrontSegmentCount < AnimSpeedKnotTimeline.SoftMaxFrontSegments;
            using (new EditorGUI.DisabledScope(!canInsert))
            {
                if (GUILayout.Button("+ 插入结点（末前段中点）", GUILayout.Height(22f)))
                {
                    InsertKnotBeforeTail(ref timeline);
                }
            }

            using (new EditorGUI.DisabledScope(timeline.FrontSegmentCount <= 1))
            {
                if (GUILayout.Button("删除末前段结点", GUILayout.Height(22f)))
                {
                    RemoveKnotBeforeTail(ref timeline);
                }
            }
        }

        if (timeline.FrontSegmentCount >= AnimSpeedKnotTimeline.SoftMaxFrontSegments)
        {
            EditorGUILayout.HelpBox(
                $"前段数已达软上限 {AnimSpeedKnotTimeline.SoftMaxFrontSegments}。",
                MessageType.Warning);
        }
    }

    void DrawBakePreview(MotionProfileSO profile, in AnimSpeedKnotTimeline timeline)
    {
        EditorGUILayout.LabelField("控件 B · Bake 预览（只读）", EditorStyles.boldLabel);
        var epsilon = profile.ResolveAnimSpeedIntegralEpsilon();
        var ok = AnimSpeedKnotBake.TrySolveTailAndBake(timeline, out var bake, out var error, epsilon);
        if (!ok)
        {
            EditorGUILayout.HelpBox("【非法】" + error + "\n保留上次合法 SpeedOverTime。", MessageType.Error);
        }
        else
        {
            var tailInfo = bake.AutoTailUsed
                ? $"AutoTail 求解 End={bake.TailEnd:F3}"
                : "AutoTail 未使用（前段 Bake / 末值保持）";
            EditorGUILayout.HelpBox(
                $"时间：Front[0→{timeline.FrontEndTime:F3}]  缝隙 L={bake.TailLength:F3}\n" +
                $"积分：I_front={bake.FrontIntegral:F4}  B={bake.TailBudget:F4}  I={bake.TotalIntegral:F4}  ε={epsilon:F4}\n" +
                $"倍率：TailStart={bake.TailStart:F3} · {tailInfo}",
                MessageType.Info);
            if (!string.IsNullOrEmpty(bake.Warning))
            {
                EditorGUILayout.HelpBox(bake.Warning, MessageType.Warning);
            }
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(MotionProfileSO.SpeedOverTime)),
                new GUIContent("Speed Over Time（Bake）"));
        }

        var integral = AnimSpeedIntegralMath.IntegrateCurve(profile.SpeedOverTime);
        DrawIntegralStatus(integral, epsilon);
    }

    void TryAutoBakeFreeFrontAutoTail(MotionProfileSO profile, bool recordUndo)
    {
        var timeline = profile.ReadAnimSpeedKnotTimeline();
        var epsilon = profile.ResolveAnimSpeedIntegralEpsilon();
        if (!AnimSpeedKnotBake.TrySolveTailAndBake(timeline, out var bake, out var error, epsilon))
        {
            if (GameMainDebugSettings.AnimSpeed228Log)
            {
                Debug.LogWarning($"[AnimSpeed228] SOLVE FAIL profile={profile.name} {error}");
            }

            return;
        }

        if (recordUndo)
        {
            Undo.RecordObject(profile, "Bake AnimSpeed FreeFrontAutoTail");
        }

        profile.SpeedOverTime = bake.Curve;
        EditorUtility.SetDirty(profile);

        if (GameMainDebugSettings.AnimSpeed228Log)
        {
            Debug.Log(
                $"[AnimSpeed228] SOLVE profile={profile.name} I_front={bake.FrontIntegral:F4} " +
                $"B={bake.TailBudget:F4} End={bake.TailEnd:F3} I={bake.TotalIntegral:F4}");
        }
    }

    static void EnsureDefaultKnotTimeline(MotionProfileSO profile)
    {
        var timeline = profile.ReadAnimSpeedKnotTimeline();
        if (timeline.TryValidate(out _))
        {
            return;
        }

        Undo.RecordObject(profile, "Init AnimSpeed Knot Timeline");
        profile.WriteAnimSpeedKnotTimeline(AnimSpeedKnotTimeline.CreateDefault());
        EditorUtility.SetDirty(profile);
    }

    static float ClampKnotTime(in AnimSpeedKnotTimeline timeline, int index, float value)
    {
        if (index <= 0)
        {
            return 0f;
        }

        var min = timeline.Times[index - 1] + KnotDelta;
        float max;
        if (index + 1 < timeline.KnotCount)
        {
            max = timeline.Times[index + 1] - KnotDelta;
        }
        else if (timeline.AutoTailEnabled)
        {
            max = 1f - Mathf.Max(KnotDelta, timeline.TailMinLength);
        }
        else
        {
            max = 1f;
        }

        return Mathf.Clamp(value, min, max);
    }

    static void InsertKnotBeforeTail(ref AnimSpeedKnotTimeline timeline)
    {
        var k = timeline.KnotCount;
        if (k < 2 || timeline.FrontSegmentCount >= AnimSpeedKnotTimeline.SoftMaxFrontSegments)
        {
            return;
        }

        var last = k - 1;
        var prev = last - 1;
        var midT = 0.5f * (timeline.Times[prev] + timeline.Times[last]);
        var midV = 0.5f * (timeline.LeaveValues[prev] + timeline.ArriveValues[last]);

        var times = new float[k + 1];
        var arrive = new float[k + 1];
        var leave = new float[k + 1];
        var joins = new AnimSpeedJoinMode[k + 1];
        var shapes = new AnimSpeedSegmentShapePreset[k];

        for (var i = 0; i < last; i++)
        {
            times[i] = timeline.Times[i];
            arrive[i] = timeline.ArriveValues[i];
            leave[i] = timeline.LeaveValues[i];
            joins[i] = timeline.Joins[i];
        }

        times[last] = midT;
        arrive[last] = midV;
        leave[last] = midV;
        joins[last] = AnimSpeedJoinMode.Continuous;

        times[last + 1] = timeline.Times[last];
        arrive[last + 1] = timeline.ArriveValues[last];
        leave[last + 1] = timeline.LeaveValues[last];
        joins[last + 1] = timeline.Joins[last];

        for (var i = 0; i < last - 1; i++)
        {
            shapes[i] = timeline.SegmentShapes[i];
        }

        shapes[last - 1] = AnimSpeedSegmentShapePreset.Linear;
        shapes[last] = timeline.SegmentShapes[last - 1];

        timeline.Times = times;
        timeline.ArriveValues = arrive;
        timeline.LeaveValues = leave;
        timeline.Joins = joins;
        timeline.SegmentShapes = shapes;
    }

    static void RemoveKnotBeforeTail(ref AnimSpeedKnotTimeline timeline)
    {
        var k = timeline.KnotCount;
        if (k <= 2)
        {
            return;
        }

        var remove = k - 2; // 删除 t* 前一个内部/末前结点？蓝图：删除末前段结点 → 删 t* 前一结点，保留 t*
        // 更直观：删除当前 t* 结点之前的那个结点（合并最后两段）
        var times = new float[k - 1];
        var arrive = new float[k - 1];
        var leave = new float[k - 1];
        var joins = new AnimSpeedJoinMode[k - 1];
        var shapes = new AnimSpeedSegmentShapePreset[k - 2];

        var dst = 0;
        for (var i = 0; i < k; i++)
        {
            if (i == remove)
            {
                continue;
            }

            times[dst] = timeline.Times[i];
            arrive[dst] = timeline.ArriveValues[i];
            leave[dst] = timeline.LeaveValues[i];
            joins[dst] = timeline.Joins[i];
            dst++;
        }

        for (var i = 0; i < shapes.Length; i++)
        {
            shapes[i] = i < remove - 1
                ? timeline.SegmentShapes[i]
                : timeline.SegmentShapes[i + 1];
        }

        // 合并后最后一段形状取原最后一段
        if (shapes.Length > 0)
        {
            shapes[shapes.Length - 1] = timeline.SegmentShapes[timeline.FrontSegmentCount - 1];
        }

        timeline.Times = times;
        timeline.ArriveValues = arrive;
        timeline.LeaveValues = leave;
        timeline.Joins = joins;
        timeline.SegmentShapes = shapes;
    }
}
#endif
