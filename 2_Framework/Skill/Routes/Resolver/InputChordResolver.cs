using UnityEngine;

/// <summary>
/// 输入组合键解析器 — 把 (Trigger + Modifier 缓冲方向) 翻译为 <see cref="DirectionalRouteType"/>。
///
/// ═══ 设计契约 ═══
///   · 纯静态、纯函数：无状态、无副作用，便于 EditMode 测试。
///   · 不直接读 InputReader / InputModifierBuffer — 调用者把 MoveBuffered 传入。
///   · 8 方向阈值固定（diag 阈值小于 cardinal）：方便手柄/键盘统一。
///   · DirectionalRouteType 枚举定义在 SkillRouteEnums.cs（不在本文件重复声明）。
/// </summary>
public static class InputChordResolver
{
    public const float CardinalThreshold = 0.5f;
    public const float DiagonalThreshold = 0.35f;

    public static DirectionalRouteType Resolve(Vector2 moveBuffered,
        DirectionalRouteType defaultDir = DirectionalRouteType.Forward)
    {
        if (moveBuffered.sqrMagnitude < 0.01f)
        {
            return defaultDir;
        }

        var x = moveBuffered.x;
        var y = moveBuffered.y;
        var ax = Mathf.Abs(x);
        var ay = Mathf.Abs(y);

        if (ax >= DiagonalThreshold && ay >= DiagonalThreshold)
        {
            if (y > 0f && x > 0f) return DirectionalRouteType.ForwardRight;
            if (y > 0f && x < 0f) return DirectionalRouteType.ForwardLeft;
            if (y < 0f && x > 0f) return DirectionalRouteType.BackwardRight;
            return DirectionalRouteType.BackwardLeft;
        }

        if (ay >= CardinalThreshold && ay > ax)
        {
            return y > 0f ? DirectionalRouteType.Forward : DirectionalRouteType.Backward;
        }

        if (ax >= CardinalThreshold)
        {
            return x > 0f ? DirectionalRouteType.Right : DirectionalRouteType.Left;
        }

        return defaultDir;
    }

    /// <summary>
    /// 184.1 W7 — 以 <paramref name="logicForward"/> 为 +Z，将世界输入方向映射为 8 向 Route。
    /// </summary>
    public static DirectionalRouteType ResolveRelativeToLogicForward(
        Vector3 worldInputDir,
        Vector3 logicForward,
        DirectionalRouteType defaultDir = DirectionalRouteType.Forward)
    {
        worldInputDir.y = 0f;
        logicForward.y = 0f;
        if (worldInputDir.sqrMagnitude < 0.0001f)
        {
            return defaultDir;
        }

        if (logicForward.sqrMagnitude < 0.0001f)
        {
            logicForward = Vector3.forward;
        }

        var basis = Quaternion.LookRotation(logicForward.normalized, Vector3.up);
        var local = Quaternion.Inverse(basis) * worldInputDir.normalized;
        return Resolve(new Vector2(local.x, local.z), defaultDir);
    }
}
