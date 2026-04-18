/// <summary>
/// Owns the active <see cref="GameplayCameraStateBase"/>; gameplay systems switch modes here instead of
/// calling <see cref="ActionCameraController"/> ad hoc.
/// </summary>
public sealed class GameplayCameraStateMachine
{
    private GameplayCameraStateBase _current;

    public GameplayCameraStateBase Current => _current;

    public void Switch(GameplayCameraStateBase next)
    {
        _current?.Exit();
        _current = next;
        _current?.Enter();
    }

    public void Tick(float deltaTime)
    {
        _current?.Tick(deltaTime);
    }
}
