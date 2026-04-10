using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家状态机管理器。
/// 构建玩家的所有状态并注册到状态机。
/// 状态列表中第一个状态（PlayerIdleState）为默认初始状态。
/// </summary>
[AddComponentMenu("GameMain/Player/Player State Manager")]
public class PlayerStateManager : EntityStateManager<Player>
{
    protected override List<EntityState<Player>> BuildStateList()
    {
        return new List<EntityState<Player>>
        {
            new PlayerIdleState(),
            new PlayerWalkState(),
            new PlayerRunState(),
            new PlayerJumpState(),
            new PlayerAttackState(),
            new PlayerDodgeState(),
            new PlayerDeadState()
        };
    }
}
