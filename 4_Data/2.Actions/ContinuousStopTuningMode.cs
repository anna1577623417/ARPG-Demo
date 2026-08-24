/// <summary>238.1 — 连续 Stop 的策划主控量。</summary>
public enum ContinuousStopTuningMode : byte
{
    /// <summary>满速停止距离 D_ref；既有资产与默认值保持此模式。</summary>
    FullSpeedDistance = 0,

    /// <summary>满速停止时长 T_ref；运行时换算为 D_ref=0.5×V_ref×T_ref。</summary>
    FullSpeedDuration = 1,
}
