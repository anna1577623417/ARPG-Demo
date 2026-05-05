using UnityEngine;

/// <summary>
/// 地面运动支柱（Locomotion Pillar）— Idle/Walk/Run 的合一状态。
///
/// 职责：
/// 1. 维护地面标签（Grounded）与实体能力轨（<see cref="EntityCapabilityTag"/>）
/// 2. 消费意图：Jump → Airborne, Attack/Dodge → Action
/// 3. 驱动移动结算（MoveByLocomotionIntent + ApplyMotor）
/// </summary>
public sealed class PlayerLocomotionState : PlayerState
{
    /// <summary>
    /// 由 PlayerStateManager 注入的"打断许可掩码"（StateTag.AllowInterruptBy* 位）。
    /// 设计：Locomotion 是无总时长的连续物理状态，没有 t∈[0,1] 概念，因此使用
    /// 整段掩码做闸门，而非 ActionWindow 的时间切片。
    /// </summary>
    private readonly ulong m_allowedInterrupts;

    /// <summary>原地转身解析器（仅在本状态生命周期内活跃；离开时 ClearLock）。</summary>
    private readonly TurnResolver m_turnResolver;

    public PlayerLocomotionState(ulong allowedInterrupts, in TurnSettings turnSettings)
    {
        m_allowedInterrupts = allowedInterrupts;
        m_turnResolver = new TurnResolver(in turnSettings);
    }

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        if (!IntentRouter.IsRoutable(intent.Kind))
        {
            return false;
        }

        // 状态级闸门：用 AllowInterruptBy* 位与 m_allowedInterrupts 做交集。
        // 与 ActionInterruptResolver 共用同一套 Intent → 标签映射，保持单一真相源。
        var requiredTag = ActionInterruptResolver.MapIntentToInterruptTag(intent.Kind);
        if (requiredTag != 0UL && (m_allowedInterrupts & requiredTag) == 0UL)
        {
            if (player.DebugInterruptFlow)
            {
                Debug.Log(
                    $"[Locomotion] REJECT | intent={intent.Kind} | reason=not in locomotionAllowedInterrupts",
                    player);
            }
            return false;
        }

        return IntentRouter.Route(player, in intent, forceActionReentry: false);
    }

    protected override void OnEnter(Player player)
    {
        RefreshLocomotionTags(player);
        m_turnResolver.ClearLock();
        player.SetTurnInfo(default);
    }

    protected override void OnExit(Player player)
    {
        // 离开 Locomotion 必须清除转身锁定，否则下次回到 Locomotion 第一帧仍会处于"locked"状态。
        m_turnResolver.ClearLock();
        player.SetTurnInfo(default);
    }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        if (!player.IsGrounded)
        {
            player.States.Change<PlayerAirborneState>();
            return;
        }

        RefreshLocomotionTags(player);

        // ★ 关键时序：在 MoveByLocomotionIntent (含 LookAtDirection) 之前采样转身意图。
        //   否则 transform.forward 已被本帧旋转推近 intent，angleDiff 永远很小。
        var turnSettings = player.States.LocomotionTurnSettings;
        var turnInfo = m_turnResolver.Tick(player, Time.deltaTime, in turnSettings);
        player.SetTurnInfo(in turnInfo);

        if (turnSettings.DrawTurnDebugRays && player.HasMovementIntent)
        {
            var o = player.transform.position + Vector3.up * 0.08f;
            var f = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            var intent = Vector3.ProjectOnPlane(player.MovementIntent, Vector3.up);
            if (f.sqrMagnitude > 1e-8f)
            {
                Debug.DrawRay(o, f.normalized * 1.25f, Color.cyan);
            }

            if (intent.sqrMagnitude > 1e-8f)
            {
                Debug.DrawRay(o, intent.normalized * 1.25f, Color.yellow);
            }
        }

        if (player.HasMovementIntent)
        {
            player.MoveByLocomotionIntent(1f, player.WantsRun);
        }
        else
        {
            player.StopMove();
        }

        player.ApplyMotor(MotorSolveContext.Locomotion);
        player.TickMobilityCooldowns();
    }

    private static void RefreshLocomotionTags(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Grounded);
        EntityAbilitySystem.Update(player);
    }
}
