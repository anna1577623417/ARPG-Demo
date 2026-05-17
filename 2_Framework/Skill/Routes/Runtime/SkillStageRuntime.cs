using UnityEngine;

/// <summary>
/// Stage 级运行时 — 一次 Route 释放期间，CurrentStage 由 RouteRuntime 推进。
///
/// ═══ 设计契约 ═══
///   · 0-GC：本类是普通 class（非 struct）由 RouteRuntime 持有并跨帧复用；切换 Stage 仅写字段。
///   · 不持有 ActionExecutor / MotionExecutor 引用 — 这些资源在 PlayerActionState 侧；本类只描述"当前播到哪"。
///   · 命中累计内嵌于 SkillRouteContext.HitTally（Stage 切换时由 RouteRuntime 调 Reset）。
/// </summary>
public sealed class SkillStageRuntime
{
    public SkillStageDefinition Definition;
    public int IndexInRoute;          // 在 Route.Stages[] 中的下标（MultiStage 用）
    public float Elapsed;              // 自 Stage 进入起累计的秒数
    public float DurationSeconds;      // Stage.ResolveDurationSeconds()，缓存避免每帧重算
    public bool Completed;             // Elapsed ≥ Duration 即置真；待 Transition 决定下一步

    /// <summary>当前 Stage 已开的 Transition（Auto/OnHit 等命中时缓存，待 RouteRuntime 推进）。</summary>
    public int ArmedTransitionIndex = -1;

    /// <summary>开窗剩余时间（用于 HUD 提示"现在按下一段还来得及"）。</summary>
    public float ArmedTransitionWindowRemain;

    public void Enter(SkillStageDefinition def, int idx)
    {
        Definition = def;
        IndexInRoute = idx;
        Elapsed = 0f;
        DurationSeconds = def != null ? def.ResolveDurationSeconds() : 0f;
        Completed = false;
        ArmedTransitionIndex = -1;
        ArmedTransitionWindowRemain = 0f;
    }

    public void Tick(float dt)
    {
        Elapsed += Mathf.Max(0f, dt);
        if (DurationSeconds <= 0.0001f)
        {
            Completed = true;
            return;
        }

        if (Elapsed >= DurationSeconds)
        {
            Completed = true;
        }
    }

    public float NormalizedTime
    {
        get
        {
            if (DurationSeconds <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Elapsed / DurationSeconds);
        }
    }
}
