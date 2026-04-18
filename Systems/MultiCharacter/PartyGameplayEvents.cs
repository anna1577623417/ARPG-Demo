using UnityEngine;

/// <summary>
/// Fired when the controllable party member changes. Listen via <see cref="IGameplayEventBus"/> or <see cref="GlobalEventBus"/>.
/// </summary>
public readonly struct ActiveCharacterChangedEvent : IGameEvent
{
    public readonly PlayerCharacter Previous;
    public readonly PlayerCharacter Current;
    public readonly int SlotIndex;

    public ActiveCharacterChangedEvent(PlayerCharacter previous, PlayerCharacter current, int slotIndex)
    {
        Previous = previous;
        Current = current;
        SlotIndex = slotIndex;
    }

    /// <summary>Active player root — useful for parenting VFX.</summary>
    public Transform ActiveRoot => Current != null ? Current.transform : null;

    /// <summary>相机世界位置采样点（与 <see cref="PlayerCharacter.CameraFollowOrRoot"/> 一致）。</summary>
    public Transform CameraFollowTargetOrRoot =>
        Current != null ? Current.CameraFollowOrRoot : null;

    /// <summary>Cinemachine <b>Look At</b>（与 <see cref="PlayerCharacter.CameraLookAtOrFollow"/> 一致）。</summary>
    public Transform CameraLookAtTargetOrFollow =>
        Current != null ? Current.CameraLookAtOrFollow : null;
}
