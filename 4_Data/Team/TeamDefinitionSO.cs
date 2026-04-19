using System;
using UnityEngine;

/// <summary>
/// 队伍成员与角色预制体的数据配置；槽位数固定为 1～8，默认 4；<see cref="PlayerManager"/> 从这里取预制体实例化。
/// </summary>
[CreateAssetMenu(fileName = "TeamDefinition", menuName = "GameMain/Team/Team Definition")]
public class TeamDefinitionSO : ScriptableObject
{
    public const int MinSlots = 1;
    public const int MaxSlots = 8;
    public const int DefaultSlotCount = 4;

    [Serializable]
    public struct MemberSlot
    {
        [Tooltip("该槽位对应的玩家角色根预制体（需挂 Player、PlayerController，并配好 PlayerCameraAnchor）。空槽在循环切换时跳过。")]
        public GameObject playerPrefab;
    }

    [Tooltip("队伍槽位数量（1～8），默认 4；每个元素对应键位 1～8 中的同序号槽。")]
    [SerializeField] private MemberSlot[] slots = new MemberSlot[DefaultSlotCount];

    public int SlotCount => slots != null ? slots.Length : 0;

    public GameObject GetPlayerPrefab(int slotIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
        {
            return null;
        }

        return slots[slotIndex].playerPrefab;
    }

    private void OnValidate()
    {
        if (slots == null || slots.Length < MinSlots)
        {
            slots = new MemberSlot[DefaultSlotCount];
            return;
        }

        if (slots.Length > MaxSlots)
        {
            Array.Resize(ref slots, MaxSlots);
        }
    }
}
