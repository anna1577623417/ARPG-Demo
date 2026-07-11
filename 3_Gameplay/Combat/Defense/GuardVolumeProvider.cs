using UnityEngine;

/// <summary>
/// 216.3 M5 L1 — 格挡前向 Volume 运行时（POCO，无 Collider）。
/// <para>由 <see cref="DefenseClip"/> Active 区间驱动：Begin → Tick（更新朝向）→ End。</para>
/// <para>L1 只开窗 + Log；L2 由 Resolver 用 <see cref="ContainsPoint"/> 判 Blocked。</para>
/// </summary>
public sealed class GuardVolumeProvider
{
    public DefenseClip Clip;
    public Entity Owner;
    public bool Active { get; private set; }

    public Vector3 Origin { get; private set; }
    public Vector3 Forward { get; private set; }
    public float AngleDegrees { get; private set; }
    public float Range { get; private set; }

    /// <summary>开窗：Active 进入时由 ActionTimelineRuntime 调用。</summary>
    public void Begin(in DefenseClip clip, Entity owner)
    {
        Clip = clip;
        Owner = owner;
        Active = true;
        AngleDegrees = clip.GuardAngleDegrees > 0.01f ? clip.GuardAngleDegrees : 120f;
        Range = clip.GuardRange > 0.01f ? clip.GuardRange : 1.5f;
        SamplePose();

        DefenseRuntimeRegistry.RegisterGuard(owner, this);

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Resolve] GUARD window on angle={AngleDegrees:F0} range={Range:F2} " +
                $"clip={SafeName(clip.ResolvedName)} active={clip.ActiveStart:F2}~{clip.ActiveEnd:F2}");
        }
    }

    /// <summary>Active 期每帧：跟随 Owner 朝向刷新 Origin/Forward。</summary>
    public void Tick()
    {
        if (!Active)
        {
            return;
        }

        SamplePose();
    }

    public void End()
    {
        if (!Active)
        {
            return;
        }

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log($"[Resolve] GUARD window off clip={SafeName(Clip.ResolvedName)}");
        }

        var owner = Owner;
        Active = false;
        Owner = null;
        DefenseRuntimeRegistry.UnregisterGuard(owner, this);
    }

    /// <summary>
    /// 世界点是否落在前向扇形内（半角 = AngleDegrees/2）。
    /// L2 Resolver 用；L1 仅设施就位。
    /// </summary>
    public bool ContainsPoint(Vector3 worldPoint)
    {
        if (!Active || Range <= 0.01f)
        {
            return false;
        }

        var to = worldPoint - Origin;
        to.y = 0f;
        var dist = to.magnitude;
        if (dist > Range || dist < 1e-4f)
        {
            return false;
        }

        var fwd = Forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f)
        {
            return false;
        }

        fwd.Normalize();
        to /= dist;
        var halfRad = AngleDegrees * 0.5f * Mathf.Deg2Rad;
        return Vector3.Dot(fwd, to) >= Mathf.Cos(halfRad);
    }

    void SamplePose()
    {
        if (Owner == null)
        {
            Origin = Vector3.zero;
            Forward = Vector3.forward;
            return;
        }

        var t = Owner.transform;
        Origin = t.position;
        Forward = t.forward;
    }

    static string SafeName(string name) =>
        string.IsNullOrEmpty(name) ? "?" : name;
}
