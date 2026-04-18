using UnityEngine;

/// <summary>
/// Instantiates party members under a parent transform from <see cref="TeamDefinition"/>.
/// </summary>
public static class PlayerPartyFactory
{
    /// <returns>Length = definition.TeamSize; empty slots remain null.</returns>
    public static PlayerCharacter[] SpawnParty(
        TeamDefinition definition,
        SpawnPoint primarySpawn,
        Transform partyParent,
        InputReader sharedInput)
    {
        if (definition == null)
        {
            Debug.LogError("[PlayerPartyFactory] TeamDefinition is null.");
            return System.Array.Empty<PlayerCharacter>();
        }

        var teamSize = Mathf.Clamp(definition.TeamSize, 1, 8);
        var results = new PlayerCharacter[teamSize];

        var spawnPos = primarySpawn != null ? primarySpawn.transform.position : Vector3.zero;
        var spawnRot = primarySpawn != null ? primarySpawn.transform.rotation : Quaternion.identity;

        for (var i = 0; i < teamSize; i++)
        {
            var prefab = definition.GetPrefabForSlot(i);
            if (prefab == null)
            {
                continue;
            }

            var instance = Object.Instantiate(prefab, spawnPos, spawnRot, partyParent);
            instance.SetActive(false);
            var pc = instance.GetComponent<PlayerCharacter>();
            if (pc == null)
            {
                pc = instance.AddComponent<PlayerCharacter>();
            }

            pc.BindPartySlot(i, sharedInput);
            results[i] = pc;
        }

        return results;
    }
}
