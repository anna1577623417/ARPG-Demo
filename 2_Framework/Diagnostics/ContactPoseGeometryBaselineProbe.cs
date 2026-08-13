using System.Text;
using UnityEngine;

/// <summary>
/// 224.1 L0 — Contact Pose/Geometry 单次基线快照（Runtime 安全，无 Editor 依赖）。
/// Preview 捕获与菜单入口在 Editor 侧扩展。
/// </summary>
public static class ContactPoseGeometryBaselineProbe
{
    public const string LogPrefix = "[224.0][Baseline]";

    static bool s_runtimeCaptured;
    static bool s_gcSampleLogged;

    public static bool IsEnabled => GameMainDebugSettings.CombatContactBaseline;

    public static void ResetOneShotFlags()
    {
        s_runtimeCaptured = false;
        s_gcSampleLogged = false;
    }

    public static void TryCaptureRuntimeOnce(
        string actionName,
        string eventId,
        uint lease,
        in ResolvedContactSpec spec,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        if (!IsEnabled || s_runtimeCaptured) return;
        s_runtimeCaptured = true;

        var sb = new StringBuilder(256);
        sb.Append(LogPrefix).Append(" RUNTIME action=").Append(Safe(actionName));
        sb.Append(" eventId=").Append(Safe(eventId));
        sb.Append(" lease=").Append(lease);
        sb.Append(" defRev=").Append(spec.DefinitionRevision);
        sb.Append(" motion=").Append(spec.Motion);
        sb.Append(" shapeMode=").Append(spec.ShapeMode);
        sb.Append(" pos=").Append(Format(worldPosition));
        sb.Append(" rot=").Append(Format(worldRotation.eulerAngles));
        AppendGeometry(sb, spec.Geometry);
        Debug.Log(sb.ToString());
        LogGcSample("runtime-begin");
    }

    public static void LogPreviewSnapshot(
        string reason,
        string actionName,
        string eventId,
        float previewTime,
        float activeStart,
        float activeEnd,
        in ResolvedContactSpec spec,
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool activeAtPreview)
    {
        if (!IsEnabled) return;

        var sb = new StringBuilder(320);
        sb.Append(LogPrefix).Append(" PREVIEW reason=").Append(Safe(reason));
        sb.Append(" action=").Append(Safe(actionName));
        sb.Append(" eventId=").Append(Safe(eventId));
        sb.Append(" previewTime=").Append(previewTime.ToString("F3"));
        sb.Append(" activeRange=[").Append(activeStart.ToString("F3"));
        sb.Append("..").Append(activeEnd.ToString("F3")).Append(']');
        sb.Append(" defRev=").Append(spec.DefinitionRevision);
        sb.Append(" motion=").Append(spec.Motion);
        sb.Append(" shapeMode=").Append(spec.ShapeMode);
        sb.Append(" origin=").Append(spec.Origin);
        sb.Append(" pos=").Append(Format(worldPosition));
        sb.Append(" rot=").Append(Format(worldRotation.eulerAngles));
        sb.Append(" activeAtPreview=").Append(activeAtPreview);
        AppendGeometry(sb, spec.Geometry);
        Debug.Log(sb.ToString());
        LogGcSample("preview-capture");
    }

    public static void LogGcSample(string reason, bool force = false)
    {
        if (s_gcSampleLogged && !force && reason != "manual-menu") return;
        s_gcSampleLogged = true;
        var bytes = System.GC.GetTotalMemory(false);
        Debug.Log(
            $"{LogPrefix} GC sample reason={reason} totalMemoryBytes={bytes} " +
            "method=GC.GetTotalMemory(false) once-per-session; " +
            "NOT a frame profiler. Re-measure in L7/L8 with same action/window/event after Pose/Geometry rewrite. " +
            "OPEN: Unity Profiler GC Alloc deep sample requires manual Play Mode capture.");
    }

    static void AppendGeometry(StringBuilder sb, HitShapeSO geometry)
    {
        if (geometry == null)
        {
            sb.Append(" geometry=null");
            return;
        }

        sb.Append(" geometry=").Append(geometry.name);
        sb.Append(" kind=").Append(geometry.GetType().Name);
        switch (geometry)
        {
            case SphereShapeSO sphere:
                sb.Append(" radius=").Append(sphere.radius.ToString("F4"));
                break;
            case BoxShapeSO box:
                sb.Append(" halfExtents=").Append(Format(box.halfExtents));
                sb.Append(" offset=").Append(Format(box.offset));
                break;
            case CapsuleShapeSO capsule:
                sb.Append(" radius=").Append(capsule.radius.ToString("F4"));
                sb.Append(" height=").Append(capsule.height.ToString("F4"));
                sb.Append(" offset=").Append(Format(capsule.offset));
                break;
            case ConeShapeSO cone:
                sb.Append(" length=").Append(cone.length.ToString("F4"));
                sb.Append(" angle=").Append(cone.angleDegrees.ToString("F2"));
                sb.Append(" offset=").Append(Format(cone.offset));
                break;
            case RingShapeSO ring:
                sb.Append(" inner=").Append(ring.innerRadius.ToString("F4"));
                sb.Append(" outer=").Append(ring.outerRadius.ToString("F4"));
                sb.Append(" offset=").Append(Format(ring.offset));
                break;
            case BeamShapeSO beam:
                sb.Append(" w=").Append(beam.width.ToString("F4"));
                sb.Append(" h=").Append(beam.height.ToString("F4"));
                sb.Append(" len=").Append(beam.length.ToString("F4"));
                sb.Append(" offset=").Append(Format(beam.offset));
                break;
        }
    }

    static string Format(Vector3 v) =>
        $"({v.x:F3},{v.y:F3},{v.z:F3})";

    static string Safe(string value) =>
        string.IsNullOrEmpty(value) ? "-" : value;
}
