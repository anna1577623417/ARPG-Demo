using UnityEngine;

/// <summary>
/// 175.2 W0 — Jump 相位状态机 + 重力倍率求解。
/// <para>纯逻辑组件，不直接操作 Player；外部按 <see cref="GravityScale"/> 调整重力贡献。</para>
/// <para>状态规则：TakeOff(1帧) → Rise(Vy>Th) → Apex(|Vy|≤Th, 含 ExtraHang) → Fall(Vy&lt;-Th) → Land(IsGrounded)。</para>
/// </summary>
public sealed class JumpPhaseDriver
{
    [System.Serializable]
    public struct Thresholds
    {
        public float ApexVyThreshold;        // |Vy| 在阈值内进入 Apex
        public float MinHoldBeforeVariable;  // VariableHeight 最小持续秒
    }

    public Thresholds Th = new Thresholds
    {
        ApexVyThreshold = 1.5f,
        MinHoldBeforeVariable = 0.05f,
    };

    public JumpPhase Phase { get; private set; } = JumpPhase.None;
    public float PhaseElapsed { get; private set; }
    public float TotalElapsed { get; private set; }
    public float ApexHoverRemaining { get; private set; }
    public float VariableHeightHoldElapsed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool JumpReleasedEarly { get; private set; }
    public float GravityScale { get; private set; } = 1f;

    JumpAbilityConfig _config;

    /// <summary>起跳时调用：写入有效配置 + 切到 TakeOff。</summary>
    public void BeginJump(in JumpAbilityConfig effective)
    {
        _config = effective;
        Phase = JumpPhase.TakeOff;
        PhaseElapsed = 0f;
        TotalElapsed = 0f;
        ApexHoverRemaining = _config.ExtraHangTimeSec;
        VariableHeightHoldElapsed = 0f;
        JumpHeld = true;
        JumpReleasedEarly = false;
        GravityScale = 0f;  // TakeOff 一帧零重力，冲量已加
    }

    /// <summary>外部每帧调用，传入当前 Vy + 是否接地 + Space 是否仍按住。</summary>
    public void Tick(float deltaTime, float currentVy, bool grounded, bool jumpHeld)
    {
        if (Phase == JumpPhase.None) return;

        TotalElapsed += deltaTime;
        PhaseElapsed += deltaTime;
        JumpHeld = jumpHeld;
        if (!jumpHeld && !JumpReleasedEarly) JumpReleasedEarly = true;

        // 触地直接进 Land
        if (grounded && Phase != JumpPhase.TakeOff)
        {
            TransitionTo(JumpPhase.Land);
            GravityScale = 1f;
            return;
        }

        switch (Phase)
        {
            case JumpPhase.TakeOff:
                // 一帧过渡，next frame -> Rise
                TransitionTo(JumpPhase.Rise);
                ApplyRiseGravity();
                break;

            case JumpPhase.Rise:
                if (currentVy <= Th.ApexVyThreshold && currentVy >= -Th.ApexVyThreshold)
                {
                    TransitionTo(JumpPhase.Apex);
                    GravityScale = _config.ApexGravityScale;
                }
                else if (currentVy < -Th.ApexVyThreshold)
                {
                    TransitionTo(JumpPhase.Fall);
                    GravityScale = _config.FallGravityScale;
                }
                else
                {
                    ApplyRiseGravity();
                }
                break;

            case JumpPhase.Apex:
                ApexHoverRemaining -= deltaTime;
                if (ApexHoverRemaining <= 0f || currentVy < -Th.ApexVyThreshold)
                {
                    TransitionTo(JumpPhase.Fall);
                    GravityScale = _config.FallGravityScale;
                }
                else
                {
                    GravityScale = _config.ApexGravityScale;
                }
                break;

            case JumpPhase.Fall:
                GravityScale = _config.FallGravityScale;
                break;
        }
    }

    void ApplyRiseGravity()
    {
        // VariableHeight：松开 Space 后超出 MinHold 则改用大重力压低
        if (JumpReleasedEarly)
        {
            VariableHeightHoldElapsed += Time.deltaTime;
            if (VariableHeightHoldElapsed > Th.MinHoldBeforeVariable)
            {
                GravityScale = _config.FallGravityScale * 1.5f;
                return;
            }
        }
        GravityScale = 1f;
    }

    void TransitionTo(JumpPhase next)
    {
        Phase = next;
        PhaseElapsed = 0f;
    }

    /// <summary>显式终结（Land 帧由外部调用 → 进 None，等下次 BeginJump）。</summary>
    public void Finish()
    {
        Phase = JumpPhase.None;
        PhaseElapsed = 0f;
        TotalElapsed = 0f;
        ApexHoverRemaining = 0f;
        VariableHeightHoldElapsed = 0f;
        JumpHeld = false;
        JumpReleasedEarly = false;
        GravityScale = 1f;
    }
}
