using System;
using UnityEngine;

/// <summary>
/// 队伍成员与角色预制体的数据配置；<see cref="PlayerManager"/> 从这里取预制体实例化。
/// </summary>
[CreateAssetMenu(fileName = "TeamDefinition", menuName = "GameMain/Team/Team Definition")]
public class TeamDefinitionSO : ScriptableObject
{
    [Serializable]
    public struct MemberSlot
    {
        [Tooltip("该槽位对应的玩家角色根预制体（需挂 Player、PlayerController，并配好 PlayerCameraAnchor）。")]
        public GameObject playerPrefab;
    }

    [Tooltip("按槽位索引与多角色切换顺序使用；当前仅槽 0 会由 PlayerManager 在开局生成。")]
    public MemberSlot[] slots = new MemberSlot[1];

    public GameObject GetPlayerPrefab(int slotIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
        {
            return null;
        }

        return slots[slotIndex].playerPrefab;
    }
}
