using UnityEngine;

/// <summary>
/// 闪避状态（无敌帧 + 速度曲线驱动）。
///
/// ═══ 设计原则 ═══
///
/// 1. 方向在进入时锁定，过程中不受输入影响
/// 2. 位移由 AnimationCurve 驱动，实现非线性手感（爆发→匀速→衰减）
/// 3. 无敌帧由状态逻辑控制（iFrameStart ~ iFrameEnd 归一化窗口），不依赖动画事件
/// 4. 相机反馈通过 EventBus 解耦（PlayerDodgeStartedEvent / EndedEvent）
///
/// ═══ 参数调整指南 ═══
///
/// Player Inspector 上的参数：
///   dodgeSpeed       — 闪避基础速度
///   dodgeDuration    — 闪避总时长
///   dodgeSpeedCurve  — 速度曲线（横轴=归一化时间，纵轴=速度系数）
///   iFrameStart      — 无敌帧开始（归一化 0~1）
///   iFrameEnd        — 无敌帧结束（归一化 0~1）
/// </summary>
public class PlayerDodgeState : PlayerState
{
    private Vector3 _dodgeDirection;

    protected override void OnEnter(Player player)
    {
        // 方向锁定：进入时取当前移动方向（无输入则用角色正前方）
        _dodgeDirection = player.GetMovementDirectionOrForward();
        player.LookAtDirection(_dodgeDirection, true);
        player.StartDodgeCooldown();
        player.PublishEvent(new PlayerDodgeStartedEvent(player.GetInstanceID(), player.name));
    }

    protected override void OnExit(Player player)
    {
        // 退出时确保关闭无敌
        player.SetInvincible(false);
        player.PublishEvent(new PlayerDodgeEndedEvent(player.GetInstanceID(), player.name));
    }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        float duration = player.DodgeDuration;
        float t = duration > 0f ? TimeSinceEntered / duration : 1f;

        // ─── 无敌帧控制 ───
        // 由归一化时间窗口 [iFrameStart, iFrameEnd] 决定，
        // 不依赖动画事件，不受帧率抖动影响。
        bool shouldBeInvincible = t >= player.IFrameStart && t <= player.IFrameEnd;
        if (shouldBeInvincible != player.IsInvincible)
        {
            player.SetInvincible(shouldBeInvincible);
        }

        // ─── 闪避结束判定 ───
        if (TimeSinceEntered >= duration)
        {
            if (player.HasMovementIntent)
            {
                if (player.WantsRun)
                    player.States.Change<PlayerRunState>();
                else
                    player.States.Change<PlayerWalkState>();
            }
            else
            {
                player.States.Change<PlayerIdleState>();
            }
            return;
        }

        // ─── 曲线驱动位移 ───
        player.ApplyDodgeMotor(_dodgeDirection, t);
    }
}
