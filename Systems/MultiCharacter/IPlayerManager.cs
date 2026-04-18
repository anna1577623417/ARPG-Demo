using System.Collections.Generic;

/// <summary>
/// Owning authority for which party member receives gameplay input and world placement on switch.
/// </summary>
public interface IPlayerManager
{
    IReadOnlyList<PlayerCharacter> Party { get; }

    PlayerCharacter ActiveCharacter { get; }

    int ActiveSlotIndex { get; }

    bool TrySwitchToSlot(int slotIndex);

    void SwitchNextOccupiedSlot();

    void SwitchPreviousOccupiedSlot();
}
