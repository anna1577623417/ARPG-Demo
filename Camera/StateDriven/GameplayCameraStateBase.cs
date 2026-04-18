/// <summary>
/// One camera behaviour mode (free look, switch blend, lock-on, …). Tick runs in <c>Update</c>
/// so yaw overrides are visible in the same frame before <see cref="CameraController.LateUpdate"/>.
/// </summary>
public abstract class GameplayCameraStateBase
{
    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    /// <param name="deltaTime">Scaled delta time unless callers pass unscaled explicitly later.</param>
    public virtual void Tick(float deltaTime)
    {
    }
}
