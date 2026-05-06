using UnityEngine;

/// <summary>GCD（公共冷却）与简单墙钟寄存器。</summary>
public sealed class CooldownTracker
{
    float _gcdRemaining;

    /// <summary>典型 0.3~1.0s。</summary>
    public float GcdDurationSeconds = 0.5f;

    public float GcdRemaining => _gcdRemaining;

    public void TriggerGlobalCooldown(float? overrideSeconds = null)
    {
        var d = overrideSeconds ?? GcdDurationSeconds;
        _gcdRemaining = Mathf.Max(_gcdRemaining, Mathf.Max(0f, d));
    }

    public bool IsGlobalCooldownActive() => _gcdRemaining > 0f;

    public void Tick(float deltaTime)
    {
        _gcdRemaining = Mathf.Max(0f, _gcdRemaining - deltaTime);
    }
}
