using UnityEngine;

/// <summary>
/// 234.5/235.2 — 普通 Locomotion 表现朝向同步 LogicForward；active Turn Lease 可短暂持有 Visual。
/// <para>挂在 VisualRoot（Animator 子物体）；Gameplay 只读 LogicForward，不读本组件 rotation。</para>
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class VisualFacingDriver : MonoBehaviour
{
    [SerializeField] Player m_player;
    [SerializeField] float m_baseAngularSpeedDeg = 540f;
    [SerializeField] float m_fastAngularSpeedDeg = 1440f;
    [SerializeField] float m_fastTriggerAngleDeg = 60f;

    PlayerAnimController m_animController;

    public void Bind(Player player, float baseAngularSpeedDeg, float fastAngularSpeedDeg = 1440f,
        float fastTriggerAngleDeg = 60f)
    {
        m_player = player;
        m_baseAngularSpeedDeg = Mathf.Max(0f, baseAngularSpeedDeg);
        m_fastAngularSpeedDeg = Mathf.Max(m_baseAngularSpeedDeg, fastAngularSpeedDeg);
        m_fastTriggerAngleDeg = Mathf.Clamp(fastTriggerAngleDeg, 1f, 179f);
        if (m_player != null)
        {
            m_animController = m_player.GetComponent<PlayerAnimController>();
        }
    }

    void LateUpdate()
    {
        if (m_player == null)
        {
            return;
        }

        var visualYawBefore = transform.eulerAngles.y;
        var logicForward = m_player.LogicForward;
        var logicYaw = logicForward.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(logicForward.x, logicForward.z) * Mathf.Rad2Deg
            : float.NaN;
        // 235.2：只有真正正在输出的 Turn Clip 才能持有 Visual。Cue pending 不得冻结骨架，
        // 缺片、被 Action 抢占或延迟消费时都直接同步 LogicFacing。
        var heldByTurnPresentation = m_animController != null && m_animController.IsPlayingTurnPresentation;
        if (heldByTurnPresentation)
        {
            CameraTurn233Probe.ObserveVisual(
                m_player,
                visualYawBefore,
                visualYawBefore,
                logicYaw,
                Mathf.Abs(Mathf.DeltaAngle(visualYawBefore, logicYaw)),
                true,
                0f);
            CharacterTurnDisplacement233Probe.ObserveVisual(
                m_player,
                visualYawBefore,
                visualYawBefore,
                logicYaw,
                true,
                0f);
            return;
        }

        if (logicForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var target = Quaternion.LookRotation(logicForward, Vector3.up);
        var deltaAngle = Quaternion.Angle(transform.rotation, target);
        transform.rotation = target;
        CameraTurn233Probe.ObserveVisual(
            m_player,
            visualYawBefore,
            transform.eulerAngles.y,
            logicYaw,
            deltaAngle,
            false,
            0f);
        CharacterTurnDisplacement233Probe.ObserveVisual(
            m_player,
            visualYawBefore,
            transform.eulerAngles.y,
            logicYaw,
            false,
            0f);
    }
}
