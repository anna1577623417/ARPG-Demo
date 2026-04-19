using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家状态机管理器 — 4 支柱拓扑的驱动核心。
///
/// ═══ 帧序（每帧 Update）═══
///
/// 1. FlushExpired：清理过期意图
/// 2. TryPeek → BuildFrameContext → TransitionResolver.CanOfferIntent（标签仲裁）
/// 3. Current.TryConsumeGameplayIntent（当前状态决定是否消费并切换）
/// 4. LogicUpdate（当前状态执行逻辑 — 移动/物理/时间轴推进）
///
/// ═══ 支柱拓扑 ═══
///
/// [0] PlayerLocomotionState（默认）
/// [1] PlayerAirborneState
/// [2] PlayerActionState（万能动作）
/// [3] PlayerDeadState（终态）
/// </summary>
[AddComponentMenu("GameMain/Player/Player State Manager")]
public class PlayerStateManager : EntityStateManager<Player>
{
    [SerializeField] private int maxIntentConsumptionsPerFrame = 1;

    protected override List<EntityState<Player>> BuildStateList()
    {
        return new List<EntityState<Player>>
        {
            new PlayerLocomotionState(),
            new PlayerAirborneState(),
            new PlayerActionState(),
            new PlayerDeadState(),
        };
    }

    protected override void OnPreLogicUpdate(float deltaTime)
    {
        if (Entity == null || Current == null)
        {
            return;
        }

        Entity.IntentBuffer.FlushExpired(Time.time);

        for (var i = 0; i < maxIntentConsumptionsPerFrame; i++)
        {
            if (!Entity.IntentBuffer.TryPeek(out var intent))
            {
                break;
            }

            var ctx = Entity.BuildFrameContext(deltaTime);
            if (!TransitionResolver.CanOfferIntent(in ctx, in intent))
            {
                break;
            }

            if (!Current.TryConsumeGameplayIntent(Entity, in ctx, in intent))
            {
                break;
            }

            Entity.IntentBuffer.Pop();
        }
    }
}
