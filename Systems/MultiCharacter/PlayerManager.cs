using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene lifetime service: activates/deactivates party pawns and resolves spawn pose by <see cref="SwitchSpawnPolicy"/>.
/// </summary>
public sealed class PlayerManager : IPlayerManager
{
    private readonly PlayerCharacter[] _party;
    private readonly SwitchSpawnPolicy _spawnPolicy;
    private readonly SpawnPoint _primarySpawn;
    private readonly Transform[] _slotAnchors;
    private readonly IGameplayEventBus _eventBus;

    private int _activeIndex = -1;

    public PlayerManager(
        PlayerCharacter[] party,
        SwitchSpawnPolicy spawnPolicy,
        SpawnPoint primarySpawn,
        Transform[] slotAnchors,
        IGameplayEventBus eventBus)
    {
        _party = party ?? System.Array.Empty<PlayerCharacter>();
        _spawnPolicy = spawnPolicy;
        _primarySpawn = primarySpawn;
        _slotAnchors = slotAnchors;
        _eventBus = eventBus;
    }

    public IReadOnlyList<PlayerCharacter> Party => _party;

    public PlayerCharacter ActiveCharacter =>
        _activeIndex >= 0 && _activeIndex < _party.Length ? _party[_activeIndex] : null;

    public int ActiveSlotIndex => _activeIndex;

    /// <summary>Call once after construction to pick first valid slot and publish events.</summary>
    public void ActivateInitialMember()
    {
        var first = FindNextOccupiedIndex(-1, 1);
        if (first < 0)
        {
            Debug.LogWarning("[PlayerManager] No party members spawned.");
            return;
        }

        ApplySwitch(-1, first, forcePoseFromPrimarySpawn: true);
    }

    public bool TrySwitchToSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _party.Length || _party[slotIndex] == null)
        {
            return false;
        }

        if (slotIndex == _activeIndex)
        {
            return true;
        }

        ApplySwitch(_activeIndex, slotIndex, forcePoseFromPrimarySpawn: false);
        return true;
    }

    public void SwitchNextOccupiedSlot()
    {
        var idx = FindNextOccupiedIndex(_activeIndex, 1);
        if (idx < 0 || idx == _activeIndex)
        {
            return;
        }

        ApplySwitch(_activeIndex, idx, forcePoseFromPrimarySpawn: false);
    }

    public void SwitchPreviousOccupiedSlot()
    {
        var idx = FindNextOccupiedIndex(_activeIndex, -1);
        if (idx < 0 || idx == _activeIndex)
        {
            return;
        }

        ApplySwitch(_activeIndex, idx, forcePoseFromPrimarySpawn: false);
    }

    private int FindNextOccupiedIndex(int startIndex, int direction)
    {
        if (_party.Length == 0)
        {
            return -1;
        }

        var idx = startIndex;
        for (var k = 0; k < _party.Length * 2; k++)
        {
            idx += direction;
            if (idx >= _party.Length)
            {
                idx = 0;
            }
            else if (idx < 0)
            {
                idx = _party.Length - 1;
            }

            if (_party[idx] != null)
            {
                return idx;
            }
        }

        return -1;
    }

    private void ApplySwitch(int fromIndex, int toIndex, bool forcePoseFromPrimarySpawn)
    {
        var previous = fromIndex >= 0 && fromIndex < _party.Length ? _party[fromIndex] : null;
        var next = _party[toIndex];

        Vector3 pos;
        Quaternion rot;

        if (forcePoseFromPrimarySpawn || _spawnPolicy == SwitchSpawnPolicy.PrimarySpawnOnly)
        {
            ResolvePrimarySpawnPose(out pos, out rot);
        }
        else if (_spawnPolicy == SwitchSpawnPolicy.SlotAnchors)
        {
            if (!TryResolveAnchorPose(toIndex, out pos, out rot))
            {
                ResolvePrimarySpawnPose(out pos, out rot);
            }
        }
        else
        {
            // Inherit previous active pose when possible
            if (previous != null)
            {
                pos = previous.transform.position;
                rot = previous.transform.rotation;
            }
            else
            {
                ResolvePrimarySpawnPose(out pos, out rot);
            }
        }

        if (previous != null)
        {
            SetPawnActive(previous, false);
        }

        next.transform.SetPositionAndRotation(pos, rot);
        SetPawnActive(next, true);

        _activeIndex = toIndex;

        var evt = new ActiveCharacterChangedEvent(previous, next, toIndex);
        _eventBus?.Publish(in evt);
    }

    private void ResolvePrimarySpawnPose(out Vector3 pos, out Quaternion rot)
    {
        if (_primarySpawn != null)
        {
            pos = _primarySpawn.transform.position;
            rot = _primarySpawn.transform.rotation;
        }
        else
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
        }
    }

    private bool TryResolveAnchorPose(int slotIndex, out Vector3 pos, out Quaternion rot)
    {
        if (_slotAnchors != null && slotIndex >= 0 && slotIndex < _slotAnchors.Length && _slotAnchors[slotIndex] != null)
        {
            pos = _slotAnchors[slotIndex].position;
            rot = _slotAnchors[slotIndex].rotation;
            return true;
        }

        pos = default;
        rot = default;
        return false;
    }

    private static void SetPawnActive(PlayerCharacter pawn, bool on)
    {
        if (pawn == null)
        {
            return;
        }

        var pc = pawn.GetComponent<PlayerController>();
        if (!on)
        {
            if (pc != null)
            {
                pc.enabled = false;
            }

            pawn.gameObject.SetActive(false);
            return;
        }

        pawn.gameObject.SetActive(true);
        var states = pawn.Player != null ? pawn.Player.States : null;
        var isDead = states != null && states.Current is PlayerDeadState;

        if (pc != null)
        {
            pc.enabled = !isDead;
        }
    }
}
