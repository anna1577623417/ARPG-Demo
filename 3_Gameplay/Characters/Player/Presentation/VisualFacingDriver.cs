using UnityEngine;

/// <summary>
/// 184.1 Layer 3 — 表现朝向缓追 LogicForward。
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

        if (m_animController != null && m_animController.IsPlayingTurnPresentation)
        {
            return;
        }

        var logicForward = m_player.LogicForward;
        if (logicForward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var target = Quaternion.LookRotation(logicForward, Vector3.up);
        var deltaAngle = Quaternion.Angle(transform.rotation, target);
        var maxSpeed = deltaAngle >= m_fastTriggerAngleDeg
            ? m_fastAngularSpeedDeg
            : m_baseAngularSpeedDeg;

        if (maxSpeed <= 0f)
        {
            transform.rotation = target;
            return;
        }

        TurnProbe.LogVisualLag(m_player, deltaAngle, maxSpeed);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            target,
            maxSpeed * Time.deltaTime);
    }
}
