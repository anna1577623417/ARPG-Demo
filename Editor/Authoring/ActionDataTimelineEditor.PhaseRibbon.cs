#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 216.3 M0 L2 — Phase 衍生只读带渲染。
/// <para>由 <see cref="PhaseDerivation"/> 从 判定窗(Hitbox) + 打断窗(Interrupt) 衍生 前摇/判定/后摇，
/// 三色绘制。<b>只读</b>——不可点击创建/拖拽（拦截见主文件 HandleTimelineInput / IsDerivedTrack）。</para>
/// </summary>
public sealed partial class ActionDataTimelineEditor
{
    static readonly Color PhaseStartupColor = new Color(0.35f, 0.55f, 0.95f, 0.72f);
    static readonly Color PhaseActiveColor = new Color(0.2f, 0.78f, 0.45f, 0.8f);
    static readonly Color PhaseRecoveryColor = new Color(0.6f, 0.42f, 0.92f, 0.72f);

    GUIStyle _phaseRibbonLabelStyle;

    void DrawPhaseRibbon(Rect barRect)
    {
        if (_phaseRibbonLabelStyle == null)
        {
            _phaseRibbonLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.92f) },
            };
        }

        var spans = PhaseDerivation.Compute(_action);

        if (spans.HasActive)
        {
            DrawPhaseSegment(barRect, 0f, spans.StartupEnd, PhaseStartupColor, "前摇");
            DrawPhaseSegment(barRect, spans.ActiveStart, spans.ActiveEnd, PhaseActiveColor, "判定");
            DrawPhaseSegment(barRect, spans.RecoveryStart, spans.RecoveryEnd, PhaseRecoveryColor, "后摇");
        }
        else
        {
            DrawPhaseSegment(barRect, 0f, spans.StartupEnd, PhaseStartupColor, "前摇");
            DrawPhaseSegment(barRect, spans.RecoveryStart, spans.RecoveryEnd, PhaseRecoveryColor, "后摇");
        }
    }

    void DrawPhaseSegment(Rect barRect, float from, float to, Color color, string label)
    {
        if (to <= from + 0.0001f)
        {
            return;
        }

        var x0 = TimeToX(barRect, from);
        var x1 = TimeToX(barRect, to);
        var seg = new Rect(x0, barRect.y + 2f, Mathf.Max(2f, x1 - x0), barRect.height - 4f);
        EditorGUI.DrawRect(seg, color);

        if (seg.width >= 30f)
        {
            GUI.Label(seg, label, _phaseRibbonLabelStyle);
        }
    }
}
#endif
