using UnityEngine;

/// <summary>237 L1 — 方向意图时钟单点。窗口比较禁止直接使用 Time.time。</summary>
public static class InputClock
{
    public static float UnscaledNow => Time.unscaledTime;
}
