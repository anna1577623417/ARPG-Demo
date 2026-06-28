using UnityEngine;

/// <summary>
/// 测试场景里挂一个就能开关 ActionTurnProbe。
/// 调试完直接禁用或删除即可。
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class ActionTurnProbeSwitch : MonoBehaviour
{
    [Tooltip("总开关。运行时勾掉立刻关日志。")]
    public bool Enabled = true;

    [Tooltip("只跟踪这个 Player。留空 = 全部 Player 都打。")]
    public Player TrackedPlayer;

    [Tooltip("角度差阈值（度）；低于此值不报。")]
    [Range(0.1f, 30f)] public float MinDeltaDeg = 1f;

    void OnEnable() => Apply();
    void OnDisable()
    {
        ActionTurnProbe.Enabled = false;
        ActionTurnProbe.SetTrackedPlayer(null);
    }
    void OnValidate() { if (isActiveAndEnabled) Apply(); }

    void Apply()
    {
        ActionTurnProbe.Enabled = Enabled;
        ActionTurnProbe.MinDeltaDeg = MinDeltaDeg;
        ActionTurnProbe.SetTrackedPlayer(TrackedPlayer);
        Debug.Log(ActionTurnProbe.DescribeState(), this);
    }

    [ContextMenu("Print State")]
    void PrintState() => Debug.Log(ActionTurnProbe.DescribeState(), this);
}
