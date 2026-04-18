using UnityEngine;

/// <summary>
/// Data asset describing party size and per-slot prefabs (nullable slots allowed).
/// </summary>
[CreateAssetMenu(fileName = "TeamDefinition", menuName = "GameMain/MultiCharacter/Team Definition")]
public class TeamDefinition : ScriptableObject
{
    [SerializeField, Range(1, 8)] private int teamSize = 4;

    [Tooltip("Index = party slot. Null entries are skipped at spawn.")]
    [SerializeField] private GameObject[] characterPrefabs = new GameObject[4];

    public int TeamSize => teamSize;

    public GameObject GetPrefabForSlot(int slotIndex)
    {
        if (characterPrefabs == null || slotIndex < 0 || slotIndex >= characterPrefabs.Length)
        {
            return null;
        }

        return characterPrefabs[slotIndex];
    }

    public int PrefabArrayLength => characterPrefabs != null ? characterPrefabs.Length : 0;
}
