using UnityEngine;

/// <summary>
/// CD 剩余秒 → char[]（1 位小数），供 TMP SetText(char[], …) 使用，避免 ToString / 插值 GC。
/// </summary>
public static class UiCooldownSecondsFormatter
{
    public const int MaxChars = 8;

    /// <summary>将剩余秒写入 buffer，格式 d.dd（至少 1 位小数，如 5.0、12.3）。</summary>
    /// <returns>有效字符数；0 表示应隐藏倒计时文本。</returns>
    public static int Write(float secondsRemaining, char[] buffer)
    {
        if (buffer == null || buffer.Length < 4)
        {
            return 0;
        }

        if (secondsRemaining <= 0.0001f)
        {
            return 0;
        }

        var tenths = Mathf.FloorToInt(secondsRemaining * 10f + 0.0001f);
        if (tenths <= 0)
        {
            return 0;
        }

        return WriteTenths(tenths, buffer);
    }

    /// <summary>直接由「十分之一秒」整数写入（用于与上一帧比较，跳过重复 SetText）。</summary>
    public static int WriteTenths(int tenths, char[] buffer)
    {
        if (buffer == null || buffer.Length < 4 || tenths <= 0)
        {
            return 0;
        }

        var whole = tenths / 10;
        var dec = tenths % 10;
        var len = 0;

        if (whole >= 100)
        {
            buffer[len++] = Digit(whole / 100);
            whole %= 100;
            buffer[len++] = Digit(whole / 10);
            buffer[len++] = Digit(whole % 10);
        }
        else if (whole >= 10)
        {
            buffer[len++] = Digit(whole / 10);
            buffer[len++] = Digit(whole % 10);
        }
        else
        {
            buffer[len++] = Digit(whole);
        }

        buffer[len++] = '.';
        buffer[len++] = Digit(dec);
        return len;
    }

    public static int ToDisplayTenths(float secondsRemaining)
    {
        if (secondsRemaining <= 0.0001f)
        {
            return 0;
        }

        return Mathf.FloorToInt(secondsRemaining * 10f + 0.0001f);
    }

    static char Digit(int d) => (char)('0' + Mathf.Clamp(d, 0, 9));
}
