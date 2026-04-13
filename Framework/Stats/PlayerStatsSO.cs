using UnityEngine;

/// <summary>
/// 玩家专用属性蓝图（在 <see cref="EntityStatsSO"/> 基础上扩展资源与 ARPG 常用项）。
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats", menuName = "GameMain/Stats/Player Stats")]
public class PlayerStatsSO : EntityStatsSO
{
    [Min(1f)] public float MaxStamina = 100f;
}
