using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 数据驱动动作资产：逻辑层读取窗口与时长，表现层读取主 Clip。
/// 动画控制器应监听局部总线事件而非直接引用状态类。
/// </summary>
[CreateAssetMenu(fileName = "NewAction", menuName = "GameMain/Action/Action Data")]
public class ActionDataSO : ScriptableObject
{
    [Tooltip("主表现用片段；复杂动作可后续扩展多轨道。")]
    public AnimationClip MainClip;

    [Tooltip("逻辑时长（秒）。与动画长度可不同，用于先行手感调参。")]
    public float Duration = 0.4f;

    [Tooltip("归一化时间轴上的标签切片。")]
    public List<ActionWindow> Windows = new List<ActionWindow>();

    /// <summary>按归一化进度计算本帧应叠加的标签并写入 mask（先清除 Phase 再叠加）。</summary>
    public void EvaluatePhaseTags(float normalizedTime, ref GameplayTagMask mask)
    {
        var phaseMask = (ulong)(StateTag.PhaseStartup | StateTag.PhaseActive | StateTag.PhaseRecovery);
        mask.Remove(phaseMask);

        if (Windows == null || Windows.Count == 0)
        {
            return;
        }

        var t = Mathf.Clamp01(normalizedTime);
        for (int i = 0; i < Windows.Count; i++)
        {
            var w = Windows[i];
            if (t >= w.NormalizedStart && t <= w.NormalizedEnd)
            {
                mask.Add(w.Tags);
            }
        }
    }
}
