using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家动画切片库（ScriptableObject 资产）。
/// 在 Inspector 中配置每个状态对应的 AnimationClip 及参数。
///
/// 使用方式：
/// 1. 右键 Create → GameMain → Animation → PlayerAnimManagerSO
/// 2. 在 Inspector 中为每个状态拖入对应的 AnimationClip
/// 3. 调整 transitionDuration、speed 等参数调手感
/// 4. 将资产拖到 PlayerAnimManager 的 AnimClipEntry 字段
/// </summary>
[CreateAssetMenu(fileName = "PlayerAnimLibrary", menuName = "GameMain/Animation/Player Anim Manager")]
public class PlayerAnimManagerSO : ScriptableObject
{
    [Serializable]
    public class AnimClipEntry
    {
        [Tooltip("状态名（必须和状态类的 StateId 一致，如 PlayerIdleState）")]
        public string StateName;

        [Tooltip("动画片段")]
        public AnimationClip Clip;

        [Tooltip("从上一个动画过渡到此动画的时长（秒）")]
        [Range(0f, 1f)]
        public float TransitionDuration = 0.2f;

        [Tooltip("播放速度倍率")]
        [Range(0.1f, 20f)]
        public float Speed = 1f;

        [Tooltip("是否循环播放（Idle/Run 循环，Attack/Jump/Dodge 单次）")]
        public bool IsLooping = true;

        [Tooltip("攻击动画的命中帧位置（0~1 归一化时间），用于伤害判定时机")]
        [Range(0f, 1f)]
        public float HitFrameNormalized = 0.5f;

        [Tooltip("是否启用根运动（Root Motion）")]
        public bool ApplyRootMotion;

        [Tooltip("动画混合的层级（0=全身，1=上半身覆盖等），预留分层动画扩展")]
        public int Layer;
    }

    [SerializeField] private AnimClipEntry[] entries;

    // 运行时字典缓存（懒加载）
    private Dictionary<string, AnimClipEntry> _lookup;

    /// <summary>按状态名查找动画配置。</summary>
    public AnimClipEntry GetEntry(string stateName)
    {
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(stateName, out var entry);
        return entry;
    }

    /// <summary>获取所有条目（供 Inspector/调试用）。</summary>
    public AnimClipEntry[] GetAllEntries() => entries;

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, AnimClipEntry>();
        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.StateName))
            {
                _lookup[entry.StateName] = entry;
            }
        }
    }

    // SO 重新加载时清缓存
    private void OnEnable() => _lookup = null;
}
