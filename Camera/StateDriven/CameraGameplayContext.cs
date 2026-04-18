/// <summary>
/// Default <see cref="ICameraGameplayContext"/> built at runtime from composition root references.
/// </summary>
public sealed class CameraGameplayContext : ICameraGameplayContext
{
    public CameraGameplayContext(ActionCameraController actionCamera, IPlayerManager playerManager)
    {
        ActionCamera = actionCamera;
        PlayerManager = playerManager;
    }

    public ActionCameraController ActionCamera { get; }

    public IPlayerManager PlayerManager { get; }
}
