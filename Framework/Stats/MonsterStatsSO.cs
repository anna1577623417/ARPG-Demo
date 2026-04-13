using UnityEngine;

/// <summary>
/// 敌人/NPC 属性蓝图占位（便于与玩家共用 <see cref="RuntimeEntityStats"/> 管线）。
/// </summary>
[CreateAssetMenu(fileName = "MonsterStats", menuName = "GameMain/Stats/Monster Stats")]
public class MonsterStatsSO : EntityStatsSO
{
    [Min(0f)] public float AggroRadius = 8f;
}
