/// <summary>Read-only adapter boundary. Implementations may read Gameplay facts but never write them.</summary>
public interface IAnimationObservationSource
{
    bool TryCapture(out AnimationObservation observation);
}
