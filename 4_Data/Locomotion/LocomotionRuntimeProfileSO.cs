using UnityEngine;

/// <summary>
/// 179.1 §2.2 — Stride 全局参数（速度平滑 / 档位区间 / 迟滞带 / 足相位）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Locomotion/Locomotion Runtime Profile", fileName = "LocomotionRuntimeProfile_")]
public sealed class LocomotionRuntimeProfileSO : ScriptableObject
{
    [Header("Speed Smoothing")]
    [Tooltip("Motor.CurrentSpeed → SmoothedSpeed 的时间常数（秒）。0.08s = 一帧抖动几乎消失，移动突变 ~0.24s 追上。")]
    [Min(0.001f)] public float SpeedSmoothingTau = 0.08f;

    [Header("Tier 适用区间 [m/s]（迟滞重叠 0.5m/s）")]
    public Vector2 WalkRange = new(0f, 3.0f);
    public Vector2 RunRange = new(2.5f, 7.5f);
    public Vector2 SprintRange = new(7.0f, 12.0f);

    [Tooltip("迟滞分数：边界 ±15% 才触发换档（防止抖动）。")]
    [Range(0f, 0.5f)] public float HysteresisFraction = 0.15f;

    [Header("Foot Phase")]
    public bool EnableFootPhase = true;

    [Tooltip("0~1 表示完整步循环；左脚落地=0.0 / 右脚落地=0.5。")]
    [Range(0f, 1f)] public float FootPhaseOffset = 0f;
}
