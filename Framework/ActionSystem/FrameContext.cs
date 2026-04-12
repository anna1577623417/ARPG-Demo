using UnityEngine;

/// <summary>
/// 单帧决策上下文（纯值类型）。
/// 在帧初由 <see cref="Player.BuildFrameContext"/> 构造，供
/// <see cref="TransitionResolver"/> 与各状态只读使用。
/// 设计原因：把运动学、资源与标签快照集中在一处，避免 Update 里散落多个临时引用类型。
/// </summary>
public struct FrameContext
{
    /// <summary>Unity 时间（秒），与意图过期时间同一时钟。</summary>
    public float Time;

    public float DeltaTime;

    public bool IsGrounded;

    public Vector3 PlanarVelocity;

    public float VerticalSpeed;

    /// <summary>当前帧用于仲裁的标签快照（只读语义，调用方勿改掩码对象引用）。</summary>
    public GameplayTagMask CurrentTags;

    public float StaminaCurrent;

    public float StaminaMax;
}
