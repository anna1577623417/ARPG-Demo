using System;
using UnityEngine;

/// <summary>
/// 184.4 — Transition 原型语义；默认由 <see cref="TransitionType"/> 继承，少数资产可 Override。
/// </summary>
[Serializable]
public struct MotionGrammarRule
{
    [Tooltip("拥有表现权 = 占据 Animator 主轨。")]
    public bool OwnsPresentation;

    [Tooltip("消费方向变化 = Stop/Pivot 期间 Tap Facing 缓存而不插 Turn。")]
    public bool ConsumesDirectionChange;

    [Tooltip("消费动量变化。")]
    public bool ConsumesMomentumChange;

    [Tooltip("阻止其它 Transition 同时表现。")]
    public bool BlocksOtherTransitions;
}
