/// <summary>
/// Shared handles for gameplay camera states (free look, switch blend, future lock-on / aim / skill).
/// </summary>
public interface ICameraGameplayContext
{
    ActionCameraController ActionCamera { get; }

    IPlayerManager PlayerManager { get; }
}
