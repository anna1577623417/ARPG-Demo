/// <summary>Pure presentation-layer conflict policy. It never changes Action lease or Gameplay state.</summary>
public static class AnimationLayerPolicy243
{
    public static bool CanCoexist(in TransitionPlan basePlan, in TransitionPlan overlayPlan)
    {
        return basePlan.ShouldSubmitPlayback
            && overlayPlan.ShouldSubmitPlayback
            && !basePlan.IsRejected
            && !overlayPlan.IsRejected
            && basePlan.EntityInstanceId == overlayPlan.EntityInstanceId
            && basePlan.Layer != overlayPlan.Layer
            && !string.IsNullOrEmpty(overlayPlan.SyncGroup)
            && basePlan.SpatialHandoffMode == SpatialHandoffMode.SameSpace
            && overlayPlan.SpatialHandoffMode == SpatialHandoffMode.SameSpace;
    }
}
