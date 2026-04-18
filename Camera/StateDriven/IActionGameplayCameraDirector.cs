using UnityEngine;

/// <summary>
/// Scene service for party-driven Action camera modes. Resolve from <see cref="IServiceResolver"/> after <see cref="SystemRoot"/> boot.
/// </summary>
public interface IActionGameplayCameraDirector
{
    GameplayCameraStateMachine StateMachine { get; }

    bool IsLockOnActive { get; }

    void RequestLockOn(Transform enemy);

    void RequestUnlock();
}
