using UnityEngine;

/// <summary>
/// 206.1 / 206.2 — 方向输入双模式（Chord vs Motion）纯函数解析。
/// Player / EditMode Spec 共用，避免在 Player 上堆测试反射。
/// </summary>
public static class DirectionalDualModeResolver
{
    public static string ClassifyMode(float holdDurationSec, float chordWindowSec, float motionWindowSec)
    {
        if (holdDurationSec < 0f)
        {
            return "Neutral";
        }

        if (holdDurationSec >= motionWindowSec)
        {
            return "Motion";
        }

        if (holdDurationSec > chordWindowSec)
        {
            return "Sustained→Motion";
        }

        if (holdDurationSec <= chordWindowSec)
        {
            return "Chord";
        }

        return "Grey→Chord";
    }

    public static bool IsMotionMode(float holdDurationSec, float chordWindowSec, float motionWindowSec)
    {
        if (holdDurationSec < 0f)
        {
            return false;
        }

        // 206.5 — 持续按住 WASD（超过 Chord 短按窗口）→ Motion：沿当前 LogicForward F-Dodge。
        if (holdDurationSec > chordWindowSec)
        {
            return true;
        }

        return holdDurationSec >= motionWindowSec;
    }

    public static DirectionalRouteType Resolve(
        Vector2 moveBuffered,
        float holdDurationSec,
        float chordWindowSec,
        float motionWindowSec,
        out bool isMotionMode,
        out string modeLabel)
    {
        modeLabel = ClassifyMode(holdDurationSec, chordWindowSec, motionWindowSec);
        isMotionMode = IsMotionMode(holdDurationSec, chordWindowSec, motionWindowSec);
        if (isMotionMode)
        {
            return DirectionalRouteType.Forward;
        }

        return InputChordResolver.Resolve(moveBuffered);
    }
}
