using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基础状态动画映射库（ScriptableObject 资产）。
///
/// ═══ 2.0 职责 ═══
///
/// 仅用于 4 支柱中非 Action 的基础状态（Locomotion / Airborne / Dead）的动画映射。
/// Action 支柱的动画由 ActionDataSO.MainClip 直接携带，不经过本库。
///
/// ═══ 使用方式 ═══
///
/// 1. Create → GameMain → Animation → Player Anim Manager
/// 2. 配置条目：StateName 填状态类名（如 "PlayerLocomotionState"）
/// 3. 拖入对应 Clip，调整过渡时长和速度
/// 4. 将资产拖到 PlayerAnimController.animLibrary 字段
/// </summary>
[CreateAssetMenu(fileName = "PlayerAnimLibrary", menuName = "GameMain/Animation/Player Anim Manager")]
public class PlayerAnimManagerSO : ScriptableObject
{
    [Serializable]
    public class AnimClipEntry
    {
        [Tooltip("状态名（与状态类名一致，如 PlayerLocomotionState、PlayerAirborneState、PlayerDeadState）")]
        public string StateName;

        [Tooltip("动画片段")]
        public AnimationClip Clip;

        [Tooltip("从上一个动画过渡到此动画的时长（秒）")]
        [Range(0f, 1f)]
        public float TransitionDuration = 0.2f;

        [Tooltip("播放速度倍率")]
        [Range(0.1f, 20f)]
        public float Speed = 1f;

        [Tooltip("步幅匹配：动画按该参考速度（m/s）制作；0=关闭，仅用 Speed。实际倍率≈当前平面速/参考速×Speed。")]
        public float ReferenceLocomotionSpeed;

        [Tooltip("是否循环播放（Locomotion 循环，Dead 单次）")]
        public bool IsLooping = true;
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
