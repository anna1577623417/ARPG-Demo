using System;
using UnityEngine;

/// <summary>
/// 177.1 W0 — 单个 Stance 的完整定义（数据驱动核心）。
/// <para>策划编辑：Locomotion / Tuning / 高度 / Enter/Exit Action / Ability 授予屏蔽 / 输入映射 / 相机 / 音效。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Stance/Stance Definition", fileName = "StanceDefinition_")]
public sealed class StanceDefinitionSO : ScriptableObject
{
    [Header("身份")]
    public StanceId Id = StanceId.Normal;
    public string DisplayName = "Normal";

    [Header("Locomotion 接管")]
    [Tooltip("此 Stance 期间使用的 LocomotionProfile（Bindings → ActionDataSO 与 Tuning 全套）。")]
    public LocomotionProfile LocomotionProfile;

    [Tooltip("速度倍率 / 加速度 / 转身阈值；若为空则沿用 LocomotionProfile.Tuning。")]
    public LocomotionTuningSO Tuning;

    [Tooltip("KCC Capsule 高度倍率（Crouch=0.65 / Normal=1.0）。")]
    [Range(0.30f, 2.0f)] public float HeightScale = 1f;

    [Tooltip("进入 Stance 的过渡 Action（如 APose2Crouch）。可为空 → 立即切换。")]
    public ActionDataSO EnterAction;

    [Tooltip("退出 Stance 的过渡 Action（如 Crouch2APose）。可为空 → 立即切换。")]
    public ActionDataSO ExitAction;

    [Header("起立 / 进入约束")]
    [Tooltip("退出此 Stance 时是否检测头顶障碍（Crouch → Normal 需要）。")]
    public bool RequireHeadClearanceOnExit;

    [Tooltip("头顶障碍 SphereCast 起点向上的距离（米）+ 半径。")]
    public float HeadClearanceProbeHeight = 1.8f;
    public float HeadClearanceProbeRadius = 0.30f;
    public LayerMask HeadClearanceMask = ~0;

    [Header("Ability 授予 / 屏蔽（W3 引入 AbilityId 前用字符串表达）")]
    [Tooltip("Stance 期间额外授予的能力名（如 'Stealth.Step'）。")]
    public string[] GrantedAbilityNames = Array.Empty<string>();

    [Tooltip("Stance 期间屏蔽的能力名（如 'Dodge.Roll'；注意 Crouch 不屏蔽 'Jump'，因为 Crouch+Sprint+Space 走 175.2 LongJump Variant）。")]
    public string[] BlockedAbilityNames = Array.Empty<string>();

    [Header("Tags 注入（进入 Stance 自动加，离开移除）")]
    public GameplayTagMask GrantedStateTags;

    [Header("输入映射")]
    [Tooltip("此 Stance 下移动倍率（与 Tuning 叠加）。")]
    [Range(0f, 2f)] public float WalkMultiplier = 1f;

    [Range(0f, 2f)] public float RunMultiplier = 1f;

    [Range(0f, 720f)] public float RotationSpeedDegPerSec = 540f;

    [Header("反馈")]
    [Tooltip("相机预设引用（C# 类型由后续 171.1 接入）。")]
    public ScriptableObject CameraPreset;

    [Tooltip("Stance 持续音效。")]
    public ScriptableObject StanceLoopAudio;

    [Header("Variant 兼容（175.2 跳跃系统集成）")]
    [Tooltip("此 Stance 下按 Space 时 JumpContext.CurrentStance 上报的字节值（让 JumpVariantResolver 可读）。")]
    public byte JumpContextStanceByte = 0;
}
