using UnityEngine;

/// <summary>
/// 测试场景里挂一个就能开关 InputActionProbe，不需要走 Console。
/// 调试完直接禁用本 MonoBehaviour 或删除即可。
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class InputActionProbeSwitch : MonoBehaviour
{
    [Tooltip("总开关。运行时勾掉立刻关日志。")]
    public bool Enabled = true;

    [Tooltip("只跟踪这个 Player。留空 = 全部 Player 都打。")]
    public Player TrackedPlayer;

    [Tooltip("心跳类日志的最小间隔（秒）；边沿事件不受此影响。")]
    [Range(0.05f, 1f)] public float HeartbeatIntervalSec = 0.25f;

    [Tooltip("Slide 一帧位移超过此值（米）即视为闪现。")]
    [Range(0.05f, 1f)] public float SlideBurstThresholdMeters = 0.30f;

    void OnEnable() => Apply();
    void OnDisable()
    {
        InputActionProbe.Enabled = false;
        InputActionProbe.SetTrackedPlayer(null);
    }
    void OnValidate() { if (isActiveAndEnabled) Apply(); }

    void Apply()
    {
        InputActionProbe.Enabled = Enabled;
        InputActionProbe.HeartbeatIntervalSec = HeartbeatIntervalSec;
        InputActionProbe.SlideBurstThresholdMeters = SlideBurstThresholdMeters;
        InputActionProbe.SetTrackedPlayer(TrackedPlayer);
        Debug.Log(InputActionProbe.DescribeState(), this);
    }

    [ContextMenu("Print State")]
    void PrintState() => Debug.Log(InputActionProbe.DescribeState(), this);
}
