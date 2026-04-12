using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家状态机管理器。
/// 四支柱拓扑：Locomotion → Airborne → Action → Dead（列表首项为默认状态）。
/// 帧序：先过期清理意图 → 标签仲裁 + 当前状态消费意图 → 再执行 LogicUpdate。
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
