using System;
using UnityEngine;

/// <summary>185.2 — 运动相位快照（Apex 阈值复用 168.3 AirInterruptPolicy）。</summary>
public readonly struct PhaseContext
{
    public readonly bool IsGrounded;
    public readonly bool IsAirborne;
    public readonly bool IsAscending;
    public readonly bool IsApex;
    public readonly bool IsDescending;
    public readonly float VerticalSpeed;

    public PhaseContext(
        bool isGrounded,
        bool isAirborne,
        bool isAscending,
        bool isApex,
        bool isDescending,
        float verticalSpeed)
    {
        IsGrounded = isGrounded;
        IsAirborne = isAirborne;
        IsAscending = isAscending;
        IsApex = isApex;
        IsDescending = isDescending;
        VerticalSpeed = verticalSpeed;
    }

    public static PhaseContext FromPlayer(Player player, float apexThreshold = 1.5f)
    {
        if (player == null)
        {
            return default;
        }

        var vy = player.VerticalSpeed;
        var grounded = player.IsGrounded;
        var airborne = !grounded;
        var threshold = Mathf.Max(0.01f, apexThreshold);
        return new PhaseContext(
            grounded,
            airborne,
            airborne && vy > threshold,
            airborne && Mathf.Abs(vy) <= threshold,
            airborne && vy < -threshold,
            vy);
    }

    public static PhaseContext FromPlayer(Player player)
    {
        var threshold = 1.5f;
        if (player?.SkillEntryLoadout != null)
        {
            threshold = player.SkillEntryLoadout.AirInterruptPolicy.ApexVyThreshold;
        }

        return FromPlayer(player, threshold);
    }
}

[Flags]
public enum PhaseMask : byte
{
    None = 0,
    Grounded = 1 << 0,
    Airborne = 1 << 1,
    Ascending = 1 << 2,
    Apex = 1 << 3,
    Descending = 1 << 4,
}
