using UnityEngine;
using UnityEngine.UIElements;

/// <summary>PlayMode read-only overlay. Live canary facts wait for 243.9; this layer never writes authoring data.</summary>
public sealed class AnimTransitionTraceOverlay
{
    readonly Label label;

    public AnimTransitionTraceOverlay(Label target)
    {
        label = target;
    }

    public void ShowReadOnlyState(CompiledAnimTransitionGraph compiled)
    {
        if (label == null) return;
        var hash = compiled != null ? compiled.GraphHash : "uncompiled";
        var version = compiled != null ? compiled.SchemaVersion.ToString() : "-";
        label.text = Application.isPlaying
            ? "PlayMode Read-Only · hash=" + hash
              + " schema=" + version
              + " rules=" + (compiled != null ? compiled.RuleCount.ToString() : "0")
              + " log=" + (AnimationTransitionGraphTrace243.IsEnabled ? "on" : "off")
              + " canary=" + DescribeCanary()
            : string.Empty;
    }

    static string DescribeCanary()
    {
        return string.Concat(
            Describe(AnimationRequestDomain.Turn), ",",
            Describe(AnimationRequestDomain.Locomotion), ",",
            Describe(AnimationRequestDomain.Airborne), ",",
            Describe(AnimationRequestDomain.Action));
    }

    static string Describe(AnimationRequestDomain domain)
    {
        if (!AnimationTransitionCanaryStatusRegistry243.TryGet(domain, out var status))
        {
            return domain + ":idle";
        }

        var route = status.CanSubmitPlan ? "canary" : status.CanEvaluateShadow ? "shadow" : "hold";
        return domain + ":" + route + "(" + status.Reason + ")";
    }
}
