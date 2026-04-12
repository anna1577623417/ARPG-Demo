using System;
using UnityEngine;

/// <summary>
/// 归一化时间轴上的一段窗口，并绑定到标签掩码。
/// 用于在 Action 支柱内根据播放进度刷新 <see cref="StateTag"/>（起手/判定/收招、可取消窗等）。
/// </summary>
[Serializable]
public struct ActionWindow
{
    [Range(0f, 1f)] public float NormalizedStart;

    [Range(0f, 1f)] public float NormalizedEnd;

    /// <summary>在此窗口内叠加到实体上的标签（Phase + Ability 等）。</summary>
    public ulong Tags;
}
